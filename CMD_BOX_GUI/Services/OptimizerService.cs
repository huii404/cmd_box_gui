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
            Logger.Info("[Optimizer] Running Quick Clean...");
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
            Logger.Success($"[Optimizer] Quick Clean completed! Freed: {SystemCore.FormatBytes(freed)}");
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

        // ================= 5. STARTUP APPS =================
        public async Task DisableStartupAppsWithWhitelistAsync()
        {
            Logger.Info("[Optimizer] Scanning Startup Apps (Driver whitelist active)...");
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
                                Logger.Info($"[Startup] Disabled: {name}");
                            }
                        }
                    }
                    catch { }
                }

                Logger.Success($"[Optimizer] Disabled {disabledCount} non-essential startup apps.");
            });
        }

        // ================= 6. SERVICES =================
        public async Task OptimizeServicesAsync()
        {
            Logger.Info("[Optimizer] Disabling Telemetry & bloat background services...");
            string[] servicesToDisable = {
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
                    }
                    catch { }
                }
            });

            Logger.Success("[Optimizer] Background telemetry services disabled!");
        }

        // ================= 7. TURBO TWEAKS =================
        public async Task OptimizeSystemProAsync()
        {
            Logger.Info("[Optimizer] Applying Turbo low-latency tweaks...");
            await Task.Run(async () =>
            {
                await ProcessRunner.RunProcessAsync("powercfg", "-h off", runAsAdmin: true);

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
                    Logger.Success("[Optimizer] Desktop responsiveness & network gaming optimized!");
                }
                catch { }
            });
        }

        // ================= 8. TASKBAR TWEAKS =================
        public async Task OptimizeTaskbarWindows11Async()
        {
            Logger.Info("[Optimizer] Optimizing Windows 11 Taskbar...");
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

                    Logger.Success("[Optimizer] Windows 11 Taskbar optimized!");
                }
                catch { }
            });
        }

        // ================= 9. FIX WINDOWS UPDATE =================
        public async Task FixWindowsUpdateAsync()
        {
            Logger.Info("[Optimizer] Repairing Windows Update components...");
            string script = "net stop wuauserv /y && net stop cryptSvc /y && net stop bits /y && net stop msiserver /y && " +
                            "Ren \"%systemroot%\\SoftwareDistribution\" SoftwareDistribution.bak 2>nul && " +
                            "Ren \"%systemroot%\\System32\\catroot2\" catroot2.bak 2>nul && " +
                            "net start wuauserv && net start cryptSvc && net start bits && net start msiserver";

            await ProcessRunner.RunProcessAsync("cmd.exe", $"/c {script}", runAsAdmin: true);
            Logger.Success("[Optimizer] Windows Update repaired!");
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
    }
}
