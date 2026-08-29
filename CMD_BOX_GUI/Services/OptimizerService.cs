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
                var drive = new DriveInfo(driveLetter);
                return drive.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }

        // ================= 1. QUICK CLEAN (FAST CLEANUP) =================
        public async Task<long> CleanQuickAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Running Quick Clean (Temp files, WER, D3D Cache, Recycle Bin, DNS)...");
            long initialFree = GetDriveFreeSpace();

            var cleanupTasks = new List<Action>
            {
                () => WipeDirectory(Path.GetTempPath(), "User Temp"),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "System Temp"),
                () => WipeDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Recent), "Recent Files"),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"), "DirectX Shader"),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER", "Temp"), "WER Temp"),
                () => WipeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"), "User CrashDumps"),
                () =>
                {
                    try
                    {
                        NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
                    }
                    catch { }
                },
                () =>
                {
                    try
                    {
                        NativeMethods.DnsFlushResolverCache();
                    }
                    catch { }
                }
            };

            await Task.Run(() =>
            {
                for (int i = 0; i < cleanupTasks.Count; i++)
                {
                    cleanupTasks[i]();
                    progress?.Report((int)((i + 1) * 100.0 / cleanupTasks.Count));
                }
            });

            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"[Optimizer] Quick Clean completed! Freed space: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // ================= 2. DEEP CLEAN PRO (EXHAUSTIVE DISK & SYSTEM CLEANUP) =================
        public async Task<long> CleanDiskProAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Running Deep Clean PRO (All hidden locations, WinSxS, Delivery Opt, Prefetch, GPU Shaders, Crash Dumps, EventLogs)...");
            long initialFree = GetDriveFreeSpace();

            // Giai đoạn 1: Dọn nhanh + Browser Caches
            await CleanQuickAsync();
            progress?.Report(15);

            await ClearBrowserCacheAsync();
            progress?.Report(30);

            // Giai đoạn 2: Quét sạch mọi ngóc ngách sâu trong Windows
            await Task.Run(async () =>
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                var deepLocations = new List<(string Path, string Label)>
                {
                    // 1. Windows System Core Temps & Prefetch
                    (Path.Combine(winDir, "Prefetch"), "Windows Prefetch"),
                    (Path.Combine(winDir, "SystemTemp"), "Windows SystemTemp"),
                    (Path.Combine(winDir, "ServiceProfiles", "LocalService", "AppData", "Local", "Temp"), "LocalService Temp"),
                    (Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Temp"), "NetworkService Temp"),

                    // 2. Windows Update & Delivery Optimization Cache
                    (Path.Combine(winDir, "SoftwareDistribution", "Download"), "Windows Update Downloads"),
                    (Path.Combine(winDir, "SoftwareDistribution", "DataStore", "Logs"), "Windows Update Logs"),
                    (Path.Combine(winDir, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "cache"), "Delivery Optimization Cache"),

                    // 3. System Logs & Panther Setup Logs
                    (Path.Combine(winDir, "Logs", "CBS"), "CBS Component Logs"),
                    (Path.Combine(winDir, "Logs", "DISM"), "DISM Service Logs"),
                    (Path.Combine(winDir, "Logs", "DPX"), "DPX Setup Logs"),
                    (Path.Combine(winDir, "Logs", "WindowsUpdate"), "Windows Update Logs"),
                    (Path.Combine(winDir, "Panther"), "Windows Setup Panther Logs"),

                    // 4. Crash Dumps & Minidumps
                    (Path.Combine(winDir, "Minidump"), "BSOD Minidumps"),
                    (Path.Combine(localApp, "CrashDumps"), "Application Crash Dumps"),

                    // 5. Windows Error Reporting (WER) Deep Dumps
                    (Path.Combine(localApp, "Microsoft", "Windows", "WER", "ReportArchive"), "WER User Archive"),
                    (Path.Combine(localApp, "Microsoft", "Windows", "WER", "ReportQueue"), "WER User Queue"),
                    (Path.Combine(localApp, "Microsoft", "Windows", "WER", "ERC"), "WER User ERC"),
                    (Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"), "WER System Archive"),
                    (Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"), "WER System Queue"),
                    (Path.Combine(programData, "Microsoft", "Windows", "WER", "Temp"), "WER System Temp"),

                    // 6. GPU Shader & DirectX Caches (NVIDIA, AMD, Intel, D3D)
                    (Path.Combine(localApp, "D3DSCache"), "Direct3D Shader Cache"),
                    (Path.Combine(localApp, "NVIDIA", "DXCache"), "NVIDIA DirectX Cache"),
                    (Path.Combine(localApp, "NVIDIA", "GLCache"), "NVIDIA OpenGL Cache"),
                    (Path.Combine(localApp, "NVIDIA Corporation", "NV_Cache"), "NVIDIA NV Cache"),
                    (Path.Combine(localApp, "AMD", "DxCache"), "AMD DirectX Cache"),
                    (Path.Combine(localApp, "AMD", "GLCache"), "AMD OpenGL Cache"),
                    (Path.Combine(localApp, "Intel", "ShaderCache"), "Intel Shader Cache"),

                    // 7. Network & Cryptnet Temporary SSL/TLS Caches
                    (Path.Combine(localApp, "Microsoft", "Windows", "INetCache"), "INetCache Temporary"),
                    (Path.Combine(appData, "Microsoft", "CryptnetUrlCache", "Content"), "Cryptnet Content"),
                    (Path.Combine(appData, "Microsoft", "CryptnetUrlCache", "MetaData"), "Cryptnet Metadata"),

                    // 8. Windows Defender Scans Cache & Support Temp
                    (Path.Combine(programData, "Microsoft", "Windows Defender", "Scans", "History", "Results", "Quick"), "Defender Scan History"),
                    (Path.Combine(programData, "Microsoft", "Windows Defender", "Support"), "Defender Support Logs"),

                    // 9. Windows Installer Patch Cache ($PatchCache$) & Downloaded Program Files
                    (Path.Combine(winDir, "Installer", "$PatchCache$"), "Installer PatchCache"),
                    (Path.Combine(winDir, "Downloaded Program Files"), "Downloaded Program Files"),

                    // 10. Local App Temporary Packages
                    (Path.Combine(localApp, "Temp"), "Local User Temp")
                };

                int totalCount = deepLocations.Count;
                for (int i = 0; i < totalCount; i++)
                {
                    var loc = deepLocations[i];
                    WipeDirectory(loc.Path, loc.Label);
                    progress?.Report(30 + (int)((i + 1) * 35.0 / totalCount));
                }

                // Xóa tệp memory dump lớn nếu có (C:\Windows\MEMORY.DMP)
                string memoryDmp = Path.Combine(winDir, "MEMORY.DMP");
                if (File.Exists(memoryDmp))
                {
                    try { File.Delete(memoryDmp); Logger.Info("[Deep Clean] Deleted MEMORY.DMP"); } catch { }
                }

                // Dọn Event Logs
                try
                {
                    Logger.Info("[Deep Clean] Clearing Windows Event Logs...");
                    await ProcessRunner.RunProcessAsync("powershell", "-NoProfile -Command \"Get-WinEvent -ListLog * -EA SilentlyContinue | ForEach-Object { Clear-WinEvent -LogName $_.LogName -EA SilentlyContinue }\"", runAsAdmin: true);
                }
                catch { }

                progress?.Report(75);

                // Chạy DISM Component Store Cleanup (Thu dọn các bản cập nhật cũ trong WinSxS)
                try
                {
                    Logger.Info("[Deep Clean] Running DISM Component Store Cleanup (WinSxS)...");
                    await ProcessRunner.RunProcessAsync("dism.exe", "/online /cleanup-image /startcomponentcleanup",
                        line => { if (line.Contains("%")) Logger.Info($"[DISM] {line.Trim()}"); },
                        runAsAdmin: true);
                }
                catch { }

                // Flush DNS và Empty Recycle Bin
                try
                {
                    NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
                    NativeMethods.DnsFlushResolverCache();
                }
                catch { }

                progress?.Report(100);
            });

            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"[Optimizer] Deep Clean PRO finished! Total freed: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // ================= 3. BROWSER CACHE PURGE =================
        public async Task ClearBrowserCacheAsync()
        {
            Logger.Info("[Optimizer] Purging Browser Caches (Chrome, Edge, Brave, CocCoc, Firefox, Opera, Vivaldi, Arc)...");
            await Task.Run(() =>
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                var browserCaches = new List<string>
                {
                    // Chrome
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Code Cache"),
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "GPUCache"),
                    Path.Combine(localApp, "Google", "Chrome", "User Data", "ShaderCache"),

                    // Edge
                    Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
                    Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "GPUCache"),

                    // Brave
                    Path.Combine(localApp, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Code Cache"),

                    // CocCoc
                    Path.Combine(localApp, "CocCoc", "Browser", "User Data", "Default", "Cache"),
                    Path.Combine(localApp, "CocCoc", "Browser", "User Data", "Default", "Code Cache"),

                    // Opera & Opera GX
                    Path.Combine(appData, "Opera Software", "Opera Stable", "Cache"),
                    Path.Combine(appData, "Opera Software", "Opera GX Stable", "Cache"),
                    Path.Combine(localApp, "Opera Software", "Opera GX Stable", "Cache"),

                    // Vivaldi
                    Path.Combine(localApp, "Vivaldi", "User Data", "Default", "Cache"),

                    // Arc Browser
                    Path.Combine(localApp, "Arc", "User Data", "Default", "Cache")
                };

                foreach (var path in browserCaches)
                {
                    if (Directory.Exists(path)) WipeDirectory(path, Path.GetFileName(Path.GetDirectoryName(path)) ?? "Browser");
                }

                // Firefox Profiles
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
            Logger.Success("[Optimizer] Browser caches cleaned successfully!");
        }

        // ================= 4. DEV ENVIRONMENT CACHE PURGE =================
        public async Task<long> CleanDevCachesAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Purging Developer Caches (NPM, Yarn, Pip, NuGet, Gradle, Cargo)...");
            long initialFree = GetDriveFreeSpace();

            var devCommands = new List<(string Name, string Cmd, string Args)>
            {
                ("NPM", "npm", "cache clean --force"),
                ("Yarn", "yarn", "cache clean --force"),
                ("Pip", "pip", "cache purge"),
                ("NuGet", "dotnet", "nuget locals all --clear")
            };

            for (int i = 0; i < devCommands.Count; i++)
            {
                var d = devCommands[i];
                try { await ProcessRunner.RunProcessAsync(d.Cmd, d.Args); } catch { }
                progress?.Report((int)((i + 1) * 80.0 / devCommands.Count));
            }

            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            WipeDirectory(Path.Combine(user, ".gradle", "caches"), "Gradle Cache");
            WipeDirectory(Path.Combine(user, ".cargo", ".package-cache"), "Cargo Package Cache");
            WipeDirectory(Path.Combine(user, ".cargo", "registry", "cache"), "Cargo Registry Cache");
            WipeDirectory(Path.Combine(user, ".nuget", "packages"), "User NuGet Packages Temp");
            WipeDirectory(Path.Combine(user, ".composer", "cache"), "Composer Cache");

            progress?.Report(100);
            long freed = Math.Max(0, GetDriveFreeSpace() - initialFree);
            Logger.Success($"[Optimizer] Dev Caches purged! Freed: {SystemCore.FormatBytes(freed)}");
            return freed;
        }

        // ================= 5. STARTUP APPS OPTIMIZER (WITH DRIVER/OEM WHITELIST) =================
        public async Task DisableStartupAppsWithWhitelistAsync()
        {
            Logger.Info("[Optimizer] Scanning Startup Apps (Preserving essential Audio/GPU/OEM drivers)...");
            await Task.Run(() =>
            {
                var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "realtek", "waves", "rtk", "nvidia", "nv", "amd", "intel", "synaptics",
                    "asus", "dell", "lenovo", "hp", "onedrive", "securityhealth", "windowsdefender",
                    "cmd_box_gui", "antigravity"
                };

                int disabledCount = 0;
                string[] regPaths = {
                    @"Software\Microsoft\Windows\CurrentVersion\Run",
                    @"Software\Microsoft\Windows\CurrentVersion\RunOnce"
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
                                Logger.Info($"[Startup] Disabled startup app: {name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"[Startup Scan] {ex.Message}");
                    }
                }

                Logger.Success($"[Optimizer] Disabled {disabledCount} non-essential startup apps (OEM/Hardware drivers safe).");
            });
        }

        // ================= 6. DISABLE TELEMETRY & BLOAT SERVICES =================
        public async Task OptimizeServicesAsync()
        {
            Logger.Info("[Optimizer] Disabling Telemetry, Diagnostic Tracking & Xbox bloat services...");
            var servicesToDisable = new[]
            {
                "DiagTrack", "dmwappushservice", "MapsBroker",
                "XblAuthManager", "XblGameSave", "XboxGipSvc", "XboxNetApiSvc",
                "WerSvc", "RetailDemo"
            };

            await Task.Run(async () =>
            {
                foreach (var svc in servicesToDisable)
                {
                    try
                    {
                        await ProcessRunner.RunProcessAsync("sc.exe", $"stop \"{svc}\"", runAsAdmin: true);
                        await ProcessRunner.RunProcessAsync("sc.exe", $"config \"{svc}\" start=disabled", runAsAdmin: true);
                        Logger.Info($"[Service] Disabled: {svc}");
                    }
                    catch { }
                }
            });

            Logger.Success("[Optimizer] Background telemetry & bloat services disabled!");
        }

        // ================= 7. LOW LATENCY TURBO & SYSTEM PRO TWEAKS =================
        public async Task OptimizeSystemProAsync()
        {
            Logger.Info("[Optimizer] Applying Low Latency Turbo tweaks (Responsiveness, Network Throttling, AutoEndTasks)...");
            await Task.Run(async () =>
            {
                // Tắt Hibernate giải phóng nhiều GB hiberfil.sys
                await ProcessRunner.RunProcessAsync("powercfg", "-h off", runAsAdmin: true);
                Logger.Success("[Optimizer] Disabled Hibernation (Saved hiberfil.sys disk space).");

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
                    Logger.Success("[Optimizer] Desktop responsiveness and Network Gaming Throttling optimized!");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[Registry Tweak] {ex.Message}");
                }
            });
        }

        // ================= 8. WINDOWS 11 TASKBAR TWEAKS =================
        public async Task OptimizeTaskbarWindows11Async()
        {
            Logger.Info("[Optimizer] Hiding Taskbar clutter on Windows 11 (Search, Widgets, Chat, Copilot)...");
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

                    Logger.Success("[Optimizer] Windows 11 Taskbar optimized cleanly!");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Taskbar Tweak] {ex.Message}");
                }
            });
        }

        // ================= 9. FIX WINDOWS UPDATE =================
        public async Task FixWindowsUpdateAsync()
        {
            Logger.Info("[Optimizer] Repairing Windows Update components & reset caches...");
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
                Logger.Success("[Optimizer] Windows Update repaired & cache refreshed!");
            }
            finally
            {
                if (File.Exists(tempBat)) File.Delete(tempBat);
            }
        }

        private static void WipeDirectory(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

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
