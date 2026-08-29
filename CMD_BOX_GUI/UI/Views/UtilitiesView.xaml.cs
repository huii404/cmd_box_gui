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
            await _utility.OpenBatteryReportHtmlAsync();
        }

        private async void BtnUninstallBloatware_Click(object sender, RoutedEventArgs e)
        {
            BtnUninstallBloatware.IsEnabled = false;
            try { await _utility.UninstallBloatwareAsync(); }
            finally { BtnUninstallBloatware.IsEnabled = true; }
        }

        // ================= 1. TRÌNH DUYỆT & GIAO TIẾP =================
        private async void BtnInstallChrome_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Google Chrome", "Google.Chrome");
        }

        private async void BtnInstallBrave_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Brave Browser", "Brave.Brave");
        }

        private async void BtnInstallFirefox_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Mozilla Firefox", "Mozilla.Firefox");
        }

        private async void BtnInstallZalo_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Zalo PC", "VNG.Zalo");
        }

        private async void BtnInstallTelegram_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Telegram Desktop", "Telegram.TelegramDesktop");
        }

        private async void BtnInstallDiscord_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Discord", "Discord.Discord");
        }

        // ================= 2. LẬP TRÌNH & DEV TOOLS =================
        private async void BtnInstallVSCode_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Visual Studio Code", "Microsoft.VisualStudioCode");
        }

        private async void BtnInstallGit_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Git", "Git.Git");
        }

        private async void BtnInstallNode_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Node.js (LTS)", "OpenJS.NodeJS.LTS");
        }

        private async void BtnInstallPython_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Python 3", "Python.Python.3.12");
        }

        private async void BtnInstallNotepadPlusPlus_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Notepad++", "Notepad++.Notepad++");
        }

        private async void BtnInstallPostman_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Postman", "Postman.Postman");
        }

        private async void BtnInstallDocker_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Docker Desktop", "Docker.DockerDesktop");
        }

        // ================= 3. TIỆN ÍCH & HỆ THỐNG =================
        private async void BtnInstall7Zip_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("7-Zip", "7zip.7zip");
        }

        private async void BtnInstallWinRAR_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("WinRAR", "RARLab.WinRAR");
        }

        private async void BtnInstallUniKey_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("UniKey", "PhamKimLong.UniKey");
        }

        private async void BtnInstallEverything_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Everything Search", "voidtools.Everything");
        }

        private async void BtnInstallRevo_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Revo Uninstaller", "RevoUninstaller.RevoUninstaller");
        }

        // ================= 4. GIẢI TRÍ & ĐỒ HỌA / STREAM =================
        private async void BtnInstallVLC_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("VLC Media Player", "VideoLAN.VLC");
        }

        private async void BtnInstallSpotify_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Spotify", "Spotify.Spotify");
        }

        private async void BtnInstallOBS_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("OBS Studio", "OBSProject.OBSStudio");
        }
    }
}
