using DSHW.Desktop.Helpers;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace DSHW.Desktop.Managers
{
    public class TrayIconManager : IDisposable
    {
        private Window? _window;
        private IntPtr _hwnd;
        private bool _disposed = false;
        private uint _trayIconId = 1001;
        private IntPtr _trayIconHandle = IntPtr.Zero;
        private bool _trayIconOwned = false;

        // Win32 API 常量
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 100;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;

        // NOTIFYICONDATA 结构体
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        // 注意：Shell_NotifyIcon 在 shell32.dll（不是 user32.dll），且必须用 Unicode 版本
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, string lpIconName);

        // 从 exe 中提取应用图标（与 csproj 的 ApplicationIcon 一致）
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// 加载应用图标：优先从当前 exe 提取（确保与文件图标一致），失败时回退系统默认图标。
        /// </summary>
        private IntPtr LoadAppIcon()
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) &&
                ExtractIconEx(exePath, 0, out _, out var smallIcon, 1) > 0 &&
                smallIcon != IntPtr.Zero)
            {
                _trayIconOwned = true;
                return smallIcon;
            }
            return LoadIcon(IntPtr.Zero, "#32512");
        }

        public TrayIconManager(Window window)
        {
            _window = window;
            _hwnd = WindowNative.GetWindowHandle(window);
            CreateTrayIcon();
        }

        private void CreateTrayIcon()
        {
            // 加载应用图标（从 exe 提取，失败回退系统默认）
            _trayIconHandle = LoadAppIcon();

            // 从资源文件获取工具提示文本（失败时回退到默认文案）
            string tooltip = ResourceHelper.GetString("Tray.Tooltip", "DSHW - DSH Workbench");

            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = _trayIconId,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _trayIconHandle,
                szTip = tooltip
            };

            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private void RemoveTrayIcon()
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = _trayIconId
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
        }

        public void UpdateTooltip(string tooltip)
        {
            if (string.IsNullOrEmpty(tooltip))
            {
                tooltip = ResourceHelper.GetString("Tray.Tooltip", "DSHW - DSH Workbench");
            }

            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = _trayIconId,
                uFlags = NIF_TIP,
                szTip = tooltip
            };
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        public void Dispose()
        {
            if (_disposed) return;
            RemoveTrayIcon();
            // 仅销毁自己提取的图标句柄（系统默认图标无需销毁）
            if (_trayIconOwned && _trayIconHandle != IntPtr.Zero)
            {
                DestroyIcon(_trayIconHandle);
                _trayIconHandle = IntPtr.Zero;
                _trayIconOwned = false;
            }
            _disposed = true;
        }
    }
}
