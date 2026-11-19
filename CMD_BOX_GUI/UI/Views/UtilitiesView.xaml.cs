using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Services;
using Microsoft.Win32;

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
            if (PnlUtilAuto == null || PnlUtilHardware == null) return;

            PnlUtilAuto.Visibility = TabUtilAuto.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlUtilHardware.Visibility = TabUtilHardware.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetRunning(bool running, string statusText)
        {
            PbUtilities.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtUtilStatus.Text = statusText;
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

        private async void BtnExportBatteryHtml_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Lưu file báo cáo pin (Battery Report)",
                Filter = "HTML Report (*.html)|*.html",
                FileName = $"battery-report-{DateTime.Now:yyyyMMdd-HHmm}.html",
                DefaultExt = ".html"
            };

            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.FileName)) return;

            BtnExportBatteryHtml.IsEnabled = false;
            BtnOpenBatteryHtml.IsEnabled = false;
            SetRunning(true, "Đang xuất file báo cáo pin...");
            try
            {
                bool ok = await _utility.ExportBatteryReportHtmlAsync(dlg.FileName);
                if (ok)
                {
                    SetRunning(false, $"Đã xuất báo cáo pin: {Path.GetFileName(dlg.FileName)}");
                    string? folder = Path.GetDirectoryName(dlg.FileName);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{dlg.FileName}\"", UseShellExecute = true });
                    }
                }
                else
                {
                    SetRunning(false, "Xuất báo cáo pin thất bại (máy không hỗ trợ pin hoặc lỗi powercfg).");
                }
            }
            finally
            {
                BtnExportBatteryHtml.IsEnabled = true;
                BtnOpenBatteryHtml.IsEnabled = true;
            }
        }

        private async void BtnOpenBatteryHtml_Click(object sender, RoutedEventArgs e)
        {
            BtnExportBatteryHtml.IsEnabled = false;
            BtnOpenBatteryHtml.IsEnabled = false;
            SetRunning(true, "Đang trích xuất và mở báo cáo pin trên web...");
            try
            {
                await _utility.OpenBatteryReportHtmlAsync();
                SetRunning(false, "Đã mở báo cáo pin trên trình duyệt.");
            }
            finally
            {
                BtnExportBatteryHtml.IsEnabled = true;
                BtnOpenBatteryHtml.IsEnabled = true;
            }
        }
    }
}
