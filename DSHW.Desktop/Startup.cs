using System.Runtime.CompilerServices;

namespace DSHW.Desktop
{
    /// <summary>
    /// 单文件发布（PublishSingleFile）时，WindowsAppSDK 的原生库被解压到临时目录，
    /// 必须在该库加载前把 MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY 指向解压目录。
    /// ModuleInitializer 在 Main 之前执行，比任何 WinAppSDK 初始化都早。
    /// 普通（非单文件）模式下此设置无副作用。
    /// </summary>
    internal static class Startup
    {
        [ModuleInitializer]
        internal static void SetWindowsAppRuntimeBaseDirectory()
        {
            Environment.SetEnvironmentVariable(
                "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
                AppContext.BaseDirectory);
        }
    }
}
