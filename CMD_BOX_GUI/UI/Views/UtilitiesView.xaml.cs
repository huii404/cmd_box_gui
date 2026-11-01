using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Services;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class UtilitiesView : UserControl
    {
        private readonly UtilityService _utility = new();

        public UtilitiesView()
        {
            InitializeComponent();
        }

        // ================= SUB-TAB SWITCHING =================
        private void TabUtil_Click(object sender, RoutedEventArgs e)
        {
            if (PnlUtilApps == null || PnlUtilAuto == null || PnlUtilHardware == null) return;

            PnlUtilApps.Visibility = TabUtilApps.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlUtilAuto.Visibility = TabUtilAuto.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlUtilHardware.Visibility = TabUtilHardware.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetRunning(bool running, string statusText)
        {
            PbUtilities.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtUtilStatus.Text = statusText;
        }

        private async Task InstallAppAsync(string name, string id)
        {
            SetRunning(true, $"Đang cài đặt [{name}] qua Winget...");
            await _utility.InstallQuickAppAsync(name, id);
            SetRunning(false, $"Tác vụ cài đặt [{name}] hoàn tất.");
        }

        private async void BtnGetCurrentMousePos_Click(object sender, RoutedEventArgs e)
        {
            BtnGetCurrentMousePos.IsEnabled = false;
            for (int i = 3; i > 0; i--)
            {
                BtnGetCurrentMousePos.Content = $"Lấy vị trí trong {i}s...";
                await Task.Delay(1000);
            }

            if (NativeMethods.GetCursorPos(out var p))
            {
                TxtClickX.Text = p.X.ToString();
                TxtClickY.Text = p.Y.ToString();
                Logger.Success($"[AutoClick] Tọa độ đã lưu: ({p.X}, {p.Y})");
            }
            BtnGetCurrentMousePos.Content = "📍 Lấy Tọa Độ (3s)";
            BtnGetCurrentMousePos.IsEnabled = true;
        }

        private async void BtnStartAutoClick_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtClickX.Text, out int x) || !int.TryParse(TxtClickY.Text, out int y)) return;
            if (!int.TryParse(TxtClickCount.Text, out int count) || count <= 0) count = 100;
            if (!int.TryParse(TxtClickDelay.Text, out int delay) || delay < 0) delay = 100;

            BtnStartAutoClick.IsEnabled = false;
            try { await _utility.StartAutoClickAsync(x, y, count, delay); }
            finally { BtnStartAutoClick.IsEnabled = true; }
        }

        private void BtnStopAutoClick_Click(object sender, RoutedEventArgs e)
        {
            _utility.StopAutoClick();
            BtnStartAutoClick.IsEnabled = true;
        }

        private async void BtnStartSpam_Click(object sender, RoutedEventArgs e)
        {
            string content = TxtSpamContent.Text;
            if (string.IsNullOrEmpty(content)) return;
            if (!int.TryParse(TxtSpamCount.Text, out int count) || count <= 0) count = 10;
            if (!int.TryParse(TxtSpamDelay.Text, out int delay) || delay < 0) delay = 200;

            BtnStartSpam.IsEnabled = false;
            try { await _utility.SpamTextAsync(content, count, delay); }
            finally { BtnStartSpam.IsEnabled = true; }
        }

        private async void BtnAutoPasteLines_Click(object sender, RoutedEventArgs e)
        {
            string content = TxtSpamContent.Text;
            if (string.IsNullOrEmpty(content)) return;

            BtnAutoPasteLines.IsEnabled = false;
            try { await _utility.AutoPasteMultiLinesAsync(content, 250); }
            finally { BtnAutoPasteLines.IsEnabled = true; }
        }

        private async void BtnOpenBatteryHtml_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang trích xuất báo cáo pin (batteryreport)...");
            await _utility.OpenBatteryReportHtmlAsync();
            SetRunning(false, "Đã mở báo cáo pin trên trình duyệt.");
        }

        // ================= 1. TRÌNH DUYỆT & GIAO TIẾP =================
        private async void BtnInstallChrome_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Google Chrome", "Google.Chrome");
        }

        private async void BtnInstallBrave_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Brave Browser", "Brave.Brave");
        }

        private async void BtnInstallFirefox_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Mozilla Firefox", "Mozilla.Firefox");
        }

        private async void BtnInstallZalo_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Zalo PC", "VNG.Zalo");
        }

        private async void BtnInstallTelegram_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Telegram Desktop", "Telegram.TelegramDesktop");
        }

        private async void BtnInstallDiscord_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Discord", "Discord.Discord");
        }

        // ================= 2. LẬP TRÌNH & DEV TOOLS =================
        private async void BtnInstallVSCode_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Visual Studio Code", "Microsoft.VisualStudioCode");
        }

        private async void BtnInstallGit_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Git", "Git.Git");
        }

        private async void BtnInstallNode_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Node.js (LTS)", "OpenJS.NodeJS.LTS");
        }

        private async void BtnInstallPython_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Python 3", "Python.Python.3.12");
        }

        private async void BtnInstallNotepadPlusPlus_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Notepad++", "Notepad++.Notepad++");
        }

        private async void BtnInstallPostman_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Postman", "Postman.Postman");
        }

        private async void BtnInstallDocker_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Docker Desktop", "Docker.DockerDesktop");
        }

        // ================= 3. TIỆN ÍCH & HỆ THỐNG =================
        private async void BtnInstall7Zip_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("7-Zip", "7zip.7zip");
        }

        private async void BtnInstallWinRAR_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("WinRAR", "RARLab.WinRAR");
        }

        private async void BtnInstallUniKey_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("UniKey", "PhamKimLong.UniKey");
        }

        private async void BtnInstallEverything_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Everything Search", "voidtools.Everything");
        }

        private async void BtnInstallRevo_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Revo Uninstaller", "RevoUninstaller.RevoUninstaller");
        }

        // ================= 4. GIẢI TRÍ & ĐỒ HỌA / STREAM =================
        private async void BtnInstallVLC_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("VLC Media Player", "VideoLAN.VLC");
        }

        private async void BtnInstallSpotify_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("Spotify", "Spotify.Spotify");
        }

        private async void BtnInstallOBS_Click(object sender, RoutedEventArgs e)
        {
            await InstallAppAsync("OBS Studio", "OBSProject.OBSStudio");
        }
    }
}
