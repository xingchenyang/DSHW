using System;
using System.Diagnostics;
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
        private bool _isRunning = false;
        private string _webUIUrl = "http://127.0.0.1:3080";
        private bool _disposed = false;
        private CancellationTokenSource? _cts;

        public event EventHandler<RunnerStatusEventArgs>? OnStatusChanged;

        public bool IsRunning => _isRunning;
        public string WebUIUrl => _webUIUrl;

        public async Task<bool> RunAsync()
        {
            if (_isRunning) return true;

            try
            {
                if (!await CheckNodeJsAsync())
                {
                    OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                    {
                        IsRunning = false,
                        StatusMessage = "未找到 Node.js"
                    });
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c npx @deepseek-ai/dsh web",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _dshProcess = new Process { StartInfo = startInfo };
                _dshProcess.Start();
                _isRunning = true;

                OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                {
                    IsRunning = true,
                    Url = _webUIUrl,
                    StatusMessage = "DSH 已启动"
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
                    StatusMessage = $"启动失败: {ex.Message}"
                });
                return false;
            }
        }

        private async Task MonitorProcessAsync(CancellationToken token)
        {
            while (_isRunning && _dshProcess != null && !token.IsCancellationRequested)
            {
                await Task.Delay(3000, token);
                try
                {
                    if (_dshProcess.HasExited)
                    {
                        _isRunning = false;
                        OnStatusChanged?.Invoke(this, new RunnerStatusEventArgs
                        {
                            IsRunning = false,
                            StatusMessage = "DSH 进程已退出"
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

                // 使用 CancellationToken 实现超时等待
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(3000));
                await process.WaitForExitAsync(cts.Token);

                return process.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                // 超时，视为 Node.js 不可用
                return false;
            }
            catch
            {
                return false;
            }
        }

        public void Shutdown()
        {
            try
            {
                _cts?.Cancel();
                if (_dshProcess != null && !_dshProcess.HasExited)
                {
                    _dshProcess.Kill();
                    _dshProcess.WaitForExit(3000);
                }
            }
            catch { }
            _isRunning = false;
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