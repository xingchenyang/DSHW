using DSHW.Desktop.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DSHW.Desktop
{
    public sealed partial class AboutWindow : Window
    {
        // 读取窗口所在显示器的 DPI（用于按缩放比例调整物理尺寸，保持等效尺寸一致）
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        public AboutWindow(string? dshVersion)
        {
            this.InitializeComponent();

            this.Title = ResourceHelper.GetString("About.Title", "About DSHW");

            // 图标（与主窗口一致；单文件模式从解压目录取）
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                // 目标显示器：主屏（避免跑到副屏）
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;

                // 先以未缩放尺寸放到目标屏，再读取该屏 DPI，最后按缩放比例定尺寸
                // （480x340 为 100% 缩放的等效尺寸；150% 时物理尺寸 = 720x510）
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = workArea.X + (workArea.Width - 480) / 2,
                    Y = workArea.Y + (workArea.Height - 340) / 2,
                    Width = 480,
                    Height = 340
                });

                uint dpi = GetDpiForWindow(hwnd);
                float scale = dpi > 0 ? dpi / 96f : 1f;
                int w = (int)(480 * scale);
                int h = (int)(340 * scale);
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32
                {
                    X = workArea.X + (workArea.Width - w) / 2,
                    Y = workArea.Y + (workArea.Height - h) / 2,
                    Width = w,
                    Height = h
                });

                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
            }

            VersionText.Text = $"DSHW {GetShellVersion()} · DSH {(string.IsNullOrEmpty(dshVersion) ? "--" : dshVersion)}";
            TaglineText.Text = ResourceHelper.GetString("About.Tagline", "DSH Workbench for Windows");
            DescriptionText.Text = ResourceHelper.GetString("About.Description", "A WinUI 3 + WebView2 desktop client for DSH.");
            CopyrightText.Text = ResourceHelper.GetString("About.Copyright", "Copyright © 2026 xingchenyang · MIT License");
        }

        private static string GetShellVersion()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "0.1.0" : $"{v.Major}.{v.Minor}.{v.Build}";
            }
            catch
            {
                return "0.1.0";
            }
        }
    }
}
