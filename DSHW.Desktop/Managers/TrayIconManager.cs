using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
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
        private readonly ResourceLoader _resourceLoader = new ResourceLoader();

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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, string lpIconName);

        public TrayIconManager(Window window)
        {
            _window = window;
            _hwnd = WindowNative.GetWindowHandle(window);
            CreateTrayIcon();
        }

        private void CreateTrayIcon()
        {
            // 加载默认应用图标 (IDI_APPLICATION = 32512)
            IntPtr hIcon = LoadIcon(IntPtr.Zero, "#32512");

            // 从资源文件获取工具提示文本
            string tooltip = _resourceLoader.GetString("Tray.Tooltip");
            if (string.IsNullOrEmpty(tooltip))
            {
                tooltip = "DSHW - DSH Workbench";
            }

            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = _trayIconId,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = hIcon,
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
                tooltip = _resourceLoader.GetString("Tray.Tooltip");
                if (string.IsNullOrEmpty(tooltip))
                {
                    tooltip = "DSHW - DSH Workbench";
                }
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
            _disposed = true;
        }
    }
}