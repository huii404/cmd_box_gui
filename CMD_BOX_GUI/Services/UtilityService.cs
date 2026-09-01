using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CMD_BOX_GUI.Core;

namespace CMD_BOX_GUI.Services
{
    public class UtilityService
    {
        private CancellationTokenSource? _autoClickCts;
        private CancellationTokenSource? _spamCts;

        public async Task StartAutoClickAsync(int x, int y, int clickCount, int intervalMs, IProgress<int>? progress = null)
        {
            _autoClickCts?.Cancel();
            _autoClickCts = new CancellationTokenSource();
            var token = _autoClickCts.Token;

            Logger.Info($"Auto Click tại ({x}, {y}) | {clickCount} lần | {intervalMs}ms. [ESC/F6 ngắt]");

            await Task.Run(async () =>
            {
                int executed = 0;
                for (int i = 0; i < clickCount; i++)
                {
                    if (token.IsCancellationRequested || SystemCore.CheckEmergencyStop())
                    {
                        Logger.Warning($"Đã ngắt Auto Click ({executed}/{clickCount}).");
                        break;
                    }

                    NativeMethods.SetCursorPos(x, y);
                    SystemCore.SimulateLeftClick();
                    executed++;

                    if (clickCount > 0) progress?.Report((int)(executed * 100.0 / clickCount));

                    try { await Task.Delay(intervalMs, token); }
                    catch (TaskCanceledException) { break; }
                }

                if (executed == clickCount) Logger.Success($"Đã click xong {clickCount} lần!");
            }, token);
        }

        public void StopAutoClick() => _autoClickCts?.Cancel();

        public async Task SpamTextAsync(string content, int count, int delayMs, bool autoPressEnter = true)
        {
            _spamCts?.Cancel();
            _spamCts = new CancellationTokenSource();
            var token = _spamCts.Token;

            Logger.Info($"Spam Text ({count} lần)... Bắt đầu sau 2s. [ESC/F6 ngắt]");
            await Task.Delay(2000, token);

            await Task.Run(async () =>
            {
                SafeSetClipboardText(content);
                int executed = 0;
                for (int i = 0; i < count; i++)
                {
                    if (token.IsCancellationRequested || SystemCore.CheckEmergencyStop())
                    {
                        Logger.Warning($"Đã ngắt Spam Text ({executed}/{count}).");
                        break;
                    }

                    SystemCore.SimulateCtrlV();
                    if (autoPressEnter)
                    {
                        await Task.Delay(15);
                        SystemCore.SimulateEnter();
                    }

                    executed++;
                    try { await Task.Delay(delayMs, token); }
                    catch (TaskCanceledException) { break; }
                }

                if (executed == count) Logger.Success($"Đã gửi xong {count} lần!");
            }, token);
        }

        public void StopSpamText() => _spamCts?.Cancel();

        public async Task AutoPasteMultiLinesAsync(string multiLineContent, int delayMs)
        {
            var lines = multiLineContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            _spamCts?.Cancel();
            _spamCts = new CancellationTokenSource();
            var token = _spamCts.Token;

            Logger.Info($"Auto Paste {lines.Length} dòng... Bắt đầu sau 2s. [ESC/F6 ngắt]");
            await Task.Delay(2000, token);

            await Task.Run(async () =>
            {
                int executed = 0;
                foreach (var line in lines)
                {
                    if (token.IsCancellationRequested || SystemCore.CheckEmergencyStop())
                    {
                        Logger.Warning($"Đã ngắt Auto Paste ({executed}/{lines.Length}).");
                        break;
                    }

                    SafeSetClipboardText(line);
                    SystemCore.SimulateCtrlV();
                    await Task.Delay(15);
                    SystemCore.SimulateEnter();

                    executed++;
                    try { await Task.Delay(delayMs, token); }
                    catch (TaskCanceledException) { break; }
                }

                if (executed == lines.Length) Logger.Success($"Đã dán xong {lines.Length} dòng dữ liệu!");
            }, token);
        }

        private static void SafeSetClipboardText(string text)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Application.Current?.Dispatcher?.Invoke(() => Clipboard.SetDataObject(text, true));
                    return;
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }
        }

        public async Task OpenBatteryReportHtmlAsync()
        {
            string htmlPath = Path.Combine(Path.GetTempPath(), "cmd_battery_report.html");
            Logger.Info("Đang xuất báo cáo pin HTML...");
            await ProcessRunner.RunProcessAsync("powercfg", $"/batteryreport /output \"{htmlPath}\"");
            if (File.Exists(htmlPath))
            {
                Process.Start(new ProcessStartInfo { FileName = htmlPath, UseShellExecute = true });
                Logger.Success("Đã mở báo cáo pin HTML trên trình duyệt.");
            }
        }

        public async Task UninstallBloatwareAsync()
        {
            Logger.Info("Đang gỡ bỏ ứng dụng rác Windows & OneDrive...");
            var opt = new OptimizerService();
            await opt.DebloatUwpAppsAsync();
            await SystemCore.RestartExplorerAsync();
        }

        public async Task InstallQuickAppAsync(string appName, string wingetId)
        {
            Logger.Info($"Đang cài đặt phần mềm {appName}...");
            int code = await ProcessRunner.RunProcessAsync("winget", $"install --id {wingetId} --silent --accept-source-agreements --accept-package-agreements",
                line => Logger.Info($"[Winget] {line}"),
                err => Logger.Warning($"[Winget] {err}"));

            if (code == 0) Logger.Success($"Đã cài đặt thành công {appName}!");
            else Logger.Warning($"Cài đặt {appName} kết thúc (Mã: {code}).");
        }
    }
}
