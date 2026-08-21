using System.Diagnostics;

namespace DSHW.Desktop.Core
{
    /// <summary>安装进度回调：每捕获一行原始输出触发一次。</summary>
    public delegate void InstallOutputHandler(string line);

    /// <summary>
    /// 在本应用内执行 DSH 安装（npm.cmd install -g），把 stdout/stderr 原始流逐行回调，
    /// 供 UI 实时滚到"安装日志"面板。经 cmd.exe 规避 PowerShell 执行策略问题。
    /// </summary>
    public class Installer
    {
        private const string Package = "@deepseek-ai/dsh";

        /// <summary>
        /// 安装命令的日志详细度。
        /// <see cref="Minimal"/>：notice，关掉 audit/fund（极简）。
        /// <see cref="Verbose"/>：--loglevel=verbose，保留 audit/fund（手动排查的"完整"档）。
        /// <see cref="Silly"/>：--loglevel=silly，最全（exe 自动更新记录到日志用）。
        /// </summary>
        public enum InstallLogLevel { Minimal, Verbose, Silly }

        /// <summary>构建安装命令。explicitVersion 为空装最新版；mirror 为空用默认 registry。</summary>
        public static string BuildCommand(string? explicitVersion = null, string? mirror = null, InstallLogLevel level = InstallLogLevel.Minimal)
        {
            var sb = new System.Text.StringBuilder("npm.cmd install -g " + Package);
            if (!string.IsNullOrWhiteSpace(explicitVersion))
                sb.Append('@').Append(explicitVersion.Trim());
            if (!string.IsNullOrWhiteSpace(mirror))
                sb.Append(" --registry=").Append(mirror.Trim());
            switch (level)
            {
                case InstallLogLevel.Verbose:
                    sb.Append(" --loglevel=verbose");
                    break;
                case InstallLogLevel.Silly:
                    sb.Append(" --loglevel=silly");
                    break;
                default: // Minimal
                    sb.Append(" --no-audit --no-fund");
                    break;
            }
            return sb.ToString();
        }

        /// <summary>bool 重载：true = Verbose，false = Minimal（兼容既有"完整日志"开关）。</summary>
        public static string BuildCommand(string? explicitVersion, string? mirror, bool verboseLogs)
            => BuildCommand(explicitVersion, mirror, verboseLogs ? InstallLogLevel.Verbose : InstallLogLevel.Minimal);

        /// <summary>
        /// 执行安装。逐行回调 output。返回是否成功（npm 退出码 0）。
        /// cancel 被触发时中止（进程树被杀）。
        /// </summary>
        public static async Task<bool> RunAsync(string commandLine, InstallOutputHandler output, CancellationToken cancellationToken = default)
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
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;

                var outTask = Task.Run(() => RelayLines(proc.StandardOutput, output, cancellationToken), cancellationToken);
                var errTask = Task.Run(() => RelayLines(proc.StandardError, output, cancellationToken), cancellationToken);

                try
                {
                    await proc.WaitForExitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    return false;
                }

                await Task.WhenAll(outTask, errTask);
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task RelayLines(StreamReader reader, InstallOutputHandler output, CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;
                    try { output(line); } catch { }
                }
            }
            catch (OperationCanceledException) { }
            // 数据到达终止时不打断已输出的内容
        }
    }
}
