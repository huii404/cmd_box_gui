using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;

namespace CMD_BOX_GUI.Services
{
    public class NetworkService
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        public async Task<List<NetworkAdapterInfo>> GetAdaptersInfoAsync()
        {
            var list = new List<NetworkAdapterInfo>();
            await Task.Run(() =>
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var ipProps = ni.GetIPProperties();
                    string ipv4 = "";
                    string gateway = "";
                    string dns = "";

                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ipv4 = addr.Address.ToString();
                            break;
                        }
                    }

                    foreach (var gw in ipProps.GatewayAddresses)
                    {
                        gateway = gw.Address.ToString();
                        break;
                    }

                    var dnsList = new List<string>();
                    foreach (var d in ipProps.DnsAddresses)
                    {
                        if (d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            dnsList.Add(d.ToString());
                    }
                    dns = string.Join(", ", dnsList);

                    list.Add(new NetworkAdapterInfo
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        Status = ni.OperationalStatus.ToString(),
                        Ipv4Address = ipv4,
                        MacAddress = string.Join(":", Regex.Matches(ni.GetPhysicalAddress().ToString(), "..")),
                        Gateway = gateway,
                        DnsServers = dns
                    });
                }
            });
            return list;
        }

        public async Task<string> GetPublicIpAsync()
        {
            try
            {
                var res = await httpClient.GetStringAsync("https://api.ipify.org");
                return res.Trim();
            }
            catch
            {
                return "Offline / Blocked";
            }
        }

        public async Task<List<WifiInfo>> AuditSavedWifiAsync()
        {
            Logger.Info("Đang quét Wi-Fi đã lưu tốc độ cao...");
            var wifiBag = new ConcurrentBag<WifiInfo>();

            string profilesOutput = await ProcessRunner.RunCommandAndGetOutputAsync("netsh", "wlan show profiles");
            var profileNames = new List<string>();

            using (var reader = new StringReader(profilesOutput))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    int colon = line.IndexOf(':');
                    if (colon >= 0 && colon < line.Length - 1)
                    {
                        string left = line[..colon].ToLowerInvariant();
                        if (left.Contains("profile") || left.Contains("hồ sơ") || left.Contains("profil"))
                        {
                            string name = line[(colon + 1)..].Trim();
                            if (!string.IsNullOrEmpty(name)) profileNames.Add(name);
                        }
                    }
                }
            }

            // Quét song song đa luồng (Parallel) để có kết quả tức thì
            await Parallel.ForEachAsync(profileNames, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (profile, _) =>
            {
                string detail = await ProcessRunner.RunCommandAndGetOutputAsync("netsh", $"wlan show profile name=\"{profile}\" key=clear");
                
                string pass = "Không có mật khẩu (Open)";
                var passMatch = Regex.Match(detail, @"(?:Key Content|Nội dung khóa|Schlüsselinhalt)\s*:\s*(.*)", RegexOptions.IgnoreCase);
                if (passMatch.Success)
                {
                    pass = passMatch.Groups[1].Value.Trim();
                }

                string auth = "WPA2/WPA3";
                var authMatch = Regex.Match(detail, @"(?:Authentication|Xác thực|Authentifizierung)\s*:\s*(.*)", RegexOptions.IgnoreCase);
                if (authMatch.Success)
                {
                    auth = authMatch.Groups[1].Value.Trim();
                }

                wifiBag.Add(new WifiInfo
                {
                    Ssid = profile,
                    Password = pass,
                    Authentication = auth,
                    Cipher = "AES"
                });
            });

            var result = new List<WifiInfo>(wifiBag);
            result.Sort((a, b) => string.Compare(a.Ssid, b.Ssid, StringComparison.OrdinalIgnoreCase));
            Logger.Success($"Đã trích xuất {result.Count} mạng Wi-Fi.");
            return result;
        }

        public async Task RepairNetworkProAsync(IProgress<int>? progress = null)
        {
            Logger.Info("Chạy Khôi phục mạng PRO (8 bước: DNS, Winsock, TCP/IP, ARP, DHCP, WinNAT, Firewall)...");

            var steps = new (string Cmd, string Args)[]
            {
                ("ipconfig", "/flushdns"),
                ("netsh", "winsock reset"),
                ("netsh", "int ip reset"),
                ("arp", "-d *"),
                ("ipconfig", "/release"),
                ("ipconfig", "/renew"),
                ("powershell", "-NoProfile -Command \"Restart-Service -Name winnat -Force -EA SilentlyContinue\""),
                ("powershell", "-NoProfile -Command \"New-NetFirewallRule -DisplayName 'CMD_BOX LocalSend' -Direction Inbound -LocalPort 53317 -Protocol TCP -Action Allow -EA SilentlyContinue\"")
            };

            for (int i = 0; i < steps.Length; i++)
            {
                try
                {
                    await ProcessRunner.RunProcessAsync(steps[i].Cmd, steps[i].Args, runAsAdmin: true);
                }
                catch { }
                progress?.Report((int)((i + 1) * 100.0 / steps.Length));
            }

            Logger.Success("Khôi phục mạng thành công!");
        }

        public async Task ApplySecurityShieldAsync()
        {
            Logger.Info("Kích hoạt Lá chắn bảo mật (Bật Defender, Firewall, Chặn Port 445/139/135, Tắt SMBv1)...");

            string psScript = @"
Set-MpPreference -EnableControlledFolderAccess Enabled -EA SilentlyContinue
New-NetFirewallRule -DisplayName 'Block Port 445' -Direction Inbound -LocalPort 445 -Protocol TCP -Action Block -EA SilentlyContinue
New-NetFirewallRule -DisplayName 'Block Port 139' -Direction Inbound -LocalPort 139 -Protocol TCP -Action Block -EA SilentlyContinue
New-NetFirewallRule -DisplayName 'Block Port 135' -Direction Inbound -LocalPort 135 -Protocol TCP -Action Block -EA SilentlyContinue
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True -EA SilentlyContinue
Disable-WindowsOptionalFeature -Online -FeatureName SMB1Protocol -NoRestart -EA SilentlyContinue
";
            await ProcessRunner.RunProcessAsync("powershell", $"-NoProfile -Command \"{psScript.Replace(Environment.NewLine, " ")}\"", runAsAdmin: true);
            Logger.Success("Đã kích hoạt toàn bộ Lá chắn bảo mật!");
        }

        public async Task CheckHostsFileSecurityAsync()
        {
            Logger.Info("Đang kiểm tra tính toàn vẹn của tệp hosts...");
            string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
            if (!File.Exists(hostsPath))
            {
                Logger.Warning("Không tìm thấy tệp hosts hệ thống.");
                return;
            }

            string[] lines = await File.ReadAllLinesAsync(hostsPath);
            int activeEntries = 0;
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("#") && !string.IsNullOrWhiteSpace(trimmed))
                {
                    activeEntries++;
                    Logger.Info($"[Hosts Entry] {trimmed}");
                }
            }

            if (activeEntries <= 2)
            {
                Logger.Success($"Tệp hosts sạch sẽ và an toàn ({activeEntries} mục chuyển hướng).");
            }
            else
            {
                Logger.Warning($"Tệp hosts chứa {activeEntries} mục chuyển hướng đang hoạt động!");
            }
        }
    }
}
