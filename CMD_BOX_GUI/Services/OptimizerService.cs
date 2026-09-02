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

        // ================= 10. BITLOCKER CHECK & DISABLE =================
        public async Task CheckAndDisableBitLockerAsync()
        {
            Logger.Info("[BitLocker] Đang kiểm tra trạng thái mã hóa ổ đĩa BitLocker...");
            await Task.Run(async () =>
            {
                try
                {
                    string statusOutput = await ProcessRunner.RunCommandAndGetOutputAsync("manage-bde.exe", "-status");
                    if (!string.IsNullOrWhiteSpace(statusOutput) && 
                        (statusOutput.Contains("Protection On", StringComparison.OrdinalIgnoreCase) || 
                         statusOutput.Contains("Fully Encrypted", StringComparison.OrdinalIgnoreCase) ||
                         statusOutput.Contains("Encryption in Progress", StringComparison.OrdinalIgnoreCase)))
                    {
                        Logger.Warning("[BitLocker] Phát hiện ổ đĩa đang bật BitLocker làm giảm tốc độ SSD! Đang tự động giải mã và tắt...");
                        string bitLockerScript = "Get-BitLockerVolume | Where-Object { $_.ProtectionStatus -eq 'On' } | Disable-BitLocker -EA SilentlyContinue; manage-bde.exe -off C:";
                        await SystemCore.RunPowerShellScriptAsync(bitLockerScript, "disable_bitlocker");
                        Logger.Success("[BitLocker] Đã gửi lệnh tắt BitLocker và giải mã ổ đĩa thành công! Tốc độ SSD sẽ được khôi phục 100%.");
                    }
                    else
                    {
                        Logger.Info("[BitLocker] Ổ đĩa không bật BitLocker (Tốc độ SSD đạt chuẩn tối đa).");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[BitLocker] Lỗi kiểm tra BitLocker: {ex.Message}");
                }
            });
        }

        // ================= 11. MASTER MAKE WIN (1-CLICK ALL-IN-ONE) =================
        public async Task MasterMakeWinAsync(IProgress<int>? progress = null)
        {
            Logger.Info("🚀 [MAKE WIN] BẮT ĐẦU TỐI ƯU TOÀN DIỆN 1-CLICK...");

            progress?.Report(10);
            await OptimizeTaskbarWindows11Async();

            progress?.Report(30);
            await OptimizeServicesAsync();

            progress?.Report(50);
            await OptimizeSystemProAsync();

            progress?.Report(70);
            await DebloatUwpAppsAsync();

            progress?.Report(85);
            await CheckAndDisableBitLockerAsync();

            progress?.Report(95);
            await SystemCore.RestartExplorerAsync();

            progress?.Report(100);
            Logger.Success("🎉 [MAKE WIN] HOÀN TẤT TOÀN BỘ CHUỖI TINH CHỈNH!");
            Logger.Success("Windows đã được gỡ Widgets, tắt BitLocker, dọn Taskbar, tắt Bing, tắt Service rác & tăng tốc tức thì!");
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

        // ================= 11. STARTUP GREETING (LỜI CHÀO WINDOWS) =================
        public (string caption, string text) GetStartupGreeting()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                if (key != null)
                {
                    string caption = key.GetValue("legalnoticecaption")?.ToString() ?? "";
                    string text = key.GetValue("legalnoticetext")?.ToString() ?? "";
                    return (caption, text);
                }
            }
            catch { }
            return ("", "");
        }

        public async Task<bool> SetStartupGreetingAsync(string caption, string text)
        {
            Logger.Info($"[Optimizer] Cấu hình lời chào Windows: [{caption}]...");
            return await Task.Run(() =>
            {
                try
                {
                    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                    if (key != null)
                    {
                        key.SetValue("legalnoticecaption", caption ?? "", RegistryValueKind.String);
                        key.SetValue("legalnoticetext", text ?? "", RegistryValueKind.String);
                        Logger.Success("[Optimizer] Đã lưu lời chào Windows thành công! (Sẽ hiển thị khi mở máy)");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Lỗi khi ghi lời chào Registry: {ex.Message}");
                }
                return false;
            });
        }

        public async Task<bool> ClearStartupGreetingAsync()
        {
            Logger.Info("[Optimizer] Đang xóa lời chào Windows...");
            return await SetStartupGreetingAsync("", "");
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
    }
}
