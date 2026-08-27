using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;

namespace CMD_BOX_GUI.Services
{
    public class UtilityService
    {
        private CancellationTokenSource? _autoClickCts;

        // 1. AUTO CLICKER
        public async Task StartAutoClickAsync(int x, int y, int clickCount, int intervalMs, IProgress<int>? progress = null)
        {
            _autoClickCts?.Cancel();
            _autoClickCts = new CancellationTokenSource();
            var token = _autoClickCts.Token;

            Logger.Info($"Bắt đầu Auto Click tại ({x}, {y}) | {clickCount} lần | {intervalMs}ms. [ESC/F6 ngắt]");

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

        // 2. SPAM TEXT
        public async Task SpamTextAsync(string content, int count, int delayMs, bool autoPressEnter = true)
        {
            Logger.Info($"Spam Text ({count} lần)... Bắt đầu sau 2s. [ESC/F6 ngắt]");
            await Task.Delay(2000);

            await Task.Run(async () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try { Clipboard.SetDataObject(content, true); } catch { }
                });

                int executed = 0;
                for (int i = 0; i < count; i++)
                {
                    if (SystemCore.CheckEmergencyStop())
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
                    await Task.Delay(delayMs);
                }

                if (executed == count) Logger.Success($"Đã gửi xong {count} lần!");
            });
        }

        // 3. AUTO PASTE MULTI-LINES
        public async Task AutoPasteMultiLinesAsync(string multiLineContent, int delayMs)
        {
            var lines = multiLineContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            Logger.Info($"Auto Paste {lines.Length} dòng... Bắt đầu sau 2s. [ESC/F6 ngắt]");
            await Task.Delay(2000);

            await Task.Run(async () =>
            {
                int executed = 0;
                foreach (var line in lines)
                {
                    if (SystemCore.CheckEmergencyStop())
                    {
                        Logger.Warning($"Đã ngắt Auto Paste ({executed}/{lines.Length}).");
                        break;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try { Clipboard.SetDataObject(line, true); } catch { }
                    });

                    SystemCore.SimulateCtrlV();
                    await Task.Delay(15);
                    SystemCore.SimulateEnter();

                    executed++;
                    await Task.Delay(delayMs);
                }

                if (executed == lines.Length) Logger.Success($"Đã dán xong {lines.Length} dòng dữ liệu!");
            });
        }

        // 4. CHẨN ĐOÁN PIN LAPTOP
        public async Task<BatteryInfo> GetBatteryReportAsync()
        {
            var info = new BatteryInfo();
            if (NativeMethods.GetSystemPowerStatus(out var sps))
            {
                info.HasBattery = sps.BatteryFlag != 128;
                info.IsCharging = (sps.BatteryFlag & 8) != 0;
                info.Percent = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 0;
                info.PowerSource = sps.ACLineStatus == 1 ? "Cắm sạc (AC)" : "Pin (DC)";
            }

            if (!info.HasBattery) return info;

            string xmlPath = Path.Combine(Path.GetTempPath(), "cmd_battery_report.xml");
            try
            {
                await ProcessRunner.RunProcessAsync("powercfg", $"/batteryreport /xml /output \"{xmlPath}\"");
                if (File.Exists(xmlPath))
                {
                    string xmlContent = await File.ReadAllTextAsync(xmlPath);
                    var doc = XDocument.Parse(xmlContent);

                    info.Manufacturer = doc.Root?.Element("Batteries")?.Element("Battery")?.Element("Manufacturer")?.Value ?? "N/A";
                    info.DeviceName = doc.Root?.Element("Batteries")?.Element("Battery")?.Element("Id")?.Value ?? "N/A";
                    info.Chemistry = doc.Root?.Element("Batteries")?.Element("Battery")?.Element("Chemistry")?.Value ?? "Li-ion";
                    info.SystemModel = doc.Root?.Element("SystemInformation")?.Element("SystemProductName")?.Value ?? "N/A";

                    string? dcStr = doc.Root?.Element("Batteries")?.Element("Battery")?.Element("DesignCapacity")?.Value;
                    string? fcStr = doc.Root?.Element("Batteries")?.Element("Battery")?.Element("FullChargeCapacity")?.Value;
                    string? cycleStr = doc.Root?.Element("Batteries")?.Element("Battery")?.Element("CycleCount")?.Value;

                    if (long.TryParse(dcStr, out long dc)) info.DesignCapacityMWh = dc;
                    if (long.TryParse(fcStr, out long fc)) info.FullChargeCapacityMWh = fc;
                    if (long.TryParse(cycleStr, out long cc)) info.CycleCount = cc;

                    if (info.DesignCapacityMWh > 0 && info.FullChargeCapacityMWh > 0)
                    {
                        info.HealthPercent = Math.Min(100.0, (double)info.FullChargeCapacityMWh / info.DesignCapacityMWh * 100.0);
                        info.WearPercent = Math.Max(0.0, 100.0 - info.HealthPercent);
                    }
                }
            }
            catch { }
            finally
            {
                if (File.Exists(xmlPath)) File.Delete(xmlPath);
            }
            return info;
        }

        public async Task OpenBatteryReportHtmlAsync()
        {
            string htmlPath = Path.Combine(Path.GetTempPath(), "cmd_battery_report.html");
            await ProcessRunner.RunProcessAsync("powercfg", $"/batteryreport /output \"{htmlPath}\"");
            if (File.Exists(htmlPath))
            {
                Process.Start(new ProcessStartInfo { FileName = htmlPath, UseShellExecute = true });
                Logger.Success("Đã mở báo cáo pin HTML.");
            }
        }

        // 5. GỠ BLOATWARE
        public async Task UninstallBloatwareAsync()
        {
            Logger.Info("Đang gỡ bỏ ứng dụng rác Windows...");
            string psScript = @"
$apps = @('Microsoft.BingNews','Microsoft.BingWeather','Microsoft.GetHelp','Microsoft.Getstarted','Microsoft.MicrosoftSolitaireCollection','Microsoft.People','Microsoft.SkypeApp','Microsoft.Todos','Microsoft.YourPhone','Microsoft.XboxApp','Microsoft.ZuneMusic','Microsoft.ZuneVideo','Clipchamp.Clipchamp')
foreach ($app in $apps) { Get-AppxPackage -Name $app -AllUsers | Remove-AppxPackage -EA SilentlyContinue }
";
            await ProcessRunner.RunProcessAsync("powershell", $"-NoProfile -Command \"{psScript.Replace(Environment.NewLine, " ")}\"", runAsAdmin: true);
            Logger.Success("Đã gỡ sạch Bloatware!");
        }

        // 6. CÀI PHẦN MỀM NHANH QUA WINGET
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
