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

            log("Install succeeded.");
            return (true, backup, "Success");
        }
    }
}
