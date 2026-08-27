using System;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Services;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class OptimizerView : UserControl
    {
        private readonly OptimizerService _optimizer = new();

        public OptimizerView()
        {
            InitializeComponent();
        }

        private async void BtnCleanQuick_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang dọn rác nhanh...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanQuickAsync(progress);
            SetRunning(false, $"Dọn nhanh xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanPro_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang dọn rác PRO (DISM, Prefetch, WinSxS)...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanDiskProAsync(progress);
            SetRunning(false, $"Dọn rác PRO xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanDev_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang dọn Dev Cache (NPM/Pip/NuGet/Gradle)...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanDevCachesAsync(progress);
            SetRunning(false, $"Đã dọn Dev Cache! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanBrowser_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang dọn Cache trình duyệt...");
            await _optimizer.ClearBrowserCacheAsync();
            SetRunning(false, "Đã làm sạch Cache trình duyệt!");
        }

        private async void BtnDisableStartup_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang quét và tắt app khởi động thừa...");
            await _optimizer.DisableStartupAppsWithWhitelistAsync();
            SetRunning(false, "Đã tối ưu Startup Apps!");
        }

        private async void BtnOptimizeServices_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tắt dịch vụ ngầm...");
            await _optimizer.OptimizeServicesAsync();
            SetRunning(false, "Đã tắt các dịch vụ ngầm!");
        }

        private async void BtnTaskbar_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tối ưu Taskbar W11...");
            await _optimizer.OptimizeTaskbarWindows11Async();
            SetRunning(false, "Đã tối ưu Taskbar!");
        }

        private async void BtnSystemPro_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang áp dụng tinh chỉnh PRO...");
            await _optimizer.OptimizeSystemProAsync();
            SetRunning(false, "Đã tối ưu phản hồi hệ thống!");
        }

        private async void BtnFixWU_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang sửa lỗi Windows Update...");
            await _optimizer.FixWindowsUpdateAsync();
            SetRunning(false, "Đã sửa xong Windows Update!");
        }

        private void SetRunning(bool running, string statusText)
        {
            PbOptimizer.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtStatus.Visibility = Visibility.Visible;
            TxtStatus.Text = statusText;
            BtnCleanQuick.IsEnabled = !running;
            BtnCleanPro.IsEnabled = !running;
            BtnCleanDev.IsEnabled = !running;
            BtnCleanBrowser.IsEnabled = !running;
            BtnDisableStartup.IsEnabled = !running;
            BtnOptimizeServices.IsEnabled = !running;
            BtnTaskbar.IsEnabled = !running;
            BtnSystemPro.IsEnabled = !running;
            BtnFixWU.IsEnabled = !running;
        }
    }
}
