using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Services;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly OptimizerService _optimizer = new();
        private readonly NetworkService _network = new();
        private readonly UtilityService _utility = new();

        public DashboardView()
        {
            InitializeComponent();
            Loaded += async (_, _) => await RefreshDashboardDataAsync();
        }

        public async Task RefreshDashboardDataAsync()
        {
            bool isAdmin = SystemCore.IsAdministrator();
            TxtAdminStatus.Text = isAdmin ? "Administrator" : "Standard User";
            TxtAdminStatus.Foreground = isAdmin ? (System.Windows.Media.Brush)FindResource("AccentSuccess") : (System.Windows.Media.Brush)FindResource("AccentWarning");
            BtnElevateAdmin.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;

            await Task.Run(() =>
            {
                try
                {
                    var drive = new DriveInfo("C");
                    long free = drive.AvailableFreeSpace;
                    long total = drive.TotalSize;
                    long used = total - free;
                    double percentUsed = (double)used / total * 100.0;

                    Dispatcher.Invoke(() =>
                    {
                        TxtDriveSpace.Text = $"{SystemCore.FormatBytes(free)} trống";
                        PbDriveSpace.Value = percentUsed;
                        TxtDriveDetail.Text = $"{SystemCore.FormatBytes(used)} / {SystemCore.FormatBytes(total)} ({percentUsed:0.#}%)";
                    });
                }
                catch { }
            });

            if (NativeMethods.GetSystemPowerStatus(out var sps))
            {
                if (sps.BatteryFlag == 128)
                {
                    TxtPowerStatus.Text = "PC (AC Online)";
                    PbBattery.Value = 100;
                    TxtBatteryDetail.Text = "Nguồn trực tiếp";
                }
                else
                {
                    int pct = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 0;
                    bool charging = (sps.BatteryFlag & 8) != 0;
                    TxtPowerStatus.Text = charging ? $"Sạc ({pct}%)" : $"{pct}% (Pin)";
                    PbBattery.Value = pct;
                    TxtBatteryDetail.Text = sps.ACLineStatus == 1 ? "Đang cắm sạc" : "Đang dùng pin";
                }
            }
        }

        private void BtnElevateAdmin_Click(object sender, RoutedEventArgs e)
        {
            SystemCore.RestartAsAdmin();
        }

        private async void BtnQuickClean_Click(object sender, RoutedEventArgs e)
        {
            SetActionRunning(true, "Đang dọn rác nhanh...");
            var progress = new Progress<int>(v => PbActionProgress.Value = v);
            long freed = await _optimizer.CleanQuickAsync(progress);
            await RefreshDashboardDataAsync();
            SetActionRunning(false, $"Đã giải phóng {SystemCore.FormatBytes(freed)}.");
        }

        private async void BtnCleanPro_Click(object sender, RoutedEventArgs e)
        {
            SetActionRunning(true, "Đang dọn rác PRO (DISM, Prefetch, WinSxS)...");
            var progress = new Progress<int>(v => PbActionProgress.Value = v);
            long freed = await _optimizer.CleanDiskProAsync(progress);
            await RefreshDashboardDataAsync();
            SetActionRunning(false, $"Đã giải phóng {SystemCore.FormatBytes(freed)}.");
        }

        private async void BtnRepairNetwork_Click(object sender, RoutedEventArgs e)
        {
            SetActionRunning(true, "Đang khôi phục mạng...");
            var progress = new Progress<int>(v => PbActionProgress.Value = v);
            await _network.RepairNetworkProAsync(progress);
            SetActionRunning(false, "Khôi phục mạng xong!");
        }

        private async void BtnOptimizeTaskbar_Click(object sender, RoutedEventArgs e)
        {
            SetActionRunning(true, "Đang tối ưu Taskbar W11...");
            await _optimizer.OptimizeTaskbarWindows11Async();
            SetActionRunning(false, "Đã tối ưu Taskbar!");
        }

        private async void BtnOptimizeServices_Click(object sender, RoutedEventArgs e)
        {
            SetActionRunning(true, "Đang tắt dịch vụ ngầm...");
            await _optimizer.OptimizeServicesAsync();
            SetActionRunning(false, "Đã tắt các dịch vụ ngầm!");
        }

        private async void BtnBatteryReport_Click(object sender, RoutedEventArgs e)
        {
            SetActionRunning(true, "Đang tạo báo cáo Pin...");
            await _utility.OpenBatteryReportHtmlAsync();
            SetActionRunning(false, "Đã mở báo cáo Pin.");
        }

        private void SetActionRunning(bool running, string statusText)
        {
            PbActionProgress.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtActionStatus.Visibility = Visibility.Visible;
            TxtActionStatus.Text = statusText;
            BtnQuickClean.IsEnabled = !running;
            BtnCleanPro.IsEnabled = !running;
            BtnRepairNetwork.IsEnabled = !running;
            BtnOptimizeTaskbar.IsEnabled = !running;
            BtnOptimizeServices.IsEnabled = !running;
            BtnBatteryReport.IsEnabled = !running;
        }
    }
}
