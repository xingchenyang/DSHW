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
            VersionText.Content = $"DSHW {GetShellVersion()} · DSH --";
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
            _runner.OnDshNotInstalled += OnDshNotInstalled;
            _ = LoadDshVersionAsync();
            _ = InitializeWebView();
        }

        private InstallGuideWindow? _installGuide;
        private void OnDshNotInstalled(object? sender, string? latestVersion)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_installGuide != null)
                {
                    _installGuide.Activate();
                    return;
                }
                _installGuide = new InstallGuideWindow(latestVersion);
                _installGuide.OnInstallSucceeded += () =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _ = _runner?.RunAsync(); // 装好后再尝试启动
                        _installGuide?.Close();
                    });
                };
                _installGuide.Closed += (s, e) => _installGuide = null;
                _installGuide.Activate();
            });
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

        /// <summary>联网查最新版并刷新版本按钮。网络失败会静默（latest=null），不打扰。</summary>
        private async Task LoadDshVersionAsync()
        {
            var shellVersion = GetShellVersion();
            if (_runner != null)
            {
                await _runner.RefreshDshVersionAsync(checkLatest: true);
            }
            DispatcherQueue.TryEnqueue(() => ApplyVersionLabel(shellVersion));
        }

        private void ApplyVersionLabel(string shellVersion)
        {
            if (_runner == null)
            {
                VersionText.Content = $"DSHW {shellVersion} · DSH --";
                VersionText.IsEnabled = false;
                return;
            }

            var installed = _runner.InstalledVersion;
            var latest = _runner.LatestVersion;
            bool hasUpdate = !string.IsNullOrEmpty(installed)
                             && !string.IsNullOrEmpty(latest)
                             && !string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase);

            if (hasUpdate)
            {
                VersionText.Content = $"DSHW {shellVersion} · DSH {installed} → {latest}";
            }
            else
            {
                VersionText.Content = $"DSHW {shellVersion} · DSH {(string.IsNullOrEmpty(installed) ? "--" : installed)}";
            }
            // 版本按钮始终可点：无论是否检测到更新，都能打开版本/更新信息对话框。
            // （不再把 IsEnabled 绑在"恰好这次联网查到差异"上，避免"偶尔能点开、关了再也点不开"。）
            VersionText.IsEnabled = true;
            VersionText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                hasUpdate ? Windows.UI.Color.FromArgb(255, 0xC5, 0x00, 0x00)
                          : Windows.UI.Color.FromArgb(255, 0x99, 0x99, 0x99));
        }

        private ContentDialog? _updateDialog;
        private TextBlock? _dialogInfoBlock; // 版本信息行，供重检/安装后原地更新（避免整树重建弄丢提示）
        private bool _verboseLogs; // 版本更新命令是否用完整日志（verbose）
        private bool _useMirror;   // 版本更新命令是否用镜像
        private string _mirrorUrl = _MirrorDefault; // 版本更新命令的镜像地址
        private const string _MirrorDefault = "https://registry.npmmirror.com";
        // 版本/更新对话框重入护栏：一次只允许一个开关流程在跑，连点也不崩
        private bool _dialogBusy;

        private async void OnVersionClick(object sender, RoutedEventArgs e)
        {
            if (_dialogBusy) return;
            _dialogBusy = true;
            try
            {
                // 不阻塞 UI：先用当前已知版本立刻弹窗，网络刷新在后台跑，完成后再原地更新内容
                await ShowVersionDialogAsync();
                if (_updateDialog == null) return; // 已关闭
                if (_runner != null)
                {
                    await _runner.RefreshDshVersionAsync(checkLatest: true);
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    ApplyVersionLabel(GetShellVersion());
                    RefreshDialogContentInPlace();
                });
            }
            finally
            {
                _dialogBusy = false;
            }
        }

        /// <summary>版本/更新信息对话框：有更新则显示升级命令（可复制），无更新则显示"已是最新"。</summary>
        private async Task ShowVersionDialogAsync()
        {
            var installed = _runner?.InstalledVersion;
            var latest = _runner?.LatestVersion;
            bool hasUpdate = !string.IsNullOrEmpty(installed)
                             && !string.IsNullOrEmpty(latest)
                             && !string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase);
            var dialog = new ContentDialog
            {
                Title = hasUpdate
                    ? ResourceHelper.GetString("Update.Title", "DSH update available")
                    : ResourceHelper.GetString("Update.UpToDateTitle", "DSH"),
                PrimaryButtonText = ResourceHelper.GetString("Dialog.Close", "Close"),
                XamlRoot = this.Content.XamlRoot,
                Content = BuildDialogContent()
            };
            dialog.Closed += (s2, e2) => { _updateDialog = null; };
            _updateDialog = dialog;
            await dialog.ShowAsync();
        }

        /// <summary>不动对话框对象、原地重建其内容与标题（避免 Hide+Show 重建导致的重入崩溃）。</summary>
        private void RefreshDialogContentInPlace()
        {
            try
            {
                if (_updateDialog == null) return;
                var installed = _runner?.InstalledVersion;
                var latest = _runner?.LatestVersion;
                bool hasUpdate = !string.IsNullOrEmpty(installed)
                                 && !string.IsNullOrEmpty(latest)
                                 && !string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase);
                _updateDialog.Title = hasUpdate
                    ? ResourceHelper.GetString("Update.Title", "DSH update available")
                    : ResourceHelper.GetString("Update.UpToDateTitle", "DSH");
                _updateDialog.Content = BuildDialogContent();
            }
            catch { }
        }

        /// <summary>重检/安装完成后，仅更新标题与版本信息行（不整树重建，避免弄丢正在显示的行内提示）。</summary>
        private void TickUpToDateInPlace()
        {
            try
            {
                if (_updateDialog == null) return;
                var installed = _runner?.InstalledVersion;
                var latest = _runner?.LatestVersion;
                bool hasUpdate = !string.IsNullOrEmpty(installed)
                                 && !string.IsNullOrEmpty(latest)
                                 && !string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase);
                _updateDialog.Title = hasUpdate
                    ? ResourceHelper.GetString("Update.Title", "DSH update available")
                    : ResourceHelper.GetString("Update.UpToDateTitle", "DSH");
                if (_dialogInfoBlock != null && _dialogInfoBlock.Parent != null)
                {
                    _dialogInfoBlock.Text = hasUpdate
                        ? $"DSH {installed} → {latest}"
                        : string.Format(ResourceHelper.GetString("Update.UpToDate", "DSH {0} is up to date"), installed ?? "--");
                }
            }
            catch { }
        }

        /// <summary>构建当前版本的对话框内容（每次调用生成新的 StackPanel 与控件）。</summary>
        private StackPanel BuildDialogContent()
        {
            var installed = _runner?.InstalledVersion;
            var latest = _runner?.LatestVersion;
            bool hasUpdate = !string.IsNullOrEmpty(installed)
                             && !string.IsNullOrEmpty(latest)
                             && !string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase);

            var stack = new StackPanel { Spacing = 10 };

            var info = new TextBlock
            {
                Text = hasUpdate
                    ? $"DSH {installed} → {latest}"
                    : string.Format(ResourceHelper.GetString("Update.UpToDate", "DSH {0} is up to date"), installed ?? "--"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(info);
            _dialogInfoBlock = info; // 供重检/安装后原地更新，避免整树重建弄丢提示

            if (hasUpdate)
            {
                // 升级前自动备份 ~/.dsh（格式可能随版本变化），并在界面提示用户
                var backupNote = new TextBlock
                {
                    Text = ResourceHelper.GetString("Update.BackupNote",
                        "⚠ Updating may change your ~/.dsh config format. Your ~/.dsh is backed up automatically before install."),
                    FontSize = 11,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00)),
                    TextWrapping = TextWrapping.Wrap
                };
                stack.Children.Add(backupNote);

                string BuildCmd() => Installer.BuildCommand(
                    latest,
                    _useMirror ? (string.IsNullOrWhiteSpace(_mirrorUrl) ? _MirrorDefault : _mirrorUrl.Trim()) : null,
                    _verboseLogs);
                var box = new TextBox
                {
                    Text = BuildCmd(),
                    IsReadOnly = true,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MinHeight = 48
                };
                stack.Children.Add(box);

                // 选项行：journaux (verbose) / utiliser le miroir 并排
                var optionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
                var verboseToggle = new ToggleSwitch
                {
                    Header = ResourceHelper.GetString("Update.Verbose", "Full logs (verbose)"),
                    OffContent = "",
                    OnContent = ""
                };
                verboseToggle.Toggled += (s2, e2) =>
                {
                    _verboseLogs = verboseToggle.IsOn;
                    box.Text = BuildCmd();
                };
                optionsRow.Children.Add(verboseToggle);

                // 镜像开关：切换/编辑时重建命令
                var mirrorToggle = new ToggleSwitch
                {
                    Header = ResourceHelper.GetString("Update.Mirror", "Use mirror"),
                    OffContent = "",
                    OnContent = ""
                };
                var mirrorInput = new TextBox
                {
                    Text = _mirrorUrl, FontSize = 12,
                    Visibility = Visibility.Collapsed,
                    PlaceholderText = "https://registry.npmmirror.com"
                };
                mirrorToggle.Toggled += (s2, e2) =>
                {
                    _useMirror = mirrorToggle.IsOn;
                    mirrorInput.Visibility = _useMirror ? Visibility.Visible : Visibility.Collapsed;
                    box.Text = BuildCmd();
                };
                optionsRow.Children.Add(mirrorToggle);
                stack.Children.Add(optionsRow);
                mirrorInput.TextChanged += (s2, tx) =>
                {
                    _useMirror = mirrorToggle.IsOn;
                    _mirrorUrl = mirrorInput.Text;
                    box.Text = BuildCmd();
                };
                stack.Children.Add(mirrorInput);

                var copyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var copyBtn = new Button { Content = ResourceHelper.GetString("Update.Copy", "Copy command") };
                copyBtn.Click += (s2, e2) =>
                {
                    if (ThrottleClick(800)) return; // 防连点
                    var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    data.SetText(box.Text);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
                    ShowTransientTooltip(copyRow, copyBtn, ResourceHelper.GetString("Update.Copied", "Copied ✓"));
                };
                copyRow.Children.Add(copyBtn);
                stack.Children.Add(copyRow);

                // App 内安装：先备份 ~/.dsh，再用 --loglevel=silly 最全日志流式安装，结果实时回显到 logBox
                // 会拉起 npm/cmd 进程，单独放一段（前面加分隔线）
                stack.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Height = 1,
                    Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0xE0, 0xE0)),
                    Margin = new Thickness(0, 4, 0, 4)
                });
                var installRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var installBtn = new Button
                {
                    Content = ResourceHelper.GetString("Update.InstallNow", "Install now (in-app)")
                };
                var logBox = new TextBox
                {
                    IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11,
                    MinHeight = 120, MaxHeight = 180, AcceptsReturn = true,
                    Visibility = Visibility.Collapsed
                };
                installBtn.Click += (s2, e2) =>
                {
                    if (ThrottleClick(1000) || !installBtn.IsEnabled) return; // 防连点 + 正在安装
                    installBtn.IsEnabled = false;
                    copyBtn.IsEnabled = false;
                    logBox.Visibility = Visibility.Visible;
                    logBox.Text = "";
                    var ct = new System.Threading.CancellationTokenSource();

                    // 缓冲式日志：后台线程累加 StringBuilder，UI 每约 150ms 合并刷新一次，
                    // 避免 silly 海量日志每行都全量重排 TextBox 而饿死 UI 线程（死机根因）。
                    var sb = new System.Text.StringBuilder();
                    System.Threading.Timer? flushTimer = new System.Threading.Timer(_ =>
                    {
                        lock (sb) { if (sb.Length > 0) { var s = sb.ToString(); sb.Clear(); DispatcherQueue.TryEnqueue(() => { try { logBox.Text += (logBox.Text.Length == 0 ? "" : "\n") + s; } catch { } }); } }
                    }, null, 0, 150);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Updater.RunUpdateAsync(
                                latest,
                                _useMirror ? (string.IsNullOrWhiteSpace(_mirrorUrl) ? _MirrorDefault : _mirrorUrl.Trim()) : null,
                                line => { lock (sb) { sb.AppendLine(line); } },
                                ct.Token);
                        }
                        finally
                        {
                            // 收尾：停计时器并最后一次刷新剩余日志
                            flushTimer?.Dispose();
                            lock (sb)
                            {
                                if (sb.Length > 0)
                                {
                                    var s = sb.ToString();
                                    sb.Clear();
                                    DispatcherQueue.TryEnqueue(() => { try { logBox.Text += (logBox.Text.Length == 0 ? "" : "\n") + s; } catch { } });
                                }
                            }
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                installBtn.IsEnabled = true;
                                copyBtn.IsEnabled = true;
                                if (_runner != null) _ = _runner.RefreshDshVersionAsync(checkLatest: true);
                                DispatcherQueue.TryEnqueue(() => ApplyVersionLabel(GetShellVersion()));
                                TickUpToDateInPlace();
                            });
                        }
                    });
                };
                installRow.Children.Add(installBtn);
                stack.Children.Add(installRow);
                stack.Children.Add(logBox);
            }

            var recheckRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var refreshBtn = new Button { Content = ResourceHelper.GetString("Update.Recheck", "Re-check") };
            refreshBtn.Click += async (s2, e2) =>
            {
                if (ThrottleClick(800)) return; // 防连点
                if (!refreshBtn.IsEnabled) return;
                refreshBtn.IsEnabled = false;
                if (_runner != null)
                {
                    await _runner.RefreshDshVersionAsync(checkLatest: true);
                }
                refreshBtn.IsEnabled = true;
                DispatcherQueue.TryEnqueue(() => ApplyVersionLabel(GetShellVersion()));
                var found = _runner?.LatestVersion;
                var inst = _runner?.InstalledVersion;
                string feedback;
                if (string.IsNullOrEmpty(found))
                {
                    feedback = ResourceHelper.GetString("Update.RecheckFail", "Re-check failed (offline?)");
                }
                else if (!string.IsNullOrEmpty(inst) && !string.Equals(inst, found, StringComparison.OrdinalIgnoreCase))
                {
                    feedback = string.Format(ResourceHelper.GetString("Update.RecheckNew", "Checked — a new version {0} is available"), found);
                }
                else
                {
                    feedback = string.Format(ResourceHelper.GetString("Update.RecheckSame", "Checked — {0} is already the latest"), found);
                }
                // 先更新版本行/标题（不整树重建，保住 tip），再在按钮右侧弹提示
                TickUpToDateInPlace();
                ShowTransientTooltip(recheckRow, refreshBtn, feedback);
            };
            recheckRow.Children.Add(refreshBtn);
            stack.Children.Add(recheckRow);

            return stack;
        }

        /// <summary>
        /// 在 host 容器里 target（按钮）的右侧原位插入一个小提示文字，约 3s 后移除。
        /// 纯 UI 元素（非 ToolTip/TeachingTip）：在 ContentDialog 里 100% 可靠、无 popup 关闭回调、不会闪退。
        /// host 通常是按钮所在的行 StackPanel（Orientation=Horizontal），因此 tip 会出现在按钮右边。
        /// </summary>
        private void ShowTransientTooltip(Panel host, FrameworkElement target, string text)
        {
            if (host == null || target == null) return;
            DispatcherQueue.TryEnqueue(async () =>
            {
                TextBlock? tip = null;
                try
                {
                    tip = new TextBlock
                    {
                        Text = text,
                        FontSize = 11,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x00, 0x72, 0x33)),
                        Margin = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    int idx = host.Children.IndexOf(target);
                    host.Children.Insert(Math.Max(0, idx) + 1, tip);
                    await Task.Delay(3000); // 停留 3s，足够看清消息
                    if (host.Children.Contains(tip))
                    {
                        host.Children.Remove(tip);
                    }
                }
                catch
                {
                    try { if (tip != null && host.Children.Contains(tip)) host.Children.Remove(tip); } catch { }
                }
            });
        }

        // 防连点：上次点击时间戳，小于 minMs 间隔的点击被忽略（copier/reverifier 共用）
        private DateTime _lastToggleClick;

        private bool ThrottleClick(double minMs)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastToggleClick).TotalMilliseconds < minMs) return true; // 忽略
            _lastToggleClick = now;
            return false;
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
