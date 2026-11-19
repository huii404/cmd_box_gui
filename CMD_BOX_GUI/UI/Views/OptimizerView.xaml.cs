using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;
using CMD_BOX_GUI.Services;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class OptimizerView : UserControl
    {
        private readonly OptimizerService _optimizer = new();
        private readonly ObservableCollection<SystemServiceItem> _unneededServices = new();
        private readonly System.ComponentModel.ICollectionView _servicesView;
        private bool _isDataLoaded = false;

        public OptimizerView()
        {
            InitializeComponent();
            _servicesView = System.Windows.Data.CollectionViewSource.GetDefaultView(_unneededServices);
            _servicesView.Filter = FilterServices;
            DgServices.ItemsSource = _servicesView;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDriveInfo();
            if (!_isDataLoaded)
            {
                _isDataLoaded = true;
                await RefreshAllDataAsync();
            }
        }

        private async Task RefreshAllDataAsync()
        {
            await RefreshJunkSizesAsync();
            await RefreshServicesAsync();
        }

        private async Task RefreshJunkSizesAsync()
        {
            try
            {
                if (BtnScanJunkSizes != null) BtnScanJunkSizes.IsEnabled = false;
                var summary = await _optimizer.ScanJunkSizesAsync();
                TxtSizeQuick.Text = SystemCore.FormatBytes(summary.QuickCleanBytes);
                TxtSizePro.Text = SystemCore.FormatBytes(summary.DeepCleanProBytes);
                TxtSizeWindowsOld.Text = SystemCore.FormatBytes(summary.WindowsOldBytes);
                TxtSizeDev.Text = SystemCore.FormatBytes(summary.DevCacheBytes);
                TxtSizeBrowser.Text = SystemCore.FormatBytes(summary.BrowserCacheBytes);
                UpdateDriveInfo();
            }
            catch { }
            finally
            {
                if (BtnScanJunkSizes != null) BtnScanJunkSizes.IsEnabled = true;
            }
        }

        private async void BtnScanJunkSizes_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang quét dung lượng rác ở từng phân mục...");
            await RefreshJunkSizesAsync();
            SetRunning(false, "Đã cập nhật dung lượng rác phát hiện trên các nút dọn dẹp.");
        }

        private async Task RefreshServicesAsync()
        {
            _unneededServices.Clear();
            var services = await Task.Run(() => OptimizerService.GetUnneededServicesList());
            foreach (var svc in services)
            {
                _unneededServices.Add(svc);
            }
            _servicesView.Refresh();
        }

        private bool FilterServices(object obj)
        {
            if (obj is not SystemServiceItem item) return false;

            string query = TxtSearchServices?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(query))
            {
                bool match = item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             item.ServiceName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             item.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             item.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
                if (!match) return false;
            }

            if (CmbServicesRiskFilter?.SelectedIndex == 1 && item.RiskLevel != "Safe") return false;
            if (CmbServicesRiskFilter?.SelectedIndex == 2 && item.RiskLevel != "Moderate") return false;
            if (CmbServicesRiskFilter?.SelectedIndex == 3 && item.RiskLevel != "Complex") return false;

            return true;
        }

        private void TxtSearchServices_TextChanged(object sender, TextChangedEventArgs e)
        {
            _servicesView?.Refresh();
        }

        private void CmbServicesRiskFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _servicesView?.Refresh();
        }

        private async void BtnDisableSafeServices_Click(object sender, RoutedEventArgs e)
        {
            var safeServicesToDisable = _unneededServices.Where(s => s.RiskLevel == "Safe" && !s.IsDisabled).ToList();
            if (safeServicesToDisable.Count == 0)
            {
                MessageBox.Show("Tất cả các dịch vụ an toàn (Safe) đã được vô hiệu hóa từ trước!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Bạn có chắc muốn vô hiệu hóa {safeServicesToDisable.Count} dịch vụ Telemetry an toàn 100%?\n(Giữ nguyên các dịch vụ Xbox và Windows Search).",
                "Xác nhận tắt dịch vụ An Toàn",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, $"Đang tắt {safeServicesToDisable.Count} dịch vụ telemetry an toàn...");
            await _optimizer.DisableServicesBatchAsync(safeServicesToDisable.Select(s => s.ServiceName));

            foreach (var svc in _unneededServices)
            {
                OptimizerService.RefreshServiceStatus(svc);
            }

            SetRunning(false, $"Đã tắt thành công {safeServicesToDisable.Count} dịch vụ telemetry an toàn!");
            MessageBox.Show($"Đã vô hiệu hóa thành công {safeServicesToDisable.Count} dịch vụ an toàn!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnRefreshServices_Click(object sender, RoutedEventArgs e)
        {
            await RefreshServicesAsync();
            SetRunning(false, "Đã cập nhật lại trạng thái các dịch vụ hệ thống.");
        }

        // ================= SUB-TAB SWITCHING LOGIC =================

        private void TabOpt_Click(object sender, RoutedEventArgs e)
        {
            if (PnlOptGeneral == null || PnlOptServices == null) return;

            PnlOptGeneral.Visibility = TabOptGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlOptServices.Visibility = TabOptServices.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================= SERVICE ACTIONS =================

        private async void BtnToggleSingleService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SystemServiceItem item)
            {
                SetRunning(true, $"Đang đặt service [{item.ServiceName}] sang [{item.TargetStartupMode}]...");

                await _optimizer.ApplyServiceStartupModeAsync(item.ServiceName, item.TargetStartupMode);
                OptimizerService.RefreshServiceStatus(item);

                SetRunning(false, $"Đã áp dụng [{item.TargetStartupMode}] cho service [{item.ServiceName}]!");
            }
        }

        private async void BtnDisableAllSelectedServices_Click(object sender, RoutedEventArgs e)
        {
            var servicesToApply = _unneededServices
                .Where(s => s.TargetStartupMode != s.CurrentStartupMode)
                .ToList();

            if (servicesToApply.Count == 0)
            {
                MessageBox.Show("Tất cả dịch vụ đã ở đúng chế độ mục tiêu bạn chọn!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Áp dụng chế độ khởi động mục tiêu cho {servicesToApply.Count} dịch vụ đã thay đổi?\n(Gom toàn bộ lệnh chạy 1 lần duy nhất).",
                "Xác nhận áp dụng Services",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, $"Đang áp dụng cấu hình cho {servicesToApply.Count} dịch vụ...");
            await _optimizer.ApplyServicesStartupBatchAsync(servicesToApply.Select(s => (s.ServiceName, s.TargetStartupMode)));

            foreach (var svc in _unneededServices)
            {
                OptimizerService.RefreshServiceStatus(svc);
            }

            SetRunning(false, $"Đã áp dụng thành công cấu hình cho {servicesToApply.Count} dịch vụ!");
            MessageBox.Show($"Đã áp dụng thành công cho {servicesToApply.Count} dịch vụ!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRefreshDrive_Click(object sender, RoutedEventArgs e)
        {
            UpdateDriveInfo();
        }

        private void UpdateDriveInfo()
        {
            try
            {
                long freeBytes = OptimizerService.GetDriveFreeSpace("C:\\");
                var drive = new System.IO.DriveInfo("C:\\");
                long totalBytes = drive.TotalSize;
                double percentFree = (double)freeBytes / totalBytes * 100.0;
                TxtDriveFree.Text = $"Ổ C: Trống {SystemCore.FormatBytes(freeBytes)} / {SystemCore.FormatBytes(totalBytes)} ({percentFree:0.#}% khả dụng)";
            }
            catch
            {
                TxtDriveFree.Text = "Ổ C: Trạng thái bình thường.";
            }
        }

        private async void BtnCleanQuick_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang chạy Quick Clean...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanQuickAsync(progress);
            await RefreshJunkSizesAsync();
            SetRunning(false, $"Quick Clean xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanPro_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang chạy Deep Clean PRO...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanDiskProAsync(progress);
            await RefreshJunkSizesAsync();
            SetRunning(false, $"Deep Clean PRO xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanDev_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang dọn cache Dev...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanDevCachesAsync(progress);
            await RefreshJunkSizesAsync();
            SetRunning(false, $"Dọn cache Dev xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanBrowser_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang dọn cache trình duyệt...");
            await _optimizer.ClearBrowserCacheAsync();
            await RefreshJunkSizesAsync();
            SetRunning(false, "Đã dọn sạch cache trình duyệt!");
        }

        private async void BtnCleanWindowsOld_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                "Bạn có chắc muốn xóa Windows.old và tệp nâng cấp cũ?\nSau khi xóa sẽ không thể khôi phục bản Windows trước.",
                "Xác nhận",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes) return;

            SetRunning(true, "Đang dọn Windows.old...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanWindowsOldAsync(progress);
            await RefreshJunkSizesAsync();
            SetRunning(false, $"Dọn Windows.old xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private void SetRunning(bool running, string statusText)
        {
            PbOptimizer.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtStatus.Text = statusText;
            if (!running)
            {
                UpdateDriveInfo();
            }

            BtnCleanQuick.IsEnabled = !running;
            BtnCleanPro.IsEnabled = !running;
            BtnCleanWindowsOld.IsEnabled = !running;
            BtnCleanDev.IsEnabled = !running;
            BtnCleanBrowser.IsEnabled = !running;
        }
    }
}
