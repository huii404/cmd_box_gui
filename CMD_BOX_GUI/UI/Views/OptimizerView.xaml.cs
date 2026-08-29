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
