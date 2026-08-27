using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;
using Microsoft.Win32;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly DispatcherTimer _autoRefreshTimer;

        public DashboardView()
        {
            InitializeComponent();

            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoRefreshTimer.Tick += async (_, _) => await RefreshDashboardDataAsync();

            Loaded += async (_, _) =>
            {
                await RefreshDashboardDataAsync();
                _autoRefreshTimer.Start();
            };

            Unloaded += (_, _) =>
            {
                _autoRefreshTimer.Stop();
            };
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDashboardDataAsync();
        }

        public async Task RefreshDashboardDataAsync()
        {
            TxtLastUpdated.Text = $"Cập nhật: {DateTime.Now:HH:mm:ss}";

            await Task.Run(() =>
            {
                // 1. Thông tin RAM
                ulong totalRam = 0;
                ulong availRam = 0;
                double ramUsedPercent = 0;
                try
                {
                    var memStatus = new NativeMethods.MEMORYSTATUSEX();
                    if (NativeMethods.GlobalMemoryStatusEx(memStatus))
                    {
                        totalRam = memStatus.ullTotalPhys;
                        availRam = memStatus.ullAvailPhys;
                        ulong usedRam = totalRam > availRam ? totalRam - availRam : 0;
                        ramUsedPercent = totalRam > 0 ? (double)usedRam / totalRam * 100.0 : 0;
                    }
                }
                catch { }

                // 2. Thông tin CPU & Phần cứng
                string cpuName = GetCpuName();
                int cpuCores = Environment.ProcessorCount;
                string osDesc = RuntimeInformation.OSDescription;
                string osArch = RuntimeInformation.OSArchitecture.ToString();
                string machineName = Environment.MachineName;
                string currentUser = $"{Environment.UserDomainName}\\{Environment.UserName}";
                string dotNetVer = RuntimeInformation.FrameworkDescription;

                // 3. Thời gian Uptime & Quyền
                TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                string uptimeText = $"{uptime.Days} ngày {uptime.Hours} giờ {uptime.Minutes} phút";
                bool isAdmin = SystemCore.IsAdministrator();
                int processCount = 0;
                try
                {
                    processCount = Process.GetProcesses().Length;
                }
                catch { }

                // 4. Danh sách các Ổ Đĩa
                var driveList = new List<DriveStorageInfo>();
                try
                {
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (!drive.IsReady) continue;

                        long total = drive.TotalSize;
                        long free = drive.AvailableFreeSpace;
                        long used = total > free ? total - free : 0;
                        double percent = total > 0 ? (double)used / total * 100.0 : 0;
                        string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
                        string format = string.IsNullOrWhiteSpace(drive.DriveFormat) ? "NTFS" : drive.DriveFormat;

                        driveList.Add(new DriveStorageInfo
                        {
                            Name = drive.Name,
                            Label = label,
                            DriveType = drive.DriveType.ToString(),
                            Format = format,
                            TotalBytes = total,
                            FreeBytes = free,
                            UsedBytes = used,
                            UsedPercent = percent,
                            TotalFormatted = SystemCore.FormatBytes(total),
                            FreeFormatted = SystemCore.FormatBytes(free),
                            UsedFormatted = SystemCore.FormatBytes(used),
                            SummaryText = $"{SystemCore.FormatBytes(used)} / {SystemCore.FormatBytes(total)} ({percent:0.#}%)"
                        });
                    }
                }
                catch { }

                // 5. Nguồn & Pin
                bool hasPower = NativeMethods.GetSystemPowerStatus(out var sps);

                // 6. Mạng & IP
                var netInfo = GetActiveNetworkInfo();

                // Cập nhật giao diện trên Dispatcher
                Dispatcher.Invoke(() =>
                {
                    // Update RAM
                    if (totalRam > 0)
                    {
                        ulong usedRam = totalRam - availRam;
                        TxtRamSummary.Text = $"{SystemCore.FormatBytes((long)usedRam)} / {SystemCore.FormatBytes((long)totalRam)}";
                        PbRam.Value = ramUsedPercent;
                        TxtRamDetail.Text = $"Trống: {SystemCore.FormatBytes((long)availRam)} ({100 - ramUsedPercent:0.#}% khả dụng)";
                    }

                    // Update CPU
                    TxtCpuCores.Text = $"{cpuCores} Threads / Nhân";
                    TxtCpuShortName.Text = CleanCpuShortName(cpuName);
                    TxtCpuArch.Text = $"Kiến trúc: {osArch}";

                    // Update Uptime & Quyền
                    TxtUptime.Text = uptimeText;
                    TxtAdminStatus.Text = isAdmin ? "🛡️ Quyền: Administrator" : "⚠️ Quyền: Standard User";
                    TxtAdminStatus.Foreground = isAdmin ? (System.Windows.Media.Brush)FindResource("AccentSuccess") : (System.Windows.Media.Brush)FindResource("AccentWarning");
                    TxtProcessCount.Text = $"{processCount} tiến trình đang chạy";

                    // Update Power
                    if (hasPower)
                    {
                        if (sps.BatteryFlag == 128)
                        {
                            TxtPowerStatus.Text = "PC (AC Online)";
                            PbBattery.Value = 100;
                            TxtBatteryDetail.Text = "Nguồn điện trực tiếp 220V";
                        }
                        else
                        {
                            int pct = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 0;
                            bool charging = (sps.BatteryFlag & 8) != 0;
                            TxtPowerStatus.Text = charging ? $"⚡ Sạc ({pct}%)" : $"🔋 {pct}% (Pin)";
                            PbBattery.Value = pct;
                            TxtBatteryDetail.Text = sps.ACLineStatus == 1 ? "Đang cắm sạc nguồn" : "Đang dùng nguồn pin";
                        }
                    }

                    // Update Drives List
                    IcDrives.ItemsSource = driveList;

                    // Update System Specs
                    TxtMachineName.Text = machineName;
                    TxtCpuFullName.Text = cpuName;
                    TxtOsVersion.Text = osDesc;
                    TxtOsArchitecture.Text = $"{osArch} ({ (Environment.Is64BitOperatingSystem ? "64-bit OS" : "32-bit OS") })";
                    TxtCurrentUser.Text = currentUser;
                    TxtDotNetVersion.Text = dotNetVer;

                    // Update Network Specs
                    TxtNetAdapter.Text = netInfo.AdapterName;
                    TxtNetIpv4.Text = netInfo.Ipv4;
                    TxtNetMac.Text = netInfo.Mac;
                    TxtNetGateway.Text = netInfo.Gateway;
                    TxtNetStatus.Text = netInfo.IsConnected ? "🟢 Đã kết nối Internet" : "🔴 Mất kết nối mạng";
                });
            });
        }

        private static string GetCpuName()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string name)
                {
                    return name.Trim();
                }
            }
            catch { }

            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Generic CPU";
        }

        private static string CleanCpuShortName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "CPU";
            return fullName.Replace("(R)", "").Replace("(TM)", "").Replace("CPU", "").Trim();
        }

        private static (string AdapterName, string Ipv4, string Mac, string Gateway, bool IsConnected) GetActiveNetworkInfo()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                var active = interfaces.FirstOrDefault(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

                if (active != null)
                {
                    var ipProps = active.GetIPProperties();
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "N/A";
                    var mac = string.Join(":", active.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                    var gateway = ipProps.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "N/A";
                    bool isConnected = NetworkInterface.GetIsNetworkAvailable();

                    return (active.Name, ipv4, mac, gateway, isConnected);
                }
            }
            catch { }

            return ("Không tìm thấy", "N/A", "N/A", "N/A", false);
        }
    }
}
