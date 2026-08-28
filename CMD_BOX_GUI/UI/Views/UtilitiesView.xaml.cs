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
                Logger.Success($"Tọa độ: ({p.X}, {p.Y})");
            }
            BtnGetCurrentMousePos.Content = "Lấy vị trí chuột (Sau 3s)";
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

        private async void BtnInstallChrome_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Google Chrome", "Google.Chrome");
        }

        private async void BtnInstallVSCode_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Visual Studio Code", "Microsoft.VisualStudioCode");
        }

        private async void BtnInstallGit_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("Git", "Git.Git");
        }

        private async void BtnInstall7Zip_Click(object sender, RoutedEventArgs e)
        {
            await _utility.InstallQuickAppAsync("7-Zip", "7zip.7zip");
        }
    }
}
