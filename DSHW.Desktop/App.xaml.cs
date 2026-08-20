using Microsoft.UI.Xaml;
using DSHW.Desktop.Core;
using DSHW.Desktop.Managers;
using Microsoft.Windows.ApplicationModel.Resources;
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

        private const int WM_TRAYICON = 0x0400 + 100;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

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

            _hwnd = WindowNative.GetWindowHandle(_window);
            var newWndProc = Marshal.GetFunctionPointerForDelegate<WndProcDelegate>(WndProc);
            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, newWndProc);
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                if (lParam == (IntPtr)WM_LBUTTONDBLCLK)
                {
                    _window?.Activate();
                    return IntPtr.Zero;
                }
                else if (lParam == (IntPtr)WM_RBUTTONUP)
                {
                    return IntPtr.Zero;
                }
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
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