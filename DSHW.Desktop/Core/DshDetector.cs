using System.Diagnostics;

namespace DSHW.Desktop.Core
{
    /// <summary>
    /// DSH 安装检测与版本查询。
    /// 全部经 cmd.exe + npm.cmd 执行，避免 PowerShell 执行策略（Restricted）阻挡 npm.ps1 / npx.ps1。
    /// 
    /// 语义区分（修掉旧 Runner 把"registry 最新版"当成"运行版本"的 bug）：
    ///   InstalledVersion = 全局 npm ls 解析出的已装版本（离线、快、准）。
    ///   LatestVersion    = registry 上最新版本（在线，可能因网络失败为 null）。
    /// </summary>
    public class DshDetector
    {
        private const string AddrName = "@deepseek-ai/dsh";

        /// <summary>已安装的 DSH 版本（全局 npm ls），未装/失败返回 null。</summary>
        public string? InstalledVersion { get; private set; }

        /// <summary>registry 上最新版本；联网失败/超时返回 null（调用方据此静默不提示）。</summary>
        public string? LatestVersion { get; private set; }

        /// <summary>是否已安装（npm ls 解析出版本，或兜底发现 dsh.cmd shim 存在）。</summary>
        public bool IsInstalled { get; private set; }

        /// <summary>本次检测的结果；失败原因可读文本（用于状态栏/日志）。</summary>
        public string ResultMessage { get; private set; } = "";

        /// <summary>
        /// 同步一次状态：先查已装（npm ls -g），再视 checkLatest 决定是否联网查最新版。
        /// </summary>
        public async Task RefreshAsync(bool checkLatest)
        {
            try
            {
                // 1) 已装版本（离线）
                var installedVer = await RunCaptureAsync("npm.cmd ls -g @deepseek-ai/dsh --depth=0");
                InstalledVersion = ParseInstalled(installedVer ?? "");
            }
            catch
            {
                InstalledVersion = null;
            }

            // 2) 兜底：即使 npm ls 解析失败/超时，只要 dsh.cmd shim 存在即视为已装（避免误报"未安装"）
            bool cmdShimExists = ResolveDshCommandPath() != null;
            bool installed = InstalledVersion != null || cmdShimExists;
            IsInstalled = installed;

            if (installed)
            {
                ResultMessage = InstalledVersion != null ? $"installed {InstalledVersion}" : "installed (shim present)";
            }
            else
            {
                ResultMessage = "not installed";
            }

            // 3) 最新版（在线）。只有在真要联网查时才覆盖 LatestVersion，
            //    避免离线路径（checkLatest=false）把已查到的 latest 清空，导致"版本按钮偶尔能点、偶尔不能"。
            if (checkLatest || !installed)
            {
                try
                {
                    var latest = await RunCaptureAsync("npm.cmd view @deepseek-ai/dsh version --no-audit --no-fund");
                    if (!string.IsNullOrWhiteSpace(latest))
                    {
                        LatestVersion = latest.Split('\n')[0].Trim();
                    }
                    else
                    {
                        LatestVersion = null; // 查询无结果 → 清空
                    }
                }
                catch
                {
                    LatestVersion = null; // 联网失败 → 静默，不弹错
                }
            }
            // else: 未联网查询 → 保留上次的 LatestVersion（不覆盖）
        }

        /// <summary>是否存在本地可执行的 dsh 命令（校验 dsh.cmd shim）。未安装时用于防误报。</summary>
        public static string? ResolveDshCommandPath()
        {
            // npm 全局前缀下的 dsh.cmd（与 npm config get prefix 一致）
            try
            {
                var prefix = RunResult("npm.cmd config get prefix");
                if (string.IsNullOrWhiteSpace(prefix)) return null;
                var dir = prefix.Trim();
                var shim = Path.Combine(dir, "dsh.cmd");
                return File.Exists(shim) ? shim : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ParseInstalled(string output)
        {
            // npm ls 输出形如：
            //   C:\...\npm
            //   `-- @deepseek-ai/dsh@0.1.0-rc.7
            // 用正则直接从原始行抓 "@deepseek-ai/dsh@<ver>"，前置可能有树形字符（` ├ └ │ ─ 空格）。
            if (string.IsNullOrWhiteSpace(output)) return null;
            var rx = new System.Text.RegularExpressions.Regex(
                System.Text.RegularExpressions.Regex.Escape(AddrName) + "@([0-9][^\\s@]*)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (var raw in output.Split('\n'))
            {
                var m = rx.Match(raw);
                if (m.Success)
                {
                    return m.Groups[1].Value.Trim().TrimEnd('\r');
                }
            }
            return null;
        }

        /// <summary>捕获完整 stdout（含 stderr 合并）。</summary>
        private static async Task<string?> RunCaptureAsync(string commandLine)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {commandLine} 2>&1",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await p.WaitForExitAsync(cts.Token);
                var stdout = await outTask;
                var stderr = await errTask;
                return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            }
            catch
            {
                return null;
            }
        }

        private static string? RunResult(string commandLine)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {commandLine}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                p.WaitForExit(15000);
                return p.StandardOutput.ReadToEnd()?.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
