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
            Logger.Info("[Optimizer] Tắt các dịch vụ theo dõi Telemetry & Services vô dụng...");
            string[] servicesToDisable = {
                "DiagTrack", "dmwappushservice", "MapsBroker",
                "XblAuthManager", "XblGameSave", "XboxGipSvc", "XboxNetApiSvc",
                "WerSvc", "RetailDemo", "SensorService", "SensrSvc", "Fax"
            };

            var commands = new List<string>();
            foreach (var svc in servicesToDisable)
            {
                commands.Add($"sc stop \"{svc}\" >nul 2>&1");
                commands.Add($"sc config \"{svc}\" start=disabled >nul 2>&1");
            }

            await SystemCore.RunBatchScriptAsync(commands, "optimize_services");
            Logger.Success("[Optimizer] Đã tắt các dịch vụ Telemetry, Game Xbox thừa (vẫn giữ lại quay màn hình BcastDVR)!");
        }

        // ================= 7. TURBO TWEAKS =================
        public async Task OptimizeSystemProAsync()
        {
            Logger.Info("[Optimizer] Đang áp dụng tinh chỉnh giảm độ trễ Turbo & Hệ thống...");
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
                        multimediaKey.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                    }

                    // 2.2: Chặn Windows tự động ép Restart máy khi có bản cập nhật Update
                    using (var auKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU"))
                    {
                        auKey.SetValue("NoAutoRebootWithLoggedOnUsers", 1, RegistryValueKind.DWord);
                        auKey.SetValue("AUOptions", 3, RegistryValueKind.DWord);
                    }

                    // 2.3: Tắt chớp đen màn hình khi UAC hỏi quyền Admin (PromptOnSecureDesktop)
                    using (var uacKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                    {
                        uacKey.SetValue("PromptOnSecureDesktop", 0, RegistryValueKind.DWord);
                    }

                    // 3.3: Tắt cảnh báo phiền toái "Low Disk Space" khi ổ đĩa gần đầy
                    using (var polExpKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                    {
                        polExpKey.SetValue("NoLowDiskSpaceChecks", 1, RegistryValueKind.DWord);
                    }

                    Logger.Success("[Optimizer] Đã tối ưu độ nhạy (0ms), Chặn tự Reboot khi Update, Tắt chớp đen UAC & Tắt cảnh báo đầy ổ!");
                }
                catch { }
            });
        }

        // ================= 8. TASKBAR TWEAKS =================
        public async Task OptimizeTaskbarWindows11Async()
        {
            Logger.Info("[Optimizer] Đang tinh chỉnh giao diện Taskbar & Menu chuột phải...");
            await Task.Run(async () =>
            {
                try
                {
                    using (var searchKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Search"))
                    {
                        searchKey.SetValue("SearchboxTaskbarMode", 0, RegistryValueKind.DWord);
                        searchKey.SetValue("BingSearchEnabled", 0, RegistryValueKind.DWord);
                        searchKey.SetValue("CortanaConsent", 0, RegistryValueKind.DWord);
                    }

                    using (var policySearchKey = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Explorer"))
                    {
                        policySearchKey.SetValue("DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
                    }

                    using (var explorerKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                    {
                        explorerKey.SetValue("TaskbarDa", 0, RegistryValueKind.DWord); // Widgets
                        explorerKey.SetValue("TaskbarMn", 0, RegistryValueKind.DWord); // Chat
                        explorerKey.SetValue("ShowTaskViewButton", 0, RegistryValueKind.DWord); // Task View
                        explorerKey.SetValue("ShowSecondsInSystemClock", 1, RegistryValueKind.DWord); // Hiện giây đồng hồ
                        explorerKey.SetValue("HideFileExt", 0, RegistryValueKind.DWord); // Hiện đuôi file
                        explorerKey.SetValue("Start_TrackDocs", 0, RegistryValueKind.DWord); // 1.2: Tắt theo dõi file mở
                        explorerKey.SetValue("Start_TrackProgs", 0, RegistryValueKind.DWord);
                    }

                    // 1.2: Tắt sạch lịch sử Recent Files & Frequent Folders trong Explorer
                    using (var expKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer"))
                    {
                        expKey.SetValue("ShowRecent", 0, RegistryValueKind.DWord);
                        expKey.SetValue("ShowFrequent", 0, RegistryValueKind.DWord);
                    }

                    using (var copilotKey = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot"))
                    {
                        copilotKey.SetValue("TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
                    }

                    // Khôi phục Menu chuột phải Win 10 trên Win 11 (Bỏ "Show more options")
                    using (var clsidKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"))
                    {
                        clsidKey.SetValue("", "");
                    }

                    // Tắt quảng cáo đề xuất cài app trong Start Menu
                    using (var contentKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"))
                    {
                        contentKey.SetValue("SystemPaneSuggestionsEnabled", 0, RegistryValueKind.DWord);
                        contentKey.SetValue("SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord);
                        contentKey.SetValue("SubscribedContent-310093Enabled", 0, RegistryValueKind.DWord);
                    }

                    Logger.Success("[Optimizer] Đã tinh chỉnh Taskbar gọn sạch, Tắt Recent Files chống soi, Tắt Bing Search & Khôi phục Menu Win 10!");
                }
                catch { }

                await SystemCore.RestartExplorerAsync();
            });
        }

        // ================= 9. UWP DEBLOAT & ONEDRIVE =================
        public async Task DebloatUwpAppsAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Đang gỡ sạch ứng dụng rác UWP, Gói Sticky Notes, Xbox, OneDrive & WebExperience...");
            string[] appsToRemove = {
                "*MicrosoftWindows.Client.WebExperience*", "*WebExperience*",
                "*MicrosoftStickyNotes*", "*StickyNotes*",
                "*Microsoft.GamingApp*", "*XboxApp*", "*XboxGamingOverlay*", "*XboxSpeechToTextOverlay*", "*XboxIdentityProvider*", "*Xbox*",
                "*Microsoft.OutlookForWindows*", "*OutlookForWindows*",
                "*LinkedIn*", "*Clipchamp*",
                "*Microsoft.Todos*", "*Todos*",
                "*Microsoft.People*", "*People*",
                "*Microsoft.SkypeApp*", "*SkypeApp*",
                "*Microsoft.GetHelp*", "*GetHelp*", "*Getstarted*", "*WindowsTips*",
                "*Microsoft.BingNews*", "*Microsoft.BingWeather*", "*Microsoft.BingFinance*", "*Microsoft.BingSports*", "*BingSearch*",
                "*Microsoft.MicrosoftSolitaireCollection*", "*Solitaire*",
                "*Microsoft.YourPhone*", "*YourPhone*",
                "*Microsoft.WindowsMaps*", "*WindowsMaps*",
                "*Microsoft.ZuneMusic*", "*Microsoft.ZuneVideo*",
                "*3DBuilder*", "*Microsoft3DViewer*",
                "*FeedbackHub*", "*WindowsFeedbackHub*",
                "*MixedReality.Portal*", "*MixedReality*",
                "*Microsoft.Windows.DevHome*", "*DevHome*",
                "*MicrosoftCorporationII.MicrosoftFamily*", "*MicrosoftFamily*",
                "*MicrosoftCorporationII.QuickAssist*", "*QuickAssist*",
                "*Cortana*",
                "*Spotify*", "*Disney*", "*TikTok*", "*Instagram*", "*Facebook*", "*Amazon*"
            };

            progress?.Report(10);
            string appsList = string.Join(",", appsToRemove.Select(a => $"'{a}'"));
            
            string psScript = $@"
$ErrorActionPreference = 'SilentlyContinue'
$apps = @({appsList})

# 1. Gỡ bỏ Packages của người dùng và Provisioned Packages (gốc hệ thống)
foreach ($app in $apps) {{
    Get-AppxPackage -AllUsers | Where-Object {{ $_.Name -like $app -or $_.PackageFullName -like $app }} | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -like $app -or $_.PackageName -like $app }} | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue
}}

# 2. Gỡ bỏ hoàn toàn OneDrive
taskkill /f /im OneDrive.exe 2>$null
if (Test-Path ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"") {{
    Start-Process ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"" -ArgumentList ""/uninstall /silent"" -Wait -NoNewWindow
}} elseif (Test-Path ""$env:SystemRoot\System32\OneDriveSetup.exe"") {{
    Start-Process ""$env:SystemRoot\System32\OneDriveSetup.exe"" -ArgumentList ""/uninstall /silent"" -Wait -NoNewWindow
}}
Remove-Item -Recurse -Force ""$env:UserProfile\OneDrive"" 2>$null
Remove-Item -Recurse -Force ""$env:LocalAppData\Microsoft\OneDrive"" 2>$null
Remove-Item -Recurse -Force ""$env:ProgramData\Microsoft OneDrive"" 2>$null
Remove-Item -Path ""HKCR:\CLSID\{{018D5C66-4533-4307-9B53-224DE2ED1FE6}}"" -Recurse -Force 2>$null
Remove-Item -Path ""HKCR:\Wow6432Node\CLSID\{{018D5C66-4533-4307-9B53-224DE2ED1FE6}}"" -Recurse -Force 2>$null

# 3. Tắt sạch cài ngầm app rác/quảng cáo Start Menu của Windows 11
$cdmPath = ""HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager""
Set-ItemProperty -Path $cdmPath -Name ""ContentDeliveryAllowed"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty -Path $cdmPath -Name ""OemPreInstalledAppsEnabled"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty -Path $cdmPath -Name ""PreInstalledAppsEnabled"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty -Path $cdmPath -Name ""PreInstalledAppsEverEnabled"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty -Path $cdmPath -Name ""SilentInstalledAppsEnabled"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty -Path $cdmPath -Name ""SubscribedContent-338388Enabled"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty -Path $cdmPath -Name ""SubscribedContent-338389Enabled"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty -Path $cdmPath -Name ""SubscribedContent-353698Enabled"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty -Path $cdmPath -Name ""SystemPaneSuggestionsEnabled"" -Value 0 -Type DWord -ErrorAction SilentlyContinue
";

            await SystemCore.RunPowerShellScriptAsync(psScript, "debloat_master");
            progress?.Report(100);

            Logger.Success("[Optimizer] Đã gỡ sạch Bloatware (OneDrive, Sticky Notes, Xbox, Get Started, News, Widgets...)!");
        }

        // ================= 10. FIX WINDOWS UPDATE =================
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

        // ================= 12. PRIVACY & GAME DVR HARDENING =================
        public async Task OptimizePrivacyAndGameDvrAsync()
        {
            Logger.Info("[Optimizer] Tinh chỉnh Quyền riêng tư & Tắt Xbox Game DVR ngốn FPS...");
            await Task.Run(() =>
            {
                try
                {
                    // 1. Tắt Activity History / Timeline
                    using (var sysKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System"))
                    {
                        sysKey.SetValue("PublishUserActivities", 0, RegistryValueKind.DWord);
                        sysKey.SetValue("UploadUserActivities", 0, RegistryValueKind.DWord);
                        sysKey.SetValue("EnableActivityFeed", 0, RegistryValueKind.DWord);
                    }

                    // 2. Tắt Advertising ID
                    using (var adKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo"))
                    {
                        adKey.SetValue("Enabled", 0, RegistryValueKind.DWord);
                    }
                    using (var adPolKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo"))
                    {
                        adPolKey.SetValue("DisabledByGroupPolicy", 1, RegistryValueKind.DWord);
                    }

                    // 3. Tắt Location Tracking (Định vị chạy ngầm)
                    using (var locKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors"))
                    {
                        locKey.SetValue("DisableLocation", 1, RegistryValueKind.DWord);
                        locKey.SetValue("DisableLocationScripting", 1, RegistryValueKind.DWord);
                    }

                    // 4. Giảm Diagnostic Data & Feedback
                    using (var siufKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Siuf\Rules"))
                    {
                        siufKey.SetValue("NumberOfSIUFInPeriod", 0, RegistryValueKind.DWord);
                    }
                    using (var dataKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
                    {
                        dataKey.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
                        dataKey.SetValue("MaxTelemetryAllowed", 0, RegistryValueKind.DWord);
                    }

                    // 5. Tắt Xbox Game Bar DVR Background Recording (Gỡ gánh nặng FPS cho GPU)
                    using (var gameCfg = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore"))
                    {
                        gameCfg.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
                    }
                    using (var dvrPol = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR"))
                    {
                        dvrPol.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord);
                    }
                    using (var dvrKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR"))
                    {
                        dvrKey.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
                        dvrKey.SetValue("HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
                    }

                    Logger.Success("[Optimizer] Đã tắt Activity History, Advertising ID, Định vị ngầm & Xbox Game DVR thành công!");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Lỗi khi tối ưu Privacy/GameDVR: {ex.Message}");
                }
            });
        }

        // ================= 13. CREATE RESTORE POINT =================
        public async Task<bool> CreateRestorePointAsync()
        {
            Logger.Info("[Optimizer] Đang tạo System Restore Point (Điểm khôi phục hệ thống)...");
            return await Task.Run(async () =>
            {
                try
                {
                    string psScript = @"
try {
    Set-Service -Name sr -StartupType Automatic -ErrorAction SilentlyContinue
    Start-Service -Name sr -ErrorAction SilentlyContinue
    Enable-ComputerRestore -Drive 'C:\' -ErrorAction SilentlyContinue
    Checkpoint-Computer -Description 'CMD_BOX_SafeBackup' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop
    Write-Output 'SUCCESS'
} catch {
    Write-Output $_.Exception.Message
}
";
                    string output = await ProcessRunner.RunCommandAndGetOutputAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"");
                    if (output.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Success("[Optimizer] Đã tạo System Restore Point 'CMD_BOX_SafeBackup' thành công!");
                        return true;
                    }
                    else
                    {
                        Logger.Warning($"[Optimizer] Kết quả tạo Restore Point: {output.Trim()}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Optimizer] Không thể tạo Restore Point: {ex.Message}");
                    return false;
                }
            });
        }

        // ================= 14. SYSTEM INTEGRITY REPAIR (SFC & DISM) =================
        public async Task RepairSystemIntegrityAsync(IProgress<int>? progress = null)
        {
            Logger.Info("[Optimizer] Bắt đầu quét và tự sửa lỗi file hệ thống (DISM & SFC)...");
            progress?.Report(10);

            // 1. DISM RestoreHealth
            Logger.Info("[Optimizer] Bước 1/2: Đang chạy DISM RestoreHealth (khôi phục kho tệp gốc từ Windows Update)...");
            await ProcessRunner.RunProcessAsync(
                "dism.exe",
                "/Online /Cleanup-Image /RestoreHealth",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[DISM] {line}"); },
                runAsAdmin: true
            );
            progress?.Report(60);

            // 2. SFC Scannow
            Logger.Info("[Optimizer] Bước 2/2: Đang chạy SFC Scannow (kiểm tra và tự vá các file hỏng)...");
            await ProcessRunner.RunProcessAsync(
                "sfc.exe",
                "/scannow",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[SFC] {line}"); },
                runAsAdmin: true
            );
            progress?.Report(100);

            Logger.Success("[Optimizer] Quá trình quét và vá file hệ thống hoàn tất! Kiểm tra chi tiết log ở trên.");
        }

        // ================= 15. WINDOWS UPDATE CONTROLLER =================
        public async Task PauseWindowsUpdateAsync()
        {
            Logger.Info("[Optimizer] Đang tạm dừng dịch vụ Windows Update...");
            await Task.Run(async () =>
            {
                try
                {
                    using (var auKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU"))
                    {
                        auKey.SetValue("NoAutoUpdate", 1, RegistryValueKind.DWord);
                        auKey.SetValue("AUOptions", 2, RegistryValueKind.DWord);
                    }

                    using (var uxKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings"))
                    {
                        uxKey.SetValue("PauseFeatureUpdatesEndTime", "2038-01-01T00:00:00Z", RegistryValueKind.String);
                        uxKey.SetValue("PauseQualityUpdatesEndTime", "2038-01-01T00:00:00Z", RegistryValueKind.String);
                        uxKey.SetValue("PauseUpdatesExpiryTime", "2038-01-01T00:00:00Z", RegistryValueKind.String);
                    }

                    await ProcessRunner.RunProcessAsync("cmd.exe", "/c sc stop wuauserv >nul 2>&1 & sc config wuauserv start=disabled >nul 2>&1 & sc stop UsoSvc >nul 2>&1 & sc config UsoSvc start=disabled >nul 2>&1", runAsAdmin: true);

                    Logger.Success("[Optimizer] Đã tạm dừng Windows Update! Máy tính sẽ không tự tải hay ép reboot.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Lỗi khi tạm dừng Windows Update: {ex.Message}");
                }
            });
        }

        public async Task ResumeWindowsUpdateAsync()
        {
            Logger.Info("[Optimizer] Đang khôi phục lại dịch vụ Windows Update...");
            await Task.Run(async () =>
            {
                try
                {
                    using (var auKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU"))
                    {
                        auKey.SetValue("NoAutoUpdate", 0, RegistryValueKind.DWord);
                        auKey.SetValue("AUOptions", 3, RegistryValueKind.DWord);
                    }

                    using (var uxKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings"))
                    {
                        try { uxKey.DeleteValue("PauseFeatureUpdatesEndTime", false); } catch { }
                        try { uxKey.DeleteValue("PauseQualityUpdatesEndTime", false); } catch { }
                        try { uxKey.DeleteValue("PauseUpdatesExpiryTime", false); } catch { }
                    }

                    await ProcessRunner.RunProcessAsync("cmd.exe", "/c sc config wuauserv start=auto >nul 2>&1 & sc start wuauserv >nul 2>&1 & sc config UsoSvc start=delayed-auto >nul 2>&1 & sc start UsoSvc >nul 2>&1", runAsAdmin: true);

                    Logger.Success("[Optimizer] Đã khôi phục Windows Update hoạt động bình thường!");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Lỗi khi khôi phục Windows Update: {ex.Message}");
                }
            });
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

        public async Task<int> ToggleServiceStateAsync(string serviceName, bool disable)
        {
            return await ApplyServiceStartupModeAsync(serviceName, disable ? Models.ServiceStartupMode.Disabled : Models.ServiceStartupMode.Manual);
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

        private static readonly (string AppName, string PackagePattern, string Category, string Description, string EstimatedSize, string RiskLevel)[] BloatwareCatalog = new[]
        {
            // SAFE: An toàn gỡ bỏ 100%
            ("Cortana", "Microsoft.549981C3F5F10", "Trợ Lý Ảo", "Trợ lý ảo Cortana đã dừng hỗ trợ, chạy ngầm ngốn tài nguyên.", "~60 MB", "Safe"),
            ("3D Viewer", "Microsoft.Microsoft3DViewer", "Đồ Họa", "Xem mô hình 3D mặc định ít người dùng phổ thông cần tới.", "~120 MB", "Safe"),
            ("3D Builder", "Microsoft.3DBuilder", "Đồ Họa", "Tạo và in mô hình 3D mặc định.", "~85 MB", "Safe"),
            ("MSN Weather (Thời Tiết)", "Microsoft.BingWeather", "Tin Tức", "Ứng dụng Thời tiết MSN liên tục kết nối mạng cập nhật.", "~90 MB", "Safe"),
            ("MSN News (Tin Tức)", "Microsoft.BingNews", "Tin Tức", "Tin tức MSN cập nhật và đẩy thông báo ngầm.", "~80 MB", "Safe"),
            ("MSN Finance (Tài Chính)", "Microsoft.BingFinance", "Tin Tức", "Theo dõi thị trường chứng khoán MSN.", "~75 MB", "Safe"),
            ("MSN Sports (Thể Thao)", "Microsoft.BingSports", "Tin Tức", "Bản tin thể thao MSN.", "~70 MB", "Safe"),
            ("Solitaire Collection", "Microsoft.MicrosoftSolitaireCollection", "Game Rác", "Bộ game bài Solitaire cài sẵn kèm quảng cáo.", "~140 MB", "Safe"),
            ("Candy Crush Saga", "king.com.CandyCrush", "Game Rác", "Game tài trợ tự động cài ngầm khi cài đặt Windows.", "~250 MB", "Safe"),
            ("Movies & TV (Zune Video)", "Microsoft.ZuneVideo", "Media", "Trình xem video mặc định của Windows.", "~80 MB", "Safe"),
            ("People (Danh Bạ)", "Microsoft.People", "Danh Bạ", "Danh bạ liên lạc tích hợp Skype/Mail không cần thiết.", "~40 MB", "Safe"),
            ("Feedback Hub", "Microsoft.WindowsFeedbackHub", "Telemetry", "Trung tâm gửi phản hồi và dữ liệu người dùng lên Microsoft.", "~55 MB", "Safe"),
            ("Skype", "Microsoft.SkypeApp", "Liên Lạc", "Ứng dụng nhắn tin / gọi video Skype.", "~110 MB", "Safe"),
            ("Microsoft To Do", "Microsoft.Todos", "Ghi Chú", "Quản lý danh sách công việc To-Do của Microsoft.", "~50 MB", "Safe"),
            ("Clipchamp Video Editor", "Clipchamp.Clipchamp", "Media", "Trình dựng video trực tuyến mặc định trên Win 11.", "~160 MB", "Safe"),
            ("Spotify Music", "SpotifyAB.SpotifyMusic", "Media", "Ứng dụng nghe nhạc Spotify đối tác cài sẵn.", "~150 MB", "Safe"),
            ("Disney+", "Disney", "Đối Tác", "Ứng dụng xem phim Disney+ đối tác cài sẵn.", "~120 MB", "Safe"),
            ("TikTok", "TikTok", "Đối Tác", "Ứng dụng mạng xã hội video TikTok cài sẵn.", "~100 MB", "Safe"),
            ("Amazon Prime Video", "AmazonVideo", "Đối Tác", "Ứng dụng xem phim Amazon Prime Video.", "~90 MB", "Safe"),

            // CAUTION: Cần cân nhắc (Chỉ gỡ nếu thực sự không dùng đến tính năng đó)
            ("Groove Music (Media Player cũ)", "Microsoft.ZuneMusic", "Media", "Trình nghe nhạc mặc định Windows đời cũ.", "~70 MB", "Caution"),
            ("Get Help", "Microsoft.GetHelp", "Tiện Ích", "Trung tâm trợ giúp cơ bản của Windows.", "~45 MB", "Caution"),
            ("Tips (Mẹo Windows)", "Microsoft.Getstarted", "Mẹo Vặt", "Hướng dẫn và gợi ý sử dụng Windows.", "~35 MB", "Caution"),
            ("Phone Link (Liên kết điện thoại)", "Microsoft.YourPhone", "Tiện Ích", "Ứng dụng đồng bộ điện thoại với PC nếu bạn không dùng.", "~95 MB", "Caution"),
            ("Microsoft 365 (Office Hub)", "Microsoft.MicrosoftOfficeHub", "Quảng Cáo", "Cổng giới thiệu và khởi động nhanh bộ Office 365.", "~65 MB", "Caution"),
            ("Power Automate", "Microsoft.PowerAutomateDesktop", "Nền Tảng", "Tự động hóa luồng tác vụ văn phòng và doanh nghiệp.", "~280 MB", "Caution"),
            ("Xbox Apps & Game Bar", "Microsoft.XboxApp", "Xbox", "Ứng dụng Xbox Console Companion & dịch vụ game.", "~180 MB", "Caution"),
            ("Voice Recorder (Ghi Âm)", "Microsoft.WindowsSoundRecorder", "Tiện Ích", "Trình ghi âm giọng nói mặc định qua microphone.", "~30 MB", "Caution")
        };

        public async Task<List<Models.BloatwareAppItem>> ScanInstalledBloatwareAsync()
        {
            var detectedList = new List<Models.BloatwareAppItem>();
            try
            {
                var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Quét an toàn theo chuẩn optimizerDuck: Loại trừ NonRemovable và Frameworks (.NET, VCLibs, WinAppRuntime)
                await ProcessRunner.RunProcessAsync(
                    "powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage | Where-Object { $_.NonRemovable -eq $false -and $_.IsFramework -eq $false } | Select-Object -ExpandProperty Name\"",
                    onOutputLine: line =>
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            installedNames.Add(line.Trim());
                        }
                    },
                    runAsAdmin: false
                );

                foreach (var entry in BloatwareCatalog)
                {
                    bool isInstalled = installedNames.Any(name => name.Contains(entry.PackagePattern, StringComparison.OrdinalIgnoreCase));
                    if (isInstalled)
                    {
                        detectedList.Add(new Models.BloatwareAppItem
                        {
                            AppName = entry.AppName,
                            PackageName = entry.PackagePattern,
                            Category = entry.Category,
                            Description = entry.Description,
                            EstimatedSize = entry.EstimatedSize,
                            RiskLevel = entry.RiskLevel,
                            IsSelected = entry.RiskLevel == "Safe", // Mặc định chỉ tích chọn trước các app Safe (An toàn)
                            IsInstalled = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Lỗi khi quét ứng dụng UWP đã cài: {ex.Message}");
            }

            return detectedList;
        }

        public async Task<bool> RemoveBloatwareBatchAsync(IEnumerable<string> packagePatterns)
        {
            try
            {
                var patternsList = packagePatterns.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                if (patternsList.Count == 0) return true;

                string patternsFormatted = string.Join(",", patternsList.Select(p => $"'{p.Trim('*')}'"));
                string script = $"$patterns = @({patternsFormatted}); foreach ($p in $patterns) {{ Get-AppxPackage | Where-Object {{ $_.Name -like \"*$p*\" }} | Remove-AppxPackage -ErrorAction SilentlyContinue }}";

                int code = await ProcessRunner.RunProcessAsync(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    runAsAdmin: false
                );
                return code == 0;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Lỗi gỡ bỏ ứng dụng UWP: {ex.Message}");
                return false;
            }
        }
    }
}
