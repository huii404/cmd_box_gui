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
        private readonly ObservableCollection<BloatwareAppItem> _bloatwareApps = new();
        private readonly System.ComponentModel.ICollectionView _servicesView;
        private readonly System.ComponentModel.ICollectionView _bloatwareView;
        private bool _isDataLoaded = false;

        public OptimizerView()
        {
            InitializeComponent();
            _servicesView = System.Windows.Data.CollectionViewSource.GetDefaultView(_unneededServices);
            _bloatwareView = System.Windows.Data.CollectionViewSource.GetDefaultView(_bloatwareApps);

            _servicesView.Filter = FilterServices;
            _bloatwareView.Filter = FilterBloatware;

            DgServices.ItemsSource = _servicesView;
            DgBloatware.ItemsSource = _bloatwareView;
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
            await RefreshBloatwareAsync();
        }

        private async Task RefreshJunkSizesAsync()
        {
            try
            {
                if (BtnScanJunkSizes != null) BtnScanJunkSizes.IsEnabled = false;
                var summary = await _optimizer.ScanJunkSizesAsync();
                BtnCleanQuick.Content = $"⚡ Quick Clean ({SystemCore.FormatBytes(summary.QuickCleanBytes)})";
                BtnCleanPro.Content = $"🔥 Deep Clean PRO ({SystemCore.FormatBytes(summary.DeepCleanProBytes)})";
                BtnCleanWindowsOld.Content = $"⚠️ Xóa Windows.old ({SystemCore.FormatBytes(summary.WindowsOldBytes)})";
                BtnCleanDev.Content = $"💻 Dọn Dev Cache ({SystemCore.FormatBytes(summary.DevCacheBytes)})";
                BtnCleanBrowser.Content = $"🌐 Dọn Cache Trình Duyệt ({SystemCore.FormatBytes(summary.BrowserCacheBytes)})";
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

        private async Task RefreshBloatwareAsync()
        {
            SetRunning(true, "Đang quét các ứng dụng rác thực tế trên máy...");
            _bloatwareApps.Clear();
            var apps = await _optimizer.ScanInstalledBloatwareAsync();
            foreach (var app in apps)
            {
                _bloatwareApps.Add(app);
            }

            int safeCount = _bloatwareApps.Count(a => a.RiskLevel == "Safe");
            int cautionCount = _bloatwareApps.Count(a => a.RiskLevel == "Caution");
            TxtBloatwareStats.Text = $"• Tìm thấy {_bloatwareApps.Count} ứng dụng ({safeCount} 🟢 An Toàn, {cautionCount} 🟡 Cần Cân Nhắc)";

            _bloatwareView.Refresh();
            TxtNoBloatware.Visibility = _bloatwareApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DgBloatware.Visibility = _bloatwareApps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            SetRunning(false, _bloatwareApps.Count > 0 
                ? $"Phát hiện {_bloatwareApps.Count} ứng dụng rác ({safeCount} Safe, {cautionCount} Caution)." 
                : "Hệ thống sạch sẽ! Không phát hiện ứng dụng rác nào cài sẵn.");
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

        private bool FilterBloatware(object obj)
        {
            if (obj is not BloatwareAppItem item) return false;

            string query = TxtSearchBloatware?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(query))
            {
                bool match = item.AppName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             item.PackageName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             item.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             item.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
                if (!match) return false;
            }

            if (CmbBloatwareRiskFilter?.SelectedIndex == 1 && item.RiskLevel != "Safe") return false;
            if (CmbBloatwareRiskFilter?.SelectedIndex == 2 && item.RiskLevel != "Caution") return false;

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

        private void TxtSearchBloatware_TextChanged(object sender, TextChangedEventArgs e)
        {
            _bloatwareView?.Refresh();
        }

        private void CmbBloatwareRiskFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _bloatwareView?.Refresh();
        }

        private void BtnSelectSafeBloatware_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _bloatwareApps)
            {
                app.IsSelected = (app.RiskLevel == "Safe");
            }
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

        private async void BtnRefreshBloatware_Click(object sender, RoutedEventArgs e)
        {
            await RefreshBloatwareAsync();
        }

        // ================= SUB-TAB SWITCHING LOGIC =================

        private void TabOpt_Click(object sender, RoutedEventArgs e)
        {
            if (PnlOptGeneral == null || PnlOptServices == null || PnlOptBloatware == null || PnlOptRescue == null) return;

            PnlOptGeneral.Visibility = TabOptGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlOptServices.Visibility = TabOptServices.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlOptBloatware.Visibility = TabOptBloatware.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlOptRescue.Visibility = TabOptRescue.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================= SERVICE & BLOATWARE ACTIONS =================

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

        private void BtnSelectAllBloatware_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _bloatwareApps) app.IsSelected = true;
        }

        private void BtnDeselectAllBloatware_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _bloatwareApps) app.IsSelected = false;
        }

        private async void BtnRemoveSelectedBloatware_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = _bloatwareApps.Where(a => a.IsSelected).ToList();
            if (selectedApps.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất một ứng dụng rác cần gỡ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Bạn có chắc muốn gỡ bỏ hoàn toàn {selectedApps.Count} ứng dụng rác đã chọn khỏi Windows?\n(Gỡ bỏ cho người dùng hiện tại, không spam popup hỏi quyền Admin).",
                "Xác nhận gỡ bỏ App Rác",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, $"Đang gỡ bỏ {selectedApps.Count} ứng dụng rác...");
            var patterns = selectedApps.Select(a => a.PackageName).ToList();
            bool ok = await _optimizer.RemoveBloatwareBatchAsync(patterns);

            if (ok)
            {
                foreach (var app in selectedApps)
                {
                    _bloatwareApps.Remove(app);
                }
            }

            TxtNoBloatware.Visibility = _bloatwareApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DgBloatware.Visibility = _bloatwareApps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            SetRunning(false, $"Đã gỡ sạch {selectedApps.Count} ứng dụng rác!");
            MessageBox.Show($"Quá trình gỡ bỏ hoàn tất! Đã loại bỏ {selectedApps.Count} ứng dụng rác khỏi hệ thống.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private async void BtnDisableStartup_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tối ưu Startup...");
            await _optimizer.DisableStartupAppsWithWhitelistAsync();
            SetRunning(false, "Đã tối ưu ứng dụng khởi động!");
        }

        private async void BtnOptimizeServices_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tối ưu Services...");
            await _optimizer.OptimizeServicesAsync();
            SetRunning(false, "Đã tắt Telemetry & dịch vụ thừa!");
        }

        private async void BtnTaskbar_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tinh chỉnh Taskbar Win 11...");
            await _optimizer.OptimizeTaskbarWindows11Async();
            SetRunning(false, "Đã tinh chỉnh Taskbar Win 11!");
        }

        private async void BtnSystemPro_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang áp dụng Low Latency Turbo...");
            await _optimizer.OptimizeSystemProAsync();
            SetRunning(false, "Đã áp dụng Low Latency Turbo!");
        }

        private async void BtnFixWU_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang sửa lỗi Windows Update...");
            await _optimizer.FixWindowsUpdateAsync();
            SetRunning(false, "Đã sửa xong Windows Update!");
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

        private async void BtnPrivacyGameDvr_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tối ưu Privacy & Game DVR...");
            await _optimizer.OptimizePrivacyAndGameDvrAsync();
            SetRunning(false, "Đã tối ưu Privacy & tắt Game DVR!");
        }

        private async void BtnCreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tạo Restore Point...");
            bool ok = await _optimizer.CreateRestorePointAsync();
            if (ok)
            {
                SetRunning(false, "Đã tạo Restore Point thành công!");
                MessageBox.Show("Đã tạo Restore Point thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                SetRunning(false, "Không thể tạo Restore Point (Cần quyền Admin).");
            }
        }

        private async void BtnRepairIntegrity_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang quét & sửa file hệ thống (DISM/SFC)...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            await _optimizer.RepairSystemIntegrityAsync(progress);
            SetRunning(false, "Quét & sửa file hệ thống hoàn tất!");
            MessageBox.Show("Quá trình DISM & SFC hoàn tất!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnPauseWU_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tạm dừng Windows Update...");
            await _optimizer.PauseWindowsUpdateAsync();
            SetRunning(false, "Đã tạm dừng Windows Update!");
        }

        private async void BtnResumeWU_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang khôi phục Windows Update...");
            await _optimizer.ResumeWindowsUpdateAsync();
            SetRunning(false, "Đã khôi phục Windows Update bình thường!");
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
            BtnDisableStartup.IsEnabled = !running;
            BtnOptimizeServices.IsEnabled = !running;
            BtnPrivacyGameDvr.IsEnabled = !running;
            BtnTaskbar.IsEnabled = !running;
            BtnSystemPro.IsEnabled = !running;
            BtnFixWU.IsEnabled = !running;
            BtnCreateRestorePoint.IsEnabled = !running;
            BtnRepairIntegrity.IsEnabled = !running;
            BtnPauseWU.IsEnabled = !running;
            BtnResumeWU.IsEnabled = !running;
        }
    }
}