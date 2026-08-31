using System.Diagnostics;

namespace DSHW.Desktop.Core
{
    /// <summary>
    /// 编排"App 内自动更新/安装"：先备份 ~/.dsh → 用 --loglevel=silly 流式执行 npm install -g（最全日志用于排查）→ 返回结果。
    /// 日志回调逐行转发给 UI（安装日志面板），与 Installer 保持一致。
    /// </summary>
    public static class Updater
    {
        /// <summary>
        /// 执行完整更新流程。返回 (成功, 备份路径, 提示消息)：
        ///   success=false 时 message 说明原因（未备份/安装失败等）。
        /// log 逐行回调（用于流式显示）。
        /// </summary>
        public static async Task<(bool success, string? backupPath, string message)> RunUpdateAsync(
            string? targetVersion, string? mirror, InstallOutputHandler log, CancellationToken cancellationToken = default)
        {
            // 0) 提示：若 3080 已有 dsh 服务在跑，升级会覆盖其正在加载的全局包/锁定的进程，
            //    建议升级完成后重启 DSHW 以载入新版。此处仅提示，不强制停止（停止会打断当前 WebView2）。
            logWarningIfServiceRunning(log);

            // 1) 备份整个 ~/.dsh
            log("--- " + "Backing up ~/.dsh ..." + " ---");
            var backup = DshBackup.Create();
            if (backup == null)
            {
                log("!! " + "Backup failed; update aborted for safety.");
                return (false, null, "BackupFailed");
            }
            log("Backup: " + backup);

            // 2) 用最全日志级别安装（--loglevel=silly），全程流式
            var cmd = Installer.BuildCommand(targetVersion, mirror, Installer.InstallLogLevel.Silly);
            log("> " + cmd);
            var ok = await Installer.RunAsync(cmd, log, cancellationToken);

            if (!ok)
            {
                log("!! " + "Install failed; your data is backed up at " + backup);
                return (false, backup, "InstallFailed");
            }

            log("Install succeeded. Restart DSHW to load the new version.");
            return (true, backup, "Success");
        }

        /// <summary>若 3080 正被监听（有 dsh web 在跑），log 一行警示：升级会覆盖其加载的全局包，完成后需重启。</summary>
        private static void logWarningIfServiceRunning(InstallOutputHandler log)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                });
                var outt = p?.StandardOutput.ReadToEnd();
                if (p != null) p.WaitForExit(5000);
                if (string.IsNullOrEmpty(outt)) return;
                foreach (var line in outt.Split('\n'))
                {
                    if (line.Contains(":3080") && line.Contains("LISTENING"))
                    {
                        log("! A DSH web service is running (port 3080). npm install -g will overwrite the loaded global package; restart DSHW after the upgrade.");
                        return;
                    }
                }
            }
            catch { }
        }
    }
}
