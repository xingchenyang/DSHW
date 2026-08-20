using DSHW.Desktop.Core;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Windows.ApplicationModel.Resources;
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
        private readonly ResourceLoader _resourceLoader = new ResourceLoader();

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
            }

            this.Closed += OnWindowClosed;
        }

        public void SetRunner(Runner runner)
        {
            _runner = runner;
            _runner.OnStatusChanged += OnStatusChanged;
            _ = InitializeWebView();
        }

        // 统一的更新状态方法
        private void UpdateStatus(string resourceKey)
        {
            var text = _resourceLoader.GetString(resourceKey);
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = text;
            });
        }

        // 带参数的更新状态方法
        private void UpdateStatusWithArgs(string resourceKey, params object[] args)
        {
            var format = _resourceLoader.GetString(resourceKey);
            var text = string.Format(format, args);
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = text;
            });
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
                    UpdateStatus("Status.Ready");
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
                else if (!e.IsRunning)
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
                else if (!e.IsRunning)
                {
                    UpdateStatus("Status.Stopped");
                }
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
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