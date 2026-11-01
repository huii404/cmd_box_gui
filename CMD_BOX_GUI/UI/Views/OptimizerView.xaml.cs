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
            await RefreshServicesAsync();
            await RefreshBloatwareAsync();
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
            if (CmbServicesRiskFilter?.SelectedIndex == 2 && item.RiskLevel != "Caution") return false;

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

            // Lọc theo Nguồn gốc BloatFinder: 1=Sponsored, 2=OEM, 3=Microsoft
            if (CmbBloatwareOriginFilter?.SelectedIndex == 1 && item.Origin != "Sponsored") return false;
            if (CmbBloatwareOriginFilter?.SelectedIndex == 2 && item.Origin != "OEM") return false;
            if (CmbBloatwareOriginFilter?.SelectedIndex == 3 && item.Origin != "Microsoft") return false;

            return true;
        }

        private void CmbBloatwareOriginFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _bloatwareView?.Refresh();
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
                bool newState = !item.IsDisabled;
                string actionName = newState ? "tắt" : "bật lại";
                SetRunning(true, $"Đang {actionName} service [{item.ServiceName}]...");

                await _optimizer.ToggleServiceStateAsync(item.ServiceName, newState);
                OptimizerService.RefreshServiceStatus(item);

                SetRunning(false, $"Thao tác [{actionName}] service [{item.ServiceName}] hoàn tất!");
            }
        }

        private async void BtnDisableAllSelectedServices_Click(object sender, RoutedEventArgs e)
        {
            var servicesToDisable = _unneededServices.Where(s => !s.IsDisabled).ToList();
            if (servicesToDisable.Count == 0)
            {
                MessageBox.Show("Tất cả các dịch vụ trong danh sách này đã được vô hiệu hóa từ trước!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Bạn có chắc muốn vô hiệu hóa {servicesToDisable.Count} dịch vụ telemetry/nền đang chạy?\n(Thao tác gom lệnh chạy 1 lần duy nhất, không hiện nhiều popup xác nhận).",
                "Xác nhận tắt Services",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, $"Đang vô hiệu hóa {servicesToDisable.Count} dịch vụ nền ngốn tài nguyên...");
            await _optimizer.DisableServicesBatchAsync(servicesToDisable.Select(s => s.ServiceName));

            foreach (var svc in _unneededServices)
            {
                OptimizerService.RefreshServiceStatus(svc);
            }

            SetRunning(false, $"Đã tắt thành công {servicesToDisable.Count} dịch vụ nền ngốn tài nguyên!");
            MessageBox.Show($"Đã vô hiệu hóa thành công {servicesToDisable.Count} dịch vụ nền!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
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
            SetRunning(false, $"Quick Clean xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanPro_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang chạy Deep Clean PRO...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanDiskProAsync(progress);
            SetRunning(false, $"Deep Clean PRO xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanDev_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang dọn cache Dev...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanDevCachesAsync(progress);
            SetRunning(false, $"Dọn cache Dev xong! Giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanBrowser_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang dọn cache trình duyệt...");
            await _optimizer.ClearBrowserCacheAsync();
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

        // ================= BLOATYNOSY-INSPIRED HEALTH CHECK & PROFILES =================

        private async void BtnHealthCheck_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang quét chẩn đoán tình trạng hệ thống theo chuẩn Bloatynosy...");
            TxtHealthStatus.Text = "⏳ Đang phân tích telemetry, rác quảng cáo, Copilot AI và ứng dụng nền...";
            
            var report = await _optimizer.ScanSystemHealthAsync();
            
            PnlHealthBadges.Visibility = Visibility.Visible;
            TxtHealthScore.Text = report.HealthScoreText;
            BrdHealthScore.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(report.HealthScoreColor)!;
            
            TxtTelemetryBadge.Text = report.IsTelemetryActive ? "Telemetry: 🔴 Đang Gửi Log" : "Telemetry: 🟢 Đã Tắt Sạch";
            TxtAdsBadge.Text = report.IsWin11AdsActive ? "Quảng Cáo Win11: 🔴 Đang Bật" : "Quảng Cáo Win11: 🟢 Đã Tắt";
            TxtBloatBadge.Text = report.DetectedBloatwareCount > 0 ? $"App Rác: 🟡 Còn {report.DetectedBloatwareCount} App" : "App Rác: 🟢 Sạch Rác";

            string statusMsg = report.TotalIssuesFound == 0 
                ? "✨ Hệ thống của bạn đã ở trạng thái tối ưu tuyệt đối (100/100)!"
                : $"Phát hiện {report.TotalIssuesFound} vấn đề. Bạn có thể chọn Profile 1-Click bên dưới để xử lý ngay.";

            TxtHealthStatus.Text = statusMsg;
            SetRunning(false, $"Chẩn đoán hoàn tất! Phát hiện {report.TotalIssuesFound} điểm cần tinh chỉnh.");
        }

        private async void BtnProfileGamer_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Kích hoạt Gamer Profile (Max FPS)?\n\n" +
                "• Tắt Telemetry ngầm và Game DVR\n" +
                "• Tắt Search Indexing & SysMain chống giật lag disk\n" +
                "• Tối ưu độ trễ chuột và Desktop Low Latency\n" +
                "• Kích hoạt gói điện năng Ultimate Performance\n\n" +
                "(Thao tác an toàn 100%, có thể bật lại bình thường).",
                "Xác nhận kích hoạt Profile Game Thủ",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, "Đang kích hoạt Gamer Profile (Max FPS)...");
            await _optimizer.ApplyGamerProfileAsync();
            await RefreshServicesAsync();
            SetRunning(false, "Đã kích hoạt Profile Game Thủ thành công!");
            MessageBox.Show("Profile Game Thủ đã được kích hoạt thành công! Máy tính đã sẵn sàng cho hiệu năng tối đa.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnProfileOffice_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Kích hoạt Office Profile (Văn phòng & Ổn định)?\n\n" +
                "• Dọn nhanh rác bộ nhớ đệm Quick Clean\n" +
                "• Tắt toàn bộ quảng cáo Start Menu Windows 11\n" +
                "• Tắt Edge Startup Boost ngốn RAM ngầm\n" +
                "• Gọn thanh Taskbar Windows 11\n" +
                "• Giữ nguyên máy in, chia sẻ mạng và OneDrive",
                "Xác nhận kích hoạt Profile Văn Phòng",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, "Đang kích hoạt Office Profile...");
            await _optimizer.ApplyOfficeProfileAsync();
            SetRunning(false, "Đã kích hoạt Profile Văn Phòng thành công!");
            MessageBox.Show("Profile Văn Phòng đã được kích hoạt thành công! Hệ thống êm ái, sạch rác và ổn định.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnProfilePrivacy_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Kích hoạt Privacy Profile (Bảo mật & Riêng tư)?\n\n" +
                "• Tắt sạch các dịch vụ Telemetry (DiagTrack, dmwappush, WerSvc)\n" +
                "• Tắt dịch vụ định vị Geolocation và Windows Insider\n" +
                "• Tắt trợ lý ảo Windows Copilot AI ngầm\n" +
                "• Tắt quảng cáo gợi ý và thu thập dữ liệu cá nhân",
                "Xác nhận kích hoạt Profile Riêng Tư",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, "Đang kích hoạt Privacy Profile...");
            await _optimizer.ApplyPrivacyProfileAsync();
            await RefreshServicesAsync();
            SetRunning(false, "Đã kích hoạt Profile Riêng Tư thành công!");
            MessageBox.Show("Profile Riêng Tư đã được kích hoạt thành công! Dữ liệu của bạn đã được bảo vệ tối đa.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnDisableWin11Ads_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tắt quảng cáo Start Menu & Windows 11...");
            await _optimizer.DisableWin11AdsAsync();
            SetRunning(false, "Đã tắt sạch quảng cáo và gợi ý trên Windows 11!");
            MessageBox.Show("Đã tắt thành công quảng cáo trong Start Menu, màn hình khóa và cài đặt Windows 11!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnDisableCopilot_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang vô hiệu hóa Windows Copilot & AI ngầm...");
            await _optimizer.DisableCopilotAIAsync();
            SetRunning(false, "Đã tắt Windows Copilot AI!");
            MessageBox.Show("Đã vô hiệu hóa thành công Windows Copilot AI!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnDisableEdgeBoost_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang tắt Edge Startup Boost...");
            await _optimizer.DisableEdgeStartupBoostAsync();
            SetRunning(false, "Đã tắt Edge chạy ngầm khi mở máy!");
            MessageBox.Show("Đã tắt tính năng Edge Startup Boost chạy ngầm khi khởi động máy!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (BtnHealthCheck != null) BtnHealthCheck.IsEnabled = !running;
            if (BtnProfileGamer != null) BtnProfileGamer.IsEnabled = !running;
            if (BtnProfileOffice != null) BtnProfileOffice.IsEnabled = !running;
            if (BtnProfilePrivacy != null) BtnProfilePrivacy.IsEnabled = !running;
            if (BtnDisableWin11Ads != null) BtnDisableWin11Ads.IsEnabled = !running;
            if (BtnDisableCopilot != null) BtnDisableCopilot.IsEnabled = !running;
            if (BtnDisableEdgeBoost != null) BtnDisableEdgeBoost.IsEnabled = !running;
        }
    }
}