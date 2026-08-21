namespace DSHW.Desktop.Core
{
    /// <summary>
    /// ~/.dsh 目录备份。
    /// 版本升级/降级可能改变 ~/.dsh 下配置格式（如 .credentials.yaml 在 0.1.1-rc 已变化），
    /// 因此自动更新前对整个 ~/.dsh 做一次快照，便于格式被破坏后完整回滚。
    /// </summary>
    public static class DshBackup
    {
        /// <summary>备份保留份数（最近的 N 份，更早删除）。</summary>
        public const int KeepCount = 5;

        /// <summary>源目录 ~/.dsh，不存在返回 null。</summary>
        public static string? SourceDir
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh");
                return Directory.Exists(dir) ? dir : null;
            }
        }

        /// <summary>
        /// 把整个 ~/.dsh 复制到 ~/.dsh.bak-yyyyMMdd-HHmmss，并清理超过保留份数的旧备份。
        /// 返回备份目录路径；源不存在/复制失败返回 null。
        /// </summary>
        public static string? Create()
        {
            var src = SourceDir;
            if (src == null) return null;

            try
            {
                var dest = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".dsh.bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                CopyDirectory(src, dest);
                PruneOldBackups();
                return dest;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>是否存在至少一份备份（用于"未备份过就更新"的强提示）。</summary>
        public static bool AnyBackupExists()
        {
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Directory.Exists(root) && Directory.GetDirectories(root, ".dsh.bak-*").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void PruneOldBackups()
        {
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var backups = Directory.GetDirectories(root, ".dsh.bak-*")
                    .OrderByDescending(d => d).ToList();
                foreach (var old in backups.Skip(KeepCount))
                {
                    try { Directory.Delete(old, recursive: true); } catch { }
                }
            }
            catch { }
        }

        private static void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(src, file);
                var target = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }
    }
}
