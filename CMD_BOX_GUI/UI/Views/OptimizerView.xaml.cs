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
            Loaded += (_, _) => LoadStartupGreeting();
        }

        private void LoadStartupGreeting()
        {
            var (caption, text) = _optimizer.GetStartupGreeting();
            TxtGreetingCaption.Text = caption;
            TxtGreetingText.Text = text;
        }

        private async void BtnSaveGreeting_Click(object sender, RoutedEventArgs e)
        {
            string caption = TxtGreetingCaption.Text.Trim();
            string text = TxtGreetingText.Text.Trim();

            if (string.IsNullOrWhiteSpace(caption) && string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Vui lòng nhập tiêu đề hoặc nội dung lời chào!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool ok = await _optimizer.SetStartupGreetingAsync(caption, text);
            if (ok)
            {
                MessageBox.Show("Đã lưu lời chào khởi động Windows thành công!\nThông điệp sẽ xuất hiện ở màn hình chào mừng mỗi khi bật máy tính.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Không thể ghi vào Registry. Vui lòng đảm bảo ứng dụng có quyền Administrator.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnClearGreeting_Click(object sender, RoutedEventArgs e)
        {
            await _optimizer.ClearStartupGreetingAsync();
            TxtGreetingCaption.Text = "";
            TxtGreetingText.Text = "";
            MessageBox.Show("Đã xóa bỏ lời chào khởi động Windows.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnMasterMakeWin_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🚀 Đang thực thi Master Make Win (Chuỗi tinh chỉnh tự động N-trong-1)...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);

            // Tự động nhận diện: Nếu người dùng có nhập lời chào thì lưu vào Registry, nếu để trống thì bỏ qua
            string caption = TxtGreetingCaption?.Text?.Trim() ?? "";
            string text = TxtGreetingText?.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(caption) || !string.IsNullOrWhiteSpace(text))
            {
                await _optimizer.SetStartupGreetingAsync(caption, text);
            }

            await _optimizer.MasterMakeWinAsync(progress);
            SetRunning(false, "🎉 Master Make Win hoàn tất! Toàn bộ tinh chỉnh Taskbar, Services, Bing & Debloat đã được áp dụng!");
        }

        private async void BtnCleanQuick_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "⚡ Running Quick Clean (Temp files, D3D Cache, Recycle Bin, DNS)...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanQuickAsync(progress);
            SetRunning(false, $"✅ Quick Clean completed! Storage reclaimed: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanPro_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🔥 Running Deep Clean PRO (WinSxS, Prefetch, GPU Shaders, Event Logs, 20+ hidden paths)...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanDiskProAsync(progress);
            SetRunning(false, $"✅ Deep Clean PRO completed! Total storage reclaimed: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanDev_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "💻 Purging Developer Caches (NPM, Yarn, Pip, NuGet, Gradle, Cargo)...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanDevCachesAsync(progress);
            SetRunning(false, $"✅ Dev Caches purged! Storage reclaimed: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnCleanBrowser_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🌐 Purging Browser Caches (Chrome, Edge, Brave, CocCoc, Firefox, Opera, Vivaldi, Arc)...");
            await _optimizer.ClearBrowserCacheAsync();
            SetRunning(false, "✅ All browser caches cleaned successfully!");
        }

        private async void BtnDisableStartup_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🛑 Scanning and disabling unneeded Startup apps...");
            await _optimizer.DisableStartupAppsWithWhitelistAsync();
            SetRunning(false, "✅ Startup apps optimized! (Essential OEM/Driver components preserved)");
        }

        private async void BtnOptimizeServices_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🛡️ Disabling background Telemetry & Xbox bloat services...");
            await _optimizer.OptimizeServicesAsync();
            SetRunning(false, "✅ Background telemetry & diagnostic tracking services disabled!");
        }

        private async void BtnTaskbar_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🪟 Optimizing Windows 11 Taskbar...");
            await _optimizer.OptimizeTaskbarWindows11Async();
            SetRunning(false, "✅ Windows 11 Taskbar cleaned and optimized!");
        }

        private async void BtnSystemPro_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "⚡ Applying Low Latency Turbo tweaks...");
            await _optimizer.OptimizeSystemProAsync();
            SetRunning(false, "✅ Low Latency Turbo tweaks applied! (Desktop responsiveness & Network gaming tuned)");
        }

        private async void BtnFixWU_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🔄 Repairing Windows Update components & resetting cache...");
            await _optimizer.FixWindowsUpdateAsync();
            SetRunning(false, "✅ Windows Update components repaired successfully!");
        }

        private async void BtnCleanWindowsOld_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                "Bạn có chắc chắn muốn xóa thư mục Windows.old và các tệp cài đặt nâng cấp cũ không?\n" +
                "Lưu ý: Sau khi xóa, bạn sẽ không thể hạ cấp (roll back) về bản Windows trước đó!",
                "Xác nhận dọn dẹp Windows.old",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes) return;

            SetRunning(true, "🗑️ Đang quét và dọn sạch Windows.old & các tệp nâng cấp cũ...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            long freed = await _optimizer.CleanWindowsOldAsync(progress);
            SetRunning(false, $"✅ Đã dọn dẹp Windows.old thành công! Dung lượng giải phóng: {SystemCore.FormatBytes(freed)}");
        }

        private async void BtnPrivacyGameDvr_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🔒 Đang cấu hình Quyền riêng tư & Tắt Xbox Game DVR ngốn FPS...");
            await _optimizer.OptimizePrivacyAndGameDvrAsync();
            SetRunning(false, "✅ Đã tối ưu Quyền riêng tư (Tắt Timeline, Ad ID, Định vị) và tắt Xbox Game DVR!");
        }

        private async void BtnCreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "💾 Đang khởi tạo System Restore Point (Điểm khôi phục hệ thống)...");
            bool ok = await _optimizer.CreateRestorePointAsync();
            if (ok)
            {
                SetRunning(false, "✅ Đã tạo System Restore Point 'CMD_BOX_SafeBackup' thành công!");
                MessageBox.Show("Đã tạo điểm khôi phục (Restore Point) an toàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                SetRunning(false, "⚠️ Không thể tạo Restore Point. Vui lòng kiểm tra quyền Administrator hoặc dung lượng ổ đĩa.");
            }
        }

        private async void BtnRepairIntegrity_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "🩺 Đang quét và tự vá file hệ thống với DISM & SFC (có thể mất 3-8 phút)...");
            var progress = new Progress<int>(v => PbOptimizer.Value = v);
            await _optimizer.RepairSystemIntegrityAsync(progress);
            SetRunning(false, "✅ Quá trình quét và vá file hệ thống hoàn tất!");
            MessageBox.Show("Quá trình quét và sửa chữa file hệ thống bằng DISM & SFC đã hoàn tất!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnPauseWU_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "⏸️ Đang cấu hình tạm dừng Windows Update...");
            await _optimizer.PauseWindowsUpdateAsync();
            SetRunning(false, "✅ Đã tạm dừng Windows Update (hoãn đến 2038 và tắt service ngầm)!");
        }

        private async void BtnResumeWU_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "▶️ Đang khôi phục lại trạng thái Windows Update ban đầu...");
            await _optimizer.ResumeWindowsUpdateAsync();
            SetRunning(false, "✅ Đã khôi phục dịch vụ Windows Update hoạt động bình thường!");
        }

        private void SetRunning(bool running, string statusText)
        {
            PbOptimizer.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtStatus.Visibility = Visibility.Visible;
            TxtStatus.Text = statusText;

            BtnMasterMakeWin.IsEnabled = !running;
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
