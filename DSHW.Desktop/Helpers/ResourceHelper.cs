using Microsoft.Windows.ApplicationModel.Resources;

namespace DSHW.Desktop.Helpers
{
    /// <summary>
    /// 多语言资源读取（.resw → resources.pri）。
    /// 
    /// 说明：在 Unpackaged（WindowsPackageType=None）模式下，MRT Core 的默认
    /// ResourceLoader 无法解析资源（NamedResource 找不到）；必须通过
    /// ResourceManager.MainResourceMap 按斜杠层级路径读取，
    /// 例如键 "Tray.Tooltip" → "Resources/Tray/Tooltip"。
    /// 该写法在 Packaged 模式下同样可用。
    /// </summary>
    public static class ResourceHelper
    {
        private static readonly ResourceManager _manager = new ResourceManager();

        /// <summary>按资源键读取本地化字符串；失败时返回空字符串。</summary>
        public static string GetString(string resourceKey)
        {
            try
            {
                var path = "Resources/" + resourceKey.Replace('.', '/');
                var candidate = _manager.MainResourceMap.GetValue(path);
                return candidate?.ValueAsString ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>按资源键读取本地化字符串；失败时回退到默认值。</summary>
        public static string GetString(string resourceKey, string fallback)
        {
            var value = GetString(resourceKey);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
