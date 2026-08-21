using DSHW.Desktop.Core;
using DSHW.Desktop.Managers;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace DSHW.Desktop
{
    public partial class App : Application
    {
        private Runner? _runner;
        private TrayIconManager? _trayManager;
        private MainWindow? _window;
        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private WndProcDelegate? _wndProcDelegate; // 必须持有委托引用，防止被 GC 回收导致窗口消息处理崩溃

        private const int WM_TRAYICON = 0x0400 + 100;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        public App()
        {
            this.InitializeComponent();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _runner = new Runner();
            _trayManager = new TrayIconManager(_window);

            _runner.OnStatusChanged += OnRunnerStatusChanged;

            _window.SetRunner(_runner);
            _window.Closed += OnWindowClosed;
            _window.Activate();

            _ = _runner.RunAsync();

            // 子类化窗口以接收托盘消息；委托必须保存为字段防止 GC 回收
            _hwnd = WindowNative.GetWindowHandle(_window);
            _wndProcDelegate = WndProc;
            var newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, newWndProc);
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                if (lParam == (IntPtr)WM_LBUTTONDBLCLK)
                {
                    _window?.ShowWindow();
                    return IntPtr.Zero;
                }
                else if (lParam == (IntPtr)WM_RBUTTONUP)
                {
                    var cmd = _trayManager?.ShowContextMenu() ?? Managers.TrayIconManager.TrayMenuCommand.None;
                    switch (cmd)
                    {
                        case Managers.TrayIconManager.TrayMenuCommand.Show:
                            _window?.ShowWindow();
                            break;
                        case Managers.TrayIconManager.TrayMenuCommand.Hide:
                            _window?.HideWindow();
                            break;
                        case Managers.TrayIconManager.TrayMenuCommand.About:
                            ShowAbout();
                            break;
                        case Managers.TrayIconManager.TrayMenuCommand.Exit:
                            _window?.RequestExit();
                            break;
                    }
                    return IntPtr.Zero;
                }
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private AboutWindow? _aboutWindow;
        private async void ShowAbout()
        {
            // 打开"关于"前先取一次最新版本（离线、快），避免显示 DSH -- 占位
            if (_runner != null)
            {
                await _runner.RefreshDshVersionAsync(checkLatest: false);
            }
            if (_aboutWindow == null)
            {
                _aboutWindow = new AboutWindow(_runner?.InstalledVersion);
                _aboutWindow.Closed += (s, e) => _aboutWindow = null;
            }
            else
            {
                _aboutWindow.UpdateDshVersion(_runner?.InstalledVersion);
            }
            _aboutWindow.Activate();
        }

        private void OnRunnerStatusChanged(object? sender, RunnerStatusEventArgs e)
        {
            _window?.UpdateRunnerStatus(e);
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _runner?.Dispose();
            _trayManager?.Dispose();

            if (_hwnd != IntPtr.Zero && _oldWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
            }
        }
    }
}
