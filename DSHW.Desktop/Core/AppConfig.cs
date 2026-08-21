using Microsoft.Extensions.Configuration;

namespace DSHW.Desktop.Core
{
    /// <summary>
    /// 应用配置：读取 exe 旁的 appsettings.json 中的启动/更新选项。
    /// 使用 Microsoft.Extensions.Configuration（csproj 已引用），单例懒加载，读取失败时全部回退默认值。
    /// </summary>
    public class AppConfig
    {
        private readonly IConfiguration _config;

        public AppConfig(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>启动时是否联网检查 DSH 最新版本并显示更新提示。</summary>
        public bool CheckUpdatesOnStart => GetBool("DeepSeek:Update:CheckOnStart", true);

        /// <summary>启动 DSH 是否采用 npx -y 自动升级（否则用已装的 dsh 命令，离线秒起）。</summary>
        public bool AutoUpdateOnStart => GetBool("DeepSeek:Update:AutoUpdate", false);

        private bool GetBool(string key, bool fallback)
        {
            try
            {
                var v = _config[key];
                return bool.TryParse(v, out var b) ? b : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>从 exe 旁 appsettings.json 构建配置；缺文件/解析失败时回退到空配置（各项落默认）。</summary>
        public static AppConfig Load()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                var builder = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                return new AppConfig(builder.Build());
            }
            catch
            {
                // 回退：空配置 → 所有 GetBool 走默认值
                return new AppConfig(new ConfigurationBuilder().Build());
            }
        }
    }
}
