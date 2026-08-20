using System.Runtime.CompilerServices;

namespace DSHW.Desktop
{
    /// <summary>
    /// 应用级早期初始化（ModuleInitializer 在 Main 之前执行）。
    /// 1. 单文件发布时，WindowsAppSDK 原生库解压到临时目录，须提前设置
    ///    MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY（普通模式无副作用）。
    /// 2. WebView2 用户数据目录固定到 %LOCALAPPDATA%\DSHW\WebView2，
    ///    避免默认落在 exe 旁产生 "*.WebView2" 文件夹污染发布目录。
    /// </summary>
    internal static class Startup
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            Environment.SetEnvironmentVariable(
                "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
                AppContext.BaseDirectory);

            try
            {
                var webView2Dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DSHW", "WebView2");
                System.IO.Directory.CreateDirectory(webView2Dir);
                Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webView2Dir);
            }
            catch
            {
                // 设置失败则回退 WebView2 默认位置（可能落在 exe 旁，但不影响运行）
            }
        }
    }
}
