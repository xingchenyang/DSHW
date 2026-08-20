using DSHW.Desktop.Core;
using DSHW.Desktop.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;

namespace DSHW.Desktop
{
    public sealed partial class MainWindow : Window
    {
        private Runner? _runner;
        private bool _isWebViewReady = false;
        private bool _hasNavigatedToWebUi = false;
        private bool _uiLoadedSuccessfully = false;   // UI 已成功加载（此后不再被进程退出降级）

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "DSHW";
            this.ExtendsContentIntoTitleBar = true;

            // 设置窗口大小
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                appWindow.MoveAndResize(new RectInt32
                {
                    X = 100,
                    Y = 100,
                    Width = 1200,
                    Height = 800
                });

                // 窗口图标（任务栏 / Alt-Tab / 窗口菜单），与 exe 图标保持一致
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }

                // 标题栏按钮显式配色（深色栏 + 白色按钮，聚焦时可见；否则跟随系统浅色主题会不可见）
                // 参考：https://learn.microsoft.com/windows/apps/develop/title-bar
                var titleBar = appWindow.TitleBar;
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 0x3D, 0x3D, 0x3D);
                titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 0x2A, 0x2A, 0x2A);
                titleBar.InactiveForegroundColor = Windows.UI.Color.FromArgb(255, 0x99, 0x99, 0x99);

                // 标题栏高度与自定义 48px 栏匹配（Win11 生效；Win10 回退为标准高度）
                titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }

            this.Closed += OnWindowClosed;
        }

        public void SetRunner(Runner runner)
        {
            _runner = runner;
            _runner.OnStatusChanged += OnStatusChanged;
            _ = InitializeWebView();
        }

        // 统一的更新状态方法（资源读取失败时回退到键名，避免崩溃）
        private void UpdateStatus(string resourceKey)
        {
            var text = GetResourceString(resourceKey);
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = text;
            });
        }

        // 带参数的更新状态方法
        private void UpdateStatusWithArgs(string resourceKey, params object[] args)
        {
            var format = GetResourceString(resourceKey);
            var text = string.Format(format, args);
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = text;
            });
        }

        private static string GetResourceString(string resourceKey)
        {
            var text = ResourceHelper.GetString(resourceKey);
            return string.IsNullOrEmpty(text) ? resourceKey : text;
        }

        private async Task InitializeWebView()
        {
            try
            {
                UpdateStatus("Status.InitializingWebView");
                await WebView.EnsureCoreWebView2Async();
                WebView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    _isWebViewReady = true;
                    // 只在真正导航到 DSH UI 后更新状态（初始空白页不算）
                    if (_hasNavigatedToWebUi)
                    {
                        if (e.IsSuccess)
                        {
                            _uiLoadedSuccessfully = true;
                            UpdateStatus("Status.Ready");
                        }
                        else if (!_uiLoadedSuccessfully)
                        {
                            UpdateStatus("Status.Stopped");
                        }
                    }
                };
                await LoadWebUI();
            }
            catch (Exception ex)
            {
                UpdateStatusWithArgs("Status.WebViewError", ex.Message);
            }
        }

        private async Task LoadWebUI()
        {
            if (_runner == null) return;
            UpdateStatus("Status.Waiting");
            int retries = 0;
            while (!_runner.IsRunning && retries < 20)
            {
                await Task.Delay(500);
                retries++;
            }

            if (_runner.IsRunning)
            {
                UpdateStatus("Status.LoadingUI");
                _hasNavigatedToWebUi = true;
                WebView.Source = new Uri(_runner.WebUIUrl);
            }
            else
            {
                UpdateStatus("Status.Stopped");
            }
        }

        private void OnStatusChanged(object? sender, RunnerStatusEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.IsRunning && !_isWebViewReady)
                {
                    _ = LoadWebUI();
                }
                else if (!e.IsRunning && !_uiLoadedSuccessfully)
                {
                    UpdateStatus("Status.Stopped");
                }
            });
        }

        public void UpdateRunnerStatus(RunnerStatusEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.IsRunning && !_isWebViewReady)
                {
                    _ = LoadWebUI();
                }
                else if (!e.IsRunning && !_uiLoadedSuccessfully)
                {
                    UpdateStatus("Status.Stopped");
                }
            });
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            try
            {
                _runner?.Dispose();
                WebView?.Close();
            }
            catch { }
        }
    }
}