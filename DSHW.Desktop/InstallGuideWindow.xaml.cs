using DSHW.Desktop.Core;
using DSHW.Desktop.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace DSHW.Desktop
{
    public sealed partial class InstallGuideWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        /// <summary>安装成功（重新检测到 dsh）后回调，供宿主重试启动。</summary>
        public event Action? OnInstallSucceeded;

        private readonly DshDetector _detector = new();
        private CancellationTokenSource? _installCts;
        private bool _installing;

        // 选择器（避免引用 XAML forward 未定义字段编译报错——由 InitializeComponent 生成）
        private string? _installTargetVersion;

        public InstallGuideWindow(string? latestVersion)
        {
            this.InitializeComponent();
            this.Title = ResourceHelper.GetString("Install.Title", "DSH not installed");

            _installTargetVersion = string.IsNullOrWhiteSpace(latestVersion) ? null : latestVersion.Trim();

            TitleText.Text = ResourceHelper.GetString("Install.Header", "DSH is not installed");
            DescText.Text = ResourceHelper.GetString("Install.Desc",
                    "Install @deepseek-ai/dsh to continue. Copy the command to run it yourself, or install right here.")
                + "\n" + ResourceHelper.GetString("Install.BackupNote",
                    "⚠ Before updating, your ~/.dsh folder is backed up automatically.");

            CopyBtn.Content = ResourceHelper.GetString("Install.Copy", "Copy command");
            InstallBtn.Content = ResourceHelper.GetString("Install.InstallNow", "Install now");
            RecheckBtn.Content = ResourceHelper.GetString("Install.Recheck", "Re-check after install");
            CloseBtn.Content = ResourceHelper.GetString("Dialog.Close", "Close");

            this.Closed += OnWindowClosing;
            LayoutWindow();
            UpdateCommandPreview();
        }

        private void LayoutWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow == null) return;

            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            const int w100 = 640, h100 = 520;
            uint dpi = GetDpiForWindow(hwnd);
            float scale = dpi > 0 ? dpi / 96f : 1f;
            int w = (int)(w100 * scale), h = (int)(h100 * scale);
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32
            {
                X = workArea.X + (workArea.Width - w) / 2,
                Y = workArea.Y + (workArea.Height - h) / 2,
                Width = w,
                Height = h
            });

            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico");
            if (System.IO.File.Exists(iconPath)) appWindow.SetIcon(iconPath);
        }

        private string BuildCommand()
        {
            return Installer.BuildCommand(_installTargetVersion,
                MirrorCheck.IsChecked == true ? MirrorInput.Text.Trim() : null,
                VerboseSwitch.IsOn);
        }

        private void UpdateCommandPreview()
        {
            CommandBox.Text = BuildCommand();
            MirrorInput.Visibility = MirrorCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnMirrorToggled(object sender, RoutedEventArgs e) => UpdateCommandPreview();

        private void OnVerboseToggled(object sender, RoutedEventArgs e) => UpdateCommandPreview();

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var data = new DataPackage();
            data.SetText(CommandBox.Text);
            Clipboard.SetContent(data);
        }

        private async void OnInstallClick(object sender, RoutedEventArgs e)
        {
            if (_installing) return;
            _installing = true;
            SetBusy(true);
            _installCts = new CancellationTokenSource();

            // 安装/更新前先备份整个 ~/.dsh（格式可能随版本变化，如 .credentials.yaml），
            // 再用最全日志（silly）跑 install 并流式记录。
            var (ok, backupPath, result) = await Updater.RunUpdateAsync(
                _installTargetVersion,
                MirrorCheck.IsChecked == true ? MirrorInput.Text.Trim() : null,
                LogLine,
                _installCts.Token);

            if (ok)
            {
                LogLine("=== " + ResourceHelper.GetString("Install.Succeeded", "Install finished") + " ===");
                await RecheckAsync();
            }
            else
            {
                LogLine("=== " + ResourceHelper.GetString("Install.Failed", "Install failed or cancelled") + " ===");
            }
            _installing = false;
            SetBusy(false);
        }

        private async void OnRecheckClick(object sender, RoutedEventArgs e) => await RecheckAsync();

        private async Task RecheckAsync()
        {
            LogLine("--- " + ResourceHelper.GetString("Install.Checking", "Checking DSH installation...") + " ---");
            await _detector.RefreshAsync(checkLatest: false);
            if (_detector.IsInstalled)
            {
                LogLine("DSH " + _detector.InstalledVersion + " " + ResourceHelper.GetString("Install.Detected", "detected"));
                OnInstallSucceeded?.Invoke();
            }
            else
            {
                LogLine(ResourceHelper.GetString("Install.StillMissing", "DSH still not detected"));
            }
        }

        private void LogLine(string line)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LogBox.Text += (LogBox.Text.Length == 0 ? "" : "\n") + line;
                LogScroll.ChangeView(null, LogScroll.ScrollableHeight, null);
            });
        }

        private void SetBusy(bool busy)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                InstallBtn.IsEnabled = !busy;
                CopyBtn.IsEnabled = !busy;
                RecheckBtn.IsEnabled = !busy;
                MirrorCheck.IsEnabled = !busy;
                MirrorInput.IsEnabled = !busy;
                VerboseSwitch.IsEnabled = !busy;
            });
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void OnWindowClosing(object sender, WindowEventArgs args)
        {
            _installCts?.Cancel();
        }
    }
}
