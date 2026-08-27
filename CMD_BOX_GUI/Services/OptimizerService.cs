using System;
using System.Collections.Generic;
using System.IO;
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
                var drive = new DriveInfo(driveLetter);
                return drive.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }

        // 1. DỌN RÁC NHANH (QUICK CLEAN)
        public async Task<long> CleanQuickAsync(IProgress<int>? progress = null)
        {
            Logger.Info("Chạy Dọn rác nhanh...");
            long initialFree = GetDriveFreeSpace();

            var list = new List<Action>
            {
                () => WipeDirectory(Path.GetTempPath(), "User Temp"),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "System Temp"),
                () => WipeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Recent), "Recent"),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"), "DirectX"),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER", "Temp"), "WER Temp"),
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
                for (int i = 0; i < list.Count; i++)
                {
                    list[i]();
                    progress?.Report((int)((i + 1) * 100.0 / list.Count));
                }
            });

            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"Dọn nhanh xong! Đã giải phóng: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // 2. DỌN RÁC CHUYÊN SÂU PRO (DISK CLEAN PRO)
        public async Task<long> CleanDiskProAsync(IProgress<int>? progress = null)
        {
            Logger.Info("Chạy Dọn rác chuyên sâu PRO (Prefetch, WinSxS, Delivery, Logs, EventLogs)...");
            long initialFree = GetDriveFreeSpace();

            // Dọn nhanh trước
            await CleanQuickAsync();
            progress?.Report(20);

            // Dọn Browser Cache
            await ClearBrowserCacheAsync();
            progress?.Report(40);

            // Dọn Prefetch, CBS Logs, Delivery Optimization, WinSxS
            await Task.Run(async () =>
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                WipeDirectory(Path.Combine(winDir, "Prefetch"), "Prefetch");
                WipeDirectory(Path.Combine(winDir, "Logs", "CBS"), "CBS Logs");
                WipeDirectory(Path.Combine(winDir, "SoftwareDistribution", "Download"), "WinUpdate Download");

                string deliveryOpt = Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization");
                WipeDirectory(deliveryOpt, "Delivery Optimization");

                // Dọn Event Logs
                try
                {
                    Logger.Info("Đang dọn dẹp Event Logs...");
                    await ProcessRunner.RunProcessAsync("powershell", "-NoProfile -Command \"Get-WinEvent -ListLog * -EA SilentlyContinue | ForEach-Object { Clear-WinEvent -LogName $_.LogName -EA SilentlyContinue }\"", runAsAdmin: true);
                }
                catch { }

                progress?.Report(70);

                // Chạy DISM Component Cleanup
                try
                {
                    Logger.Info("Đang chạy DISM Component Cleanup (WinSxS)...");
                    await ProcessRunner.RunProcessAsync("dism.exe", "/online /cleanup-image /startcomponentcleanup /resetbase",
                        line => { if (line.Contains("%")) Logger.Info($"[DISM] {line.Trim()}"); },
                        runAsAdmin: true);
                }
                catch { }

                progress?.Report(100);
            });

            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"Dọn rác PRO hoàn tất! Tổng giải phóng: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // 3. DỌN CACHE TRÌNH DUYỆT (BROWSER CACHE)
        public async Task ClearBrowserCacheAsync()
        {
            Logger.Info("Đang dọn dẹp Cache trình duyệt (Chrome, Edge, Brave, Cốc Cốc, Firefox)...");
            await Task.Run(() =>
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var browserCaches = new List<string>
                {
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Code Cache"),
                    Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
                    Path.Combine(localApp, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "CocCoc", "Browser", "User Data", "Default", "Cache")
                };

                foreach (var path in browserCaches)
                {
                    if (Directory.Exists(path)) WipeDirectory(path, Path.GetFileName(Path.GetDirectoryName(path)) ?? "Browser");
                }

                // Firefox
                string ffProfile = Path.Combine(localApp, "Mozilla", "Firefox", "Profiles");
                if (Directory.Exists(ffProfile))
                {
                    foreach (var p in Directory.GetDirectories(ffProfile))
                    {
                        string cache2 = Path.Combine(p, "cache2");
                        if (Directory.Exists(cache2)) WipeDirectory(cache2, "Firefox Cache");
                    }
                }
            });
            Logger.Success("Đã làm sạch Cache các trình duyệt web!");
        }

        // 4. DỌN CACHE MÔI TRƯỜNG DEV
        public async Task<long> CleanDevCachesAsync(IProgress<int>? progress = null)
        {
            Logger.Info("Đang dọn dẹp Cache Dev (NPM, Pip, NuGet, Gradle, Cargo)...");
            long initialFree = GetDriveFreeSpace();

            var devCommands = new List<(string Name, string Cmd, string Args)>
            {
                ("NPM", "npm", "cache clean --force"),
                ("Pip", "pip", "cache purge"),
                ("NuGet", "dotnet", "nuget locals all --clear")
            };

            for (int i = 0; i < devCommands.Count; i++)
            {
                var d = devCommands[i];
                try { await ProcessRunner.RunProcessAsync(d.Cmd, d.Args); } catch { }
                progress?.Report((int)((i + 1) * 100.0 / devCommands.Count));
            }

            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            WipeDirectory(Path.Combine(user, ".gradle", "caches"), "Gradle");
            WipeDirectory(Path.Combine(user, ".cargo", ".package-cache"), "Cargo");

            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"Đã dọn sạch Dev Cache! Giải phóng: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // 5. QUẢN LÝ TẮT APP KHỞI ĐỘNG (STARTUP APPS VỚI WHITELIST)
        public async Task DisableStartupAppsWithWhitelistAsync()
        {
            Logger.Info("Đang quét Startup Apps và bảo vệ Driver/OEM thiết yếu...");
            await Task.Run(() =>
            {
                var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "realtek", "waves", "rtk", "nvidia", "nv", "amd", "intel", "synaptics",
                    "asus", "dell", "lenovo", "hp", "onedrive", "securityhealth", "windowsdefender"
                };

                int disabledCount = 0;
                string[] regPaths = {
                    @"Software\Microsoft\Windows\CurrentVersion\Run",
                };

                foreach (var path in regPaths)
                {
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(path, true);
                        if (key == null) continue;

                        foreach (var name in key.GetValueNames())
                        {
                            string val = key.GetValue(name)?.ToString() ?? "";
                            bool isSafe = false;
                            foreach (var w in whitelist)
                            {
                                if (name.Contains(w, StringComparison.OrdinalIgnoreCase) || val.Contains(w, StringComparison.OrdinalIgnoreCase))
                                {
                                    isSafe = true;
                                    break;
                                }
                            }

                            if (!isSafe)
                            {
                                key.DeleteValue(name, false);
                                disabledCount++;
                                Logger.Info($"[Startup] Đã tắt app khởi động: {name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Lỗi quét Registry Startup: {ex.Message}");
                    }
                }

                Logger.Success($"Đã tắt {disabledCount} ứng dụng khởi động không cần thiết (Bảo tồn Driver/GPU/Audio).");
            });
        }

        // 6. TẮT CÁC DỊCH VỤ WINDOWS KHÔNG CẦN THIẾT (TELEMETRY, XBOX, MAPS)
        public async Task OptimizeServicesAsync()
        {
            Logger.Info("Đang vô hiệu hóa các dịch vụ ngầm không cần thiết (Telemetry, Xbox, Maps, WER)...");
            var servicesToDisable = new[]
            {
                "DiagTrack", "dmwappushservice", "MapsBroker",
                "XblAuthManager", "XblGameSave", "XboxGipSvc", "XboxNetApiSvc",
                "WerSvc"
            };

            await Task.Run(async () =>
            {
                foreach (var svc in servicesToDisable)
                {
                    try
                    {
                        await ProcessRunner.RunProcessAsync("sc.exe", $"stop \"{svc}\"", runAsAdmin: true);
                        await ProcessRunner.RunProcessAsync("sc.exe", $"config \"{svc}\" start=disabled", runAsAdmin: true);
                        Logger.Info($"[Service] Đã tắt dịch vụ: {svc}");
                    }
                    catch { }
                }
            });

            Logger.Success("Đã tối ưu hóa và tắt các dịch vụ ngầm!");
        }

        // 7. TINH CHỈNH HỆ THỐNG PRO (HIBERNATE OFF, DESKTOP RESPONSIVENESS)
        public async Task OptimizeSystemProAsync()
        {
            Logger.Info("Đang áp dụng Tinh chỉnh Hệ thống PRO...");
            await Task.Run(async () =>
            {
                // Tắt Hibernate giải phóng nhiều GB hiberfil.sys
                await ProcessRunner.RunProcessAsync("powercfg", "-h off", runAsAdmin: true);
                Logger.Success("Đã tắt Hibernate (Giải phóng tệp hiberfil.sys).");

                // Registry tweaks
                try
                {
                    using (var desktopKey = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop"))
                    {
                        desktopKey.SetValue("MenuShowDelay", "0", RegistryValueKind.String);
                        desktopKey.SetValue("WaitToKillAppTimeout", "2000", RegistryValueKind.String);
                        desktopKey.SetValue("HungAppTimeout", "1000", RegistryValueKind.String);
                        desktopKey.SetValue("AutoEndTasks", "1", RegistryValueKind.String);
                    }

                    using (var multimediaKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"))
                    {
                        multimediaKey.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                        multimediaKey.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                    }
                    Logger.Success("Đã tối ưu hóa độ trễ phản hồi Desktop & Network Throttling!");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Lỗi ghi Registry: {ex.Message}");
                }
            });
        }

        // 8. TINH CHỈNH TASKBAR WIN 11
        public async Task OptimizeTaskbarWindows11Async()
        {
            Logger.Info("Đang ẩn icon thừa trên Taskbar Win 11 (Search, Widgets, Teams, Copilot)...");
            await Task.Run(() =>
            {
                try
                {
                    using (var searchKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Search"))
                    {
                        searchKey.SetValue("SearchboxTaskbarMode", 0, RegistryValueKind.DWord);
                    }

                    using (var explorerKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                    {
                        explorerKey.SetValue("TaskbarDa", 0, RegistryValueKind.DWord);
                        explorerKey.SetValue("TaskbarMn", 0, RegistryValueKind.DWord);
                        explorerKey.SetValue("ShowTaskViewButton", 0, RegistryValueKind.DWord);
                    }

                    using (var copilotKey = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot"))
                    {
                        copilotKey.SetValue("TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
                    }

                    Logger.Success("Đã tối ưu Taskbar Win 11 thành công!");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Lỗi Registry Taskbar: {ex.Message}");
                }
            });
        }

        // 9. SỬA LỖI WINDOWS UPDATE
        public async Task FixWindowsUpdateAsync()
        {
            Logger.Info("Đang sửa lỗi Windows Update...");
            string script = @"
net stop wuauserv /y
net stop cryptSvc /y
net stop bits /y
net stop msiserver /y
Ren ""%systemroot%\SoftwareDistribution"" SoftwareDistribution.bak 2>nul
Ren ""%systemroot%\System32\catroot2"" catroot2.bak 2>nul
net start wuauserv
net start cryptSvc
net start bits
net start msiserver
";
            string tempBat = Path.Combine(Path.GetTempPath(), "fix_wu.bat");
            try
            {
                await File.WriteAllTextAsync(tempBat, script);
                await ProcessRunner.RunProcessAsync("cmd.exe", $"/c \"{tempBat}\"", runAsAdmin: true);
                Logger.Success("Đã sửa lỗi và reset cache Windows Update!");
            }
            finally
            {
                if (File.Exists(tempBat)) File.Delete(tempBat);
            }
        }

        private static void WipeDirectory(string path, string label)
        {
            if (!Directory.Exists(path)) return;
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
    }
}
