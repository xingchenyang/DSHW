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
            // 递归复制，但剪掉 node_modules 及常见缓存目录（可重建的构建产物，不该进备份，否则 0.x GB 级膨胀）
            CopyDirRecursive(new DirectoryInfo(src), dest);
        }

        private static readonly string[] _skipDirs =
        {
            "node_modules",      // npm/依赖产物，可随时重新安装
            "blob_storage",      // 缓存
            "indexeddb",         // 浏览器/运行时缓存
            "cache",             // 通用缓存
            ".cache"
        };

        private static void CopyDirRecursive(DirectoryInfo dir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in dir.GetFiles())
            {
                var target = Path.Combine(destDir, file.Name);
                file.CopyTo(target, overwrite: true);
            }

            foreach (var sub in dir.GetDirectories())
            {
                // 命中剪枝名单的子目录：整棵跳过（node_modules 等可重建产物不入备份）
                bool skip = Array.Exists(_skipDirs, n => string.Equals(n, sub.Name, StringComparison.OrdinalIgnoreCase));
                if (skip) continue;
                CopyDirRecursive(sub, Path.Combine(destDir, sub.Name));
            }
        }
    }
}
