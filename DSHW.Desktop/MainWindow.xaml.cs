using DSHW.Desktop.Core;
using DSHW.Desktop.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Reflection;
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
        private bool _isExiting = false;              // 真退出（托盘"退出"），关闭按钮不拦截
        private int _navigationRetries = 0;
        private const int MaxNavigationRetries = 3;

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
                AppWindowRef = appWindow;

                // 默认尺寸：主显示器工作区 85%（适配 1080p 笔记本 / 4K 副屏），最小 1100x700
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                var workArea = displayArea.WorkArea;
                int width = Math.Max(1100, (int)(workArea.Width * 0.85));
                int height = Math.Max(700, (int)(workArea.Height * 0.85));
                appWindow.MoveAndResize(new RectInt32
                {
                    X = workArea.X + (workArea.Width - width) / 2,
                    Y = workArea.Y + (workArea.Height - height) / 2,
                    Width = width,
                    Height = height
                });

                // 窗口图标（任务栏 / Alt-Tab / 窗口菜单），与 exe 图标保持一致
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }

                // 标题栏按钮显式配色（浅色栏 + 深色按钮字，聚焦时可见）
                // 参考：https://learn.microsoft.com/windows/apps/develop/title-bar
                var titleBar = appWindow.TitleBar;
                titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0x1A, 0x1A, 0x1A);
                titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 0x00, 0x00, 0x00);
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 0xE5, 0xE5, 0xE5);
                titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 0x00, 0x00, 0x00);
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 0xD0, 0xD0, 0xD0);
                titleBar.InactiveForegroundColor = Windows.UI.Color.FromArgb(255, 0x99, 0x99, 0x99);

                // 标题栏高度与自定义 48px 栏匹配（Win11 生效；Win10 回退为标准高度）
                titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

                // 关闭按钮（X）→ 隐藏到托盘；托盘"退出"时置 _isExiting 真退出
                appWindow.Closing += (s, e) =>
                {
                    if (!_isExiting)
                    {
                        e.Cancel = true;
                        appWindow.Hide();
                    }
                };
            }

            this.Closed += OnWindowClosed;

            // 壳版本（程序集）；DSH 版本在 SetRunner 后再查（需要 Runner 就绪）
            VersionText.Text = $"DSHW {GetShellVersion()} · DSH --";
        }

        public AppWindow? AppWindowRef { get; private set; }

        /// <summary>供托盘"显示窗口"调用。</summary>
        public void ShowWindow()
        {
            AppWindowRef?.Show();
            Activate();
        }

        /// <summary>供托盘"隐藏窗口"调用。</summary>
        public void HideWindow()
        {
            AppWindowRef?.Hide();
        }

        /// <summary>供托盘"退出"调用：允许关闭并触发清理。</summary>
        public void RequestExit()
        {
            _isExiting = true;
            _runner?.Dispose();             // 杀 DSH 进程树，确保 3080 释放
            AppWindowRef?.Destroy();        // 关闭窗口
            Application.Current.Exit();     // 退出应用（清理托盘图标）
        }

        public void SetRunner(Runner runner)
        {
            _runner = runner;
            _runner.OnStatusChanged += OnStatusChanged;
            _ = LoadDshVersionAsync();
            _ = InitializeWebView();
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

        private async Task LoadDshVersionAsync()
        {
            var shellVersion = GetShellVersion();
            var dshVersion = _runner != null ? await _runner.GetDshVersionAsync() : null;
            DispatcherQueue.TryEnqueue(() =>
            {
                VersionText.Text = $"DSHW {shellVersion} · DSH {(string.IsNullOrEmpty(dshVersion) ? "--" : dshVersion)}";
            });
        }

        // 统一的更新状态方法：标题栏短状态 + 状态栏详细消息（超长省略，悬停显示全文）
        private void UpdateStatus(string resourceKey)
        {
            var text = GetResourceString(resourceKey);
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = text;
                DetailStatusText.Text = text;
                ToolTipService.SetToolTip(DetailStatusText, text);
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
                DetailStatusText.Text = text;
                ToolTipService.SetToolTip(DetailStatusText, text);
            });
        }

        // 状态栏显示自由文本（不经过本地化）
        private void UpdateDetailStatus(string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DetailStatusText.Text = text;
                ToolTipService.SetToolTip(DetailStatusText, text);
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
                // WebView2 用户数据目录已由 Startup.cs 设为 %LOCALAPPDATA%\DSHW\WebView2，
                // 避免默认落在 exe 旁产生 "*.WebView2" 文件夹污染发布目录
                await WebView.EnsureCoreWebView2Async();
                WebView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    _isWebViewReady = true;
                    if (_hasNavigatedToWebUi)
                    {
                        if (e.IsSuccess)
                        {
                            _uiLoadedSuccessfully = true;
                            UpdateStatus("Status.Ready");
                        }
                        else if (!_uiLoadedSuccessfully)
                        {
                            // 首次连接失败（服务未就绪）→ 自动重试
                            _ = RetryNavigationAsync();
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

        private async Task RetryNavigationAsync()
        {
            if (_uiLoadedSuccessfully || _runner == null || _navigationRetries >= MaxNavigationRetries) return;
            _navigationRetries++;
            UpdateStatus("Status.Waiting");
            await Task.Delay(2000);
            if (!_uiLoadedSuccessfully && _runner.IsRunning)
            {
                _hasNavigatedToWebUi = true;
                WebView.Source = new Uri(_runner.WebUIUrl);
            }
            else if (!_uiLoadedSuccessfully)
            {
                UpdateStatus("Status.Stopped");
            }
        }

        private async Task LoadWebUI()
        {
            if (_runner == null) return;
            UpdateStatus("Status.Waiting");

            // 等 DSH 服务在端口上就绪（最多 60s；复用现有服务时立即返回）
            var serviceReady = await _runner.WaitForServiceAsync(TimeSpan.FromSeconds(60));

            if (serviceReady)
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
