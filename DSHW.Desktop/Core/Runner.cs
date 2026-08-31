using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DSHW.Desktop.Core
{
    public class RunnerStatusEventArgs : EventArgs
    {
        public bool IsRunning { get; set; }
        public string Url { get; set; } = "http://127.0.0.1:3080";
        public string? StatusMessage { get; set; }
    }

    public class Runner : IDisposable
    {
        private Process? _dshProcess;
        private bool _ownsProcess = false;      // 是否由本应用启动的 DSH（否则是复用了已存在的服务）
        private int _listenerPid = 0;           // 实际监听端口的进程（可能是 npx 退出后的孤儿 dsh）
        private bool _isRunning = false;
        private readonly string _host = "127.0.0.1";
        private readonly int _port = 3080;
        private readonly string _webUIUrl;
        private bool _disposed = false;
        private CancellationTokenSource? _cts;

        public event EventHandler<RunnerStatusEventArgs>? OnStatusChanged;

        /// <summary>端口空闲且 Node 存在但 dsh 未安装时触发，供 UI 弹出安装引导。</summary>
        public event EventHandler<string?>? OnDshNotInstalled;

        public bool IsRunning => _isRunning;
        public string WebUIUrl => _webUIUrl;

        /// <summary>已安装的 DSH 版本（全局 npm ls；离线）。</summary>
        public string? InstalledVersion { get; private set; }

        /// <summary>registry 上最新 DSH 版本（在线；联网失败为 null）。</summary>
        public string? LatestVersion { get; private set; }

        private readonly DshDetector _detector = new();
        private readonly AppConfig _config;

        public Runner(AppConfig? config = null)
        {
            _webUIUrl = $"http://{_host}:{_port}";
            _config = config ?? AppConfig.Load();
        }

        public async Task<bool> RunAsync()
        {
            if (_isRunning) return true;

            try
            {
                // 1) 端口已有服务 → 直接复用（不重复启动，避免 EADDRINUSE；退出时也不杀它）
                if (await IsServiceAliveAsync())
                {
                    _isRunning = true;
                    OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                    {
                        IsRunning = true,
                        Url = _webUIUrl,
                        StatusMessage = "Connected to existing DSH service"
                    });
                    return true;
                }

                // 2) 没有现成服务 → 需要 Node.js 并启动 DSH
                if (!await CheckNodeJsAsync())
                {
                    OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                    {
                        IsRunning = false,
                        StatusMessage = "Node.js not found"
                    });
                    return false;
                }

                // 3) 检查 dsh 是否已安装；未装 → 触发安装引导（不启动），让 UI 决定后续
                await _detector.RefreshAsync(checkLatest: false);
                InstalledVersion = _detector.InstalledVersion;
                if (!_detector.IsInstalled)
                {
                    OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                    {
                        IsRunning = false,
                        StatusMessage = "DSH is not installed"
                    });
                    OnDshNotInstalled?.Invoke(this, _detector.LatestVersion);
                    return false; // 不启动；等 UI 装好后重新 RunAsync
                }

                // 4) 准备启动。输出重定向到 %LOCALAPPDATA%\DSHW\dsh.log
                //    （单文件 exe 的 BaseDirectory 是临时解压目录，不可靠）
                var logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DSHW");
                string logPath;
                try
                {
                    System.IO.Directory.CreateDirectory(logDir);
                    logPath = System.IO.Path.Combine(logDir, "dsh.log");
                }
                catch
                {
                    logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dsh.log");
                }

                // 启动命令：默认 dsh web（离线、秒起）；仅当 appsettings 显式开启 AutoUpdate 才用 npx -y 自动升级
                // 加 --no-open：DSHW 用 WebView2 展示 UI，不让 dsh 再开默认浏览器（Edge）
                // -y：npx 首次运行会自动确认安装（无 stdin 的进程里交互提示会失败导致 DSH 退出）
                var startCmd = _config.AutoUpdateOnStart
                    ? "npx -y @deepseek-ai/dsh web --no-open"
                    : "dsh web --no-open";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {startCmd} 1> \"{logPath}\" 2>&1",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _dshProcess = new Process { StartInfo = startInfo };
                _dshProcess.Start();
                _ownsProcess = true;
                _isRunning = true;

                OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                {
                    IsRunning = true,
                    Url = _webUIUrl,
                    StatusMessage = "DSH started"
                });

                _cts = new CancellationTokenSource();
                _ = MonitorProcessAsync(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                {
                    IsRunning = false,
                    StatusMessage = $"Start failed: {ex.Message}"
                });
                return false;
            }
        }

        /// <summary>等待 DSH 服务在端口上就绪（最多 timeout）。已复用现有服务时立即返回 true。</summary>
        public async Task<bool> WaitForServiceAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await IsServiceAliveAsync()) return true;
                await Task.Delay(1000);
            }
            return await IsServiceAliveAsync();
        }

        /// <summary>TCP 探测端口（127.0.0.1:3080）是否有服务监听。</summary>
        private async Task<bool> IsServiceAliveAsync()
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(_host, _port);
                if (await Task.WhenAny(task, Task.Delay(1500)) != task)
                {
                    return false; // 连接超时
                }
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 刷新版本信息：已装版本（离线）+ 最新版本（在线，可选）。
        /// installed / latest 分开语义（避免旧实现把 registry 最新版当成运行版本）。
        /// </summary>
        public async Task RefreshDshVersionAsync(bool checkLatest)
        {
            await _detector.RefreshAsync(checkLatest: checkLatest);
            InstalledVersion = _detector.InstalledVersion;
            LatestVersion = _detector.LatestVersion;
        }

        private async Task MonitorProcessAsync(CancellationToken token)
        {
            while (_isRunning && _ownsProcess && !token.IsCancellationRequested)
            {
                await Task.Delay(3000, token);
                try
                {
                    // npx 启动 dsh 后可能自己退出并遗留下 dsh 孤儿进程（cmd 也会随之退出），
                    // 所以以"端口是否仍有监听"为准判断运行状态。
                    // 安全：只记录"首次出现"的监听进程（启动时端口空闲，第一个占端口的就是我们的 dsh）；
                    // 之后即使被其他服务（如 Harness GUI）抢占，也不会误记、更不会误杀。
                    var listenerPid = GetPortListenerPid();
                    if (_listenerPid == 0 && listenerPid != 0)
                    {
                        _listenerPid = listenerPid;
                    }
                    else if (listenerPid == 0 && (_dshProcess == null || _dshProcess.HasExited))
                    {
                        // cmd 已退出且端口无监听 → DSH 真的挂了
                        _isRunning = false;
                        OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                        {
                            IsRunning = false,
                            StatusMessage = "DSH process exited"
                        });
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        }

        /// <summary>netstat 解析 127.0.0.1:3080 上 LISTENING 的进程 PID；无则返回 0。</summary>
        private int GetPortListenerPid()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using var p = Process.Start(psi);
                var output = p?.StandardOutput.ReadToEnd();
                if (string.IsNullOrEmpty(output)) return 0;
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains($":{_port}") && line.Contains("LISTENING"))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[^1], out var pid) && pid != 0)
                            return pid;
                    }
                }
            }
            catch { }
            return 0;
        }

        private async Task<bool> CheckNodeJsAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "node",
                        Arguments = "--version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                process.Start();

                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(3000));
                await process.WaitForExitAsync(cts.Token);

                return process.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                return false; // 超时，视为 Node.js 不可用
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 关闭本应用启动的 DSH。
        /// 安全护栏：只杀"本应用启动期间观察到的"3080 监听进程（_listenerPid），
        /// 且杀前复查该 PID 仍是当前监听者；绝不误杀其他服务（如 Harness GUI）。
        /// </summary>
        public void Shutdown()
        {
            _cts?.Cancel();
            if (_ownsProcess)
            {
                // 1) cmd 进程树（cmd → npx → dsh，若链条还活着）
                if (_dshProcess != null && !_dshProcess.HasExited)
                {
                    try
                    {
                        RunTaskKill(_dshProcess.Id, force: false);
                        if (!_dshProcess.WaitForExit(3000))
                        {
                            RunTaskKill(_dshProcess.Id, force: true);
                        }
                    }
                    catch { }
                }

                // 2) 端口监听进程（覆盖 npx 退出后的孤儿 dsh）：
                //    仅当运行期间记录过监听 PID 且复查仍是同一进程时才杀
                if (_listenerPid != 0)
                {
                    var current = GetPortListenerPid();
                    if (current == _listenerPid)
                    {
                        RunTaskKill(_listenerPid, force: true);
                    }
                    _listenerPid = 0;
                }
            }
            _isRunning = false;
            _dshProcess = null;
            _ownsProcess = false;
        }

        private static void RunTaskKill(int pid, bool force)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/PID {pid} /T {(force ? "/F" : "")}".TrimEnd(),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(5000);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Shutdown();
            _cts?.Dispose();
            _dshProcess?.Dispose();
            _disposed = true;
        }
    }
}
