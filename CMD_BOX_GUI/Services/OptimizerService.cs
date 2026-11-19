using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CMD_BOX_GUI.Core;
using Microsoft.Win32;

namespace CMD_BOX_GUI.Services
{
    public class OptimizerService
    {
        public static long GetDriveFreeSpace(string driveLetter = "C:\\")
        {
            try
            {
                return new DriveInfo(driveLetter).AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }

        // ================= 1. QUICK CLEAN =================
        public async Task<long> CleanQuickAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Quick Clean bắt đầu...");
            long initialFree = GetDriveFreeSpace();

            var tasks = new Action[]
            {
                () => WipeDirectory(Path.GetTempPath()),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")),
                () => WipeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Recent)),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache")),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER", "Temp")),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps")),
                () =>
                {
                    try { NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND); } catch { }
                },
                () =>
                {
                    try { NativeMethods.DnsFlushResolverCache(); } catch { }
                }
            };

            await Task.Run(() =>
            {
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i]();
                    progress?.Report((int)((i + 1) * 100.0 / tasks.Length));
                }
            });

            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"[Optimizer] Quick Clean hoàn tất: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // ================= 2. DEEP CLEAN PRO =================
        public async Task<long> CleanDiskProAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Running Deep Clean PRO...");
            long initialFree = GetDriveFreeSpace();

            await CleanQuickAsync();
            progress?.Report(15);

            await ClearBrowserCacheAsync();
            progress?.Report(30);

            await Task.Run(async () =>
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                string[] deepPaths = {
                    Path.Combine(winDir, "Prefetch"),
                    Path.Combine(winDir, "SystemTemp"),
                    Path.Combine(winDir, "ServiceProfiles", "LocalService", "AppData", "Local", "Temp"),
                    Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Temp"),
                    Path.Combine(winDir, "SoftwareDistribution", "Download"),
                    Path.Combine(winDir, "SoftwareDistribution", "DataStore", "Logs"),
                    Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "cache"),
                    Path.Combine(winDir, "Logs", "CBS"),
                    Path.Combine(winDir, "Logs", "DISM"),
                    Path.Combine(winDir, "Logs", "DPX"),
                    Path.Combine(winDir, "Logs", "WindowsUpdate"),
                    Path.Combine(winDir, "Panther"),
                    Path.Combine(winDir, "Minidump"),
                    Path.Combine(localApp, "CrashDumps"),
                    Path.Combine(localApp, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(localApp, "Microsoft", "Windows", "WER", "ReportQueue"),
                    Path.Combine(localApp, "Microsoft", "Windows", "WER", "ERC"),
                    Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"),
                    Path.Combine(programData, "Microsoft", "Windows", "WER", "Temp"),
                    Path.Combine(localApp, "D3DSCache"),
                    Path.Combine(localApp, "NVIDIA", "DXCache"),
                    Path.Combine(localApp, "NVIDIA", "GLCache"),
                    Path.Combine(localApp, "NVIDIA Corporation", "NV_Cache"),
                    Path.Combine(localApp, "AMD", "DxCache"),
                    Path.Combine(localApp, "AMD", "GLCache"),
                    Path.Combine(localApp, "Intel", "ShaderCache"),
                    Path.Combine(localApp, "Microsoft", "Windows", "INetCache"),
                    Path.Combine(appData, "Microsoft", "CryptnetUrlCache", "Content"),
                    Path.Combine(appData, "Microsoft", "CryptnetUrlCache", "MetaData"),
                    Path.Combine(programData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Quick"),
                    Path.Combine(programData, "Microsoft", "Windows Defender", "Support"),
                    Path.Combine(winDir, "Installer", "$PatchCache$"),
                    Path.Combine(winDir, "Downloaded Program Files"),
                    Path.Combine(localApp, "Temp")
                };

                for (int i = 0; i < deepPaths.Length; i++)
                {
                    WipeDirectory(deepPaths[i]);
                    progress?.Report(30 + (int)((i + 1) * 35.0 / deepPaths.Length));
                }

                string memoryDmp = Path.Combine(winDir, "MEMORY.DMP");
                if (File.Exists(memoryDmp))
                {
                    try { File.Delete(memoryDmp); } catch { }
                }

                try
                {
                    await ProcessRunner.RunProcessAsync("powershell", "-NoProfile -Command \"Get-WinEvent -ListLog * -EA SilentlyContinue | ForEach-Object { Clear-WinEvent -LogName $_.LogName -EA SilentlyContinue }\"", runAsAdmin: true);
                }
                catch { }

                progress?.Report(75);

                try
                {
                    await ProcessRunner.RunProcessAsync("dism.exe", "/online /cleanup-image /startcomponentcleanup", runAsAdmin: true);
                }
                catch { }

                try
                {
                    NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
                    NativeMethods.DnsFlushResolverCache();
                }
                catch { }

                progress?.Report(100);
            });

            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"[Optimizer] Deep Clean PRO completed! Freed: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // ================= 3. BROWSER CACHE =================
        public async Task ClearBrowserCacheAsync()
        {
            Logger.Info("[Optimizer] Purging Browser Caches...");
            await Task.Run(() =>
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                string[] browserCaches = {
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Code Cache"),
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "GPUCache"),
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "ShaderCache"),
                    Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
                    Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "GPUCache"),
                    Path.Combine(localApp, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Code Cache"),
                    Path.Combine(localApp, "CocCoc", "Browser", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "CocCoc", "Browser", "User Data", "Default", "Code Cache"),
                    Path.Combine(appData, "Opera Software", "Opera Stable", "Cache"),
                    Path.Combine(appData, "Opera Software", "Opera GX Stable", "Cache"),
                    Path.Combine(localApp, "Opera Software", "Opera GX Stable", "Cache"),
                    Path.Combine(localApp, "Vivaldi", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "Arc", "User Data", "Default", "Cache")
                };

                foreach (var path in browserCaches)
                {
                    if (Directory.Exists(path)) WipeDirectory(path);
                }

                string ffProfile = Path.Combine(localApp, "Mozilla", "Firefox", "Profiles");
                if (Directory.Exists(ffProfile))
                {
                    foreach (var p in Directory.GetDirectories(ffProfile))
                    {
                        string cache2 = Path.Combine(p, "cache2");
                        if (Directory.Exists(cache2)) WipeDirectory(cache2);
                    }
                }
            });
            Logger.Success("[Optimizer] Browser caches purged!");
        }

        // ================= 4. DEV CACHES =================
        public async Task<long> CleanDevCachesAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Purging Developer Caches...");
            long initialFree = GetDriveFreeSpace();

            (string Cmd, string Args)[] devCommands = {
                ("npm", "cache clean --force"),
                ("yarn", "cache clean --force"),
                ("pip", "cache purge"),
                ("dotnet", "nuget locals all --clear")
            };

            for (int i = 0; i < devCommands.Length; i++)
            {
                try { await ProcessRunner.RunProcessAsync(devCommands[i].Cmd, devCommands[i].Args); } catch { }
                progress?.Report((int)((i + 1) * 80.0 / devCommands.Length));
            }

            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            WipeDirectory(Path.Combine(user, ".gradle", "caches"));
            WipeDirectory(Path.Combine(user, ".cargo", ".package-cache"));
            WipeDirectory(Path.Combine(user, ".cargo", "registry", "cache"));
            WipeDirectory(Path.Combine(user, ".nuget", "packages"));
            WipeDirectory(Path.Combine(user, ".composer", "cache"));

            progress?.Report(100);
            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"[Optimizer] Dev Caches purged! Freed: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // ================= 16. EXTENDED DISK CLEANING: WINDOWS.OLD =================
        public async Task<long> CleanWindowsOldAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Đang quét và dọn sạch các thư mục nâng cấp Windows cũ (Windows.old)...");
            long initialFree = GetDriveFreeSpace();

            await Task.Run(async () =>
            {
                progress?.Report(20);
                string[] oldPaths = {
                    @"C:\Windows.old",
                    @"C:\$Windows.~BT",
                    @"C:\$Windows.~WS"
                };

                for (int i = 0; i < oldPaths.Length; i++)
                {
                    string path = oldPaths[i];
                    if (Directory.Exists(path))
                    {
                        Logger.Info($"[Optimizer] Đang xóa thư mục: {path}...");
                        await ProcessRunner.RunProcessAsync("cmd.exe", $"/c takeown /F \"{path}\" /A /R /D Y >nul 2>&1 & icacls \"{path}\" /grant *S-1-5-32-544:F /T /C /Q >nul 2>&1 & rd /s /q \"{path}\" >nul 2>&1", runAsAdmin: true);
                    }
                    progress?.Report(20 + (int)((i + 1) * 60.0 / oldPaths.Length));
                }

                try
                {
                    await ProcessRunner.RunProcessAsync("dism.exe", "/online /cleanup-image /spsuperseded", runAsAdmin: true);
                }
                catch { }

                progress?.Report(100);
            });

            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"[Optimizer] Đã dọn dẹp Windows.old và tệp nâng cấp cũ! Dung lượng giải phóng: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        private static void WipeDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            try
            {
                var dir = new DirectoryInfo(path);
                foreach (var file in dir.GetFiles())
                {
                    try { file.Delete(); } catch { }
                }
                foreach (var sub in dir.GetDirectories())
                {
                    try { sub.Delete(true); } catch { }
                }
            }
            catch { }
        }

        // ================= 5. QUẢN LÝ DỊCH VỤ RÁC & UWP BLOATWARE (HELLZERG OPTIMIZER PATTERN) =================

        public static void RefreshServiceStatus(Models.SystemServiceItem svc)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{svc.ServiceName}");
                if (key != null && key.GetValue("Start") is int startVal)
                {
                    if (startVal == 4)
                    {
                        svc.CurrentStartupMode = Models.ServiceStartupMode.Disabled;
                        return;
                    }
                    if (startVal == 3)
                    {
                        svc.CurrentStartupMode = Models.ServiceStartupMode.Manual;
                        return;
                    }
                    if (startVal == 2)
                    {
                        bool isDelayed = key.GetValue("DelayedAutostart") is int d && d == 1;
                        svc.CurrentStartupMode = isDelayed
                            ? Models.ServiceStartupMode.AutomaticDelayed
                            : Models.ServiceStartupMode.Automatic;
                        return;
                    }
                }
                svc.CurrentStartupMode = Models.ServiceStartupMode.Manual;
            }
            catch
            {
                // Giữ nguyên mặc định nếu không đọc được
            }
        }

        public static List<Models.SystemServiceItem> GetUnneededServicesList()
        {
            var list = new List<Models.SystemServiceItem>
            {
                new() { ServiceName = "DiagTrack", DisplayName = "Connected User Experiences & Telemetry", Category = "Telemetry", Description = "Thu thập dữ liệu chẩn đoán & gửi ẩn về máy chủ Microsoft.", Impact = "Tiết kiệm RAM & SSD I/O", RiskLevel = "Safe", TargetStartupMode = Models.ServiceStartupMode.Disabled },
                new() { ServiceName = "dmwappushservice", DisplayName = "Device Management WAP Push", Category = "Telemetry", Description = "Kênh chuyển tin nhắn telemetry và định tuyến quản lý thiết bị.", Impact = "Bảo mật & Giảm tiến trình nền", RiskLevel = "Safe", TargetStartupMode = Models.ServiceStartupMode.Disabled },
                new() { ServiceName = "diagnosticshub.standardcollector.service", DisplayName = "Microsoft (R) Diagnostics Hub Standard Collector", Category = "Telemetry", Description = "Thu thập sự kiện chẩn đoán thời gian thực cho Visual Studio / OEM.", Impact = "Tiết kiệm CPU", RiskLevel = "Safe", TargetStartupMode = Models.ServiceStartupMode.Disabled },
                new() { ServiceName = "WerSvc", DisplayName = "Windows Error Reporting Service", Category = "Báo Lỗi", Description = "Ghi log khi app bị crash và gửi báo cáo lỗi lên Microsoft.", Impact = "Tiết kiệm dung lượng ổ C", RiskLevel = "Safe", TargetStartupMode = Models.ServiceStartupMode.Disabled },

                new() { ServiceName = "MapsBroker", DisplayName = "Downloaded Maps Manager", Category = "Hệ Thống", Description = "Tự động tải & cập nhật bản đồ ngoại tuyến của ứng dụng Windows Maps.", Impact = "Giảm tiêu hao SSD & Network", RiskLevel = "Moderate", TargetStartupMode = Models.ServiceStartupMode.Disabled },
                new() { ServiceName = "XblAuthManager", DisplayName = "Xbox Live Auth Manager", Category = "Xbox", Description = "Quản lý xác thực tài khoản Xbox Live (nên chuyển Manual/Disabled nếu không chơi game Xbox).", Impact = "Tiết kiệm RAM", RiskLevel = "Moderate", TargetStartupMode = Models.ServiceStartupMode.Manual },
                new() { ServiceName = "XblGameSave", DisplayName = "Xbox Live Game Save", Category = "Xbox", Description = "Đồng bộ lưu file save của các game trong Xbox App.", Impact = "Tiết kiệm Network & RAM", RiskLevel = "Moderate", TargetStartupMode = Models.ServiceStartupMode.Manual },
                new() { ServiceName = "XboxNetApiSvc", DisplayName = "Xbox Live Networking Service", Category = "Xbox", Description = "Dịch vụ mạng hỗ trợ kết nối mạng cho Xbox Live.", Impact = "Giảm tải mạng", RiskLevel = "Moderate", TargetStartupMode = Models.ServiceStartupMode.Manual },
                new() { ServiceName = "XboxGipSvc", DisplayName = "Xbox Accessory Management Service", Category = "Xbox", Description = "Quản lý phụ kiện tay cầm Xbox chuyên dụng.", Impact = "Tiết kiệm RAM", RiskLevel = "Moderate", TargetStartupMode = Models.ServiceStartupMode.Manual },

                new() { ServiceName = "WSearch", DisplayName = "Windows Search Indexer", Category = "Hệ Thống", Description = "Lập chỉ mục tìm kiếm liên tục (tắt giúp giảm 100% Disk HDD/SSD yếu, nhưng tìm file trong Explorer sẽ chậm hơn).", Impact = "Hạ nhiệt ổ cứng & CPU", RiskLevel = "Complex", TargetStartupMode = Models.ServiceStartupMode.Manual },
                new() { ServiceName = "SysMain", DisplayName = "SysMain (Superfetch)", Category = "Hệ Thống", Description = "Nạp trước ứng dụng vào RAM (tắt giúp giải phóng RAM cho game nặng, nhưng trên HDD mở app lần đầu chậm hơn).", Impact = "Chống nghẽn RAM & Disk", RiskLevel = "Complex", TargetStartupMode = Models.ServiceStartupMode.Disabled }
            };

            foreach (var svc in list)
            {
                RefreshServiceStatus(svc);
            }

            return list;
        }

        private static IEnumerable<string> BuildServiceModeCommands(string serviceName, Models.ServiceStartupMode mode)
        {
            return mode switch
            {
                Models.ServiceStartupMode.Disabled => new[]
                {
                    $"sc stop \"{serviceName}\" >nul 2>&1",
                    $"sc config \"{serviceName}\" start=disabled >nul 2>&1"
                },
                Models.ServiceStartupMode.Manual => new[]
                {
                    $"sc config \"{serviceName}\" start=demand >nul 2>&1"
                },
                Models.ServiceStartupMode.Automatic => new[]
                {
                    $"sc config \"{serviceName}\" start=auto >nul 2>&1",
                    $"sc start \"{serviceName}\" >nul 2>&1"
                },
                Models.ServiceStartupMode.AutomaticDelayed => new[]
                {
                    $"sc config \"{serviceName}\" start=delayed-auto >nul 2>&1"
                },
                _ => Array.Empty<string>()
            };
        }

        public async Task<int> ApplyServiceStartupModeAsync(string serviceName, Models.ServiceStartupMode mode)
        {
            var commands = BuildServiceModeCommands(serviceName, mode).ToList();
            if (commands.Count == 0) return 0;
            return await SystemCore.RunBatchScriptAsync(commands, $"svc_{serviceName}");
        }

        public async Task<int> ApplyServicesStartupBatchAsync(IEnumerable<(string ServiceName, Models.ServiceStartupMode Mode)> items)
        {
            var commands = new List<string>();
            foreach (var (svc, mode) in items)
            {
                commands.AddRange(BuildServiceModeCommands(svc, mode));
            }
            if (commands.Count == 0) return 0;
            return await SystemCore.RunBatchScriptAsync(commands, "batch_apply_services");
        }

        public async Task<int> DisableServicesBatchAsync(IEnumerable<string> serviceNames)
        {
            return await ApplyServicesStartupBatchAsync(serviceNames.Select(s => (s, Models.ServiceStartupMode.Disabled)));
        }

        public async Task<Models.JunkScanSummary> ScanJunkSizesAsync()
        {
            return await Task.Run(() =>
            {
                string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                long quick = GetDirectorySizeSafe(Path.GetTempPath())
                           + GetDirectorySizeSafe(Path.Combine(winDir, "Temp"))
                           + GetDirectorySizeSafe(Path.Combine(localApp, @"Microsoft\Windows\WER"))
                           + GetDirectorySizeSafe(Path.Combine(localApp, "D3DSCache"));

                long proExtra = GetDirectorySizeSafe(Path.Combine(winDir, "Prefetch"))
                              + GetDirectorySizeSafe(Path.Combine(winDir, @"SoftwareDistribution\DeliveryOptimization"))
                              + GetDirectorySizeSafe(Path.Combine(winDir, @"SoftwareDistribution\Download"))
                              + GetDirectorySizeSafe(Path.Combine(localApp, @"NVIDIA\DXCache"))
                              + GetDirectorySizeSafe(Path.Combine(localApp, @"AMD\DxCache"));

                long winOld = GetDirectorySizeSafe(@"C:\Windows.old")
                            + GetDirectorySizeSafe(@"C:\$Windows.~BT")
                            + GetDirectorySizeSafe(@"C:\$Windows.~WS");

                long devCache = GetDirectorySizeSafe(Path.Combine(user, ".gradle", "caches"))
                              + GetDirectorySizeSafe(Path.Combine(user, ".cargo", ".package-cache"))
                              + GetDirectorySizeSafe(Path.Combine(user, ".cargo", "registry", "cache"))
                              + GetDirectorySizeSafe(Path.Combine(user, ".nuget", "packages"))
                              + GetDirectorySizeSafe(Path.Combine(user, ".composer", "cache"))
                              + GetDirectorySizeSafe(Path.Combine(appData, "npm-cache"))
                              + GetDirectorySizeSafe(Path.Combine(localApp, "pip", "cache"));

                string[] browserDirs =
                {
                    Path.Combine(localApp, @"Google\Chrome\User Data\Default\Cache"),
                    Path.Combine(localApp, @"Google\Chrome\User Data\Default\Code Cache"),
                    Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Cache"),
                    Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Code Cache"),
                    Path.Combine(localApp, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"),
                    Path.Combine(localApp, @"CocCoc\Browser\User Data\Default\Cache"),
                    Path.Combine(localApp, @"Opera Software\Opera Stable\Cache"),
                    Path.Combine(localApp, @"Vivaldi\User Data\Default\Cache")
                };

                long browser = 0;
                foreach (var dir in browserDirs) browser += GetDirectorySizeSafe(dir);

                string ffProfile = Path.Combine(localApp, @"Mozilla\Firefox\Profiles");
                if (Directory.Exists(ffProfile))
                {
                    try
                    {
                        foreach (var p in Directory.GetDirectories(ffProfile))
                        {
                            browser += GetDirectorySizeSafe(Path.Combine(p, "cache2"));
                        }
                    }
                    catch { }
                }

                return new Models.JunkScanSummary
                {
                    QuickCleanBytes = quick,
                    DeepCleanProBytes = quick + proExtra,
                    WindowsOldBytes = winOld,
                    DevCacheBytes = devCache,
                    BrowserCacheBytes = browser
                };
            });
        }

        private static long GetDirectorySizeSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return 0;
            long total = 0;
            try
            {
                var stack = new Stack<string>();
                stack.Push(path);
                while (stack.Count > 0)
                {
                    string current = stack.Pop();
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(current))
                        {
                            try { total += new FileInfo(file).Length; } catch { }
                        }
                        foreach (var sub in Directory.EnumerateDirectories(current))
                        {
                            var attrs = File.GetAttributes(sub);
                            if ((attrs & FileAttributes.ReparsePoint) == 0)
                            {
                                stack.Push(sub);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return total;
        }
    }
}
