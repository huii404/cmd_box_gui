using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };

        // 1. LẤY THÔNG TIN MẠNG HIỆN TẠI (LAN, GATEWAY, DNS, PUBLIC IP)
        public async Task<(string LocalIp, string Gateway, string Dns, string AdapterName)> GetCurrentNetworkInfoAsync()
        {
            return await Task.Run(() =>
            {
                string localIp = "Chưa kết nối";
                string gateway = "N/A";
                string dns = "N/A";
                string adapterName = "N/A";

                try
                {
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                    foreach (var ni in interfaces)
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up) continue;
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                        var ipProps = ni.GetIPProperties();
                        if (ipProps.GatewayAddresses.Count == 0) continue;

                        adapterName = ni.Name;

                        foreach (var addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                localIp = addr.Address.ToString();
                                break;
                            }
                        }

                        foreach (var gw in ipProps.GatewayAddresses)
                        {
                            gateway = gw.Address.ToString();
                            break;
                        }

                        var dnsList = new System.Collections.Generic.List<string>();
                        foreach (var d in ipProps.DnsAddresses)
                        {
                            if (d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                dnsList.Add(d.ToString());
                            }
                        }
                        if (dnsList.Count > 0) dns = string.Join(", ", dnsList);

                        break; // Đã tìm thấy card mạng chính
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Lỗi đọc thông tin card mạng: {ex.Message}");
                }

                return (localIp, gateway, dns, adapterName);
            });
        }

        public async Task<string> GetPublicIpAsync()
        {
            try
            {
                var res = await _httpClient.GetStringAsync("https://api.ipify.org");
                return res.Trim();
            }
            catch
            {
                return "Offline / Blocked";
            }
        }

        // 2. TÍNH NĂNG 1: XÓA CACHE DNS (FLUSH DNS)
        public async Task FlushDnsAsync()
        {
            Logger.Info("🧹 Bắt đầu xóa bộ nhớ đệm DNS (Flush DNS)...");
            int code = await ProcessRunner.RunProcessAsync(
                "ipconfig",
                "/flushdns",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[DNS] {line}"); },
                onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[DNS] {err}"); }
            );

            if (code == 0)
                Logger.Success("✅ Đã xóa sạch bộ nhớ đệm DNS (Flush DNS) thành công!");
            else
                Logger.Warning($"⚠️ Lệnh Flush DNS kết thúc với mã: {code}");
        }

        // 3. TÍNH NĂNG 2: LÀM MỚI ĐỊA CHỈ IP (DHCP RELEASE & RENEW)
        public async Task RenewIpAddressAsync()
        {
            Logger.Info("🔄 Đang giải phóng và xin cấp lại địa chỉ IP từ Router (Release / Renew)...");
            
            await ProcessRunner.RunProcessAsync(
                "ipconfig",
                "/release",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[IP] {line}"); },
                onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[IP] {err}"); }
            );

            await Task.Delay(500);

            int code = await ProcessRunner.RunProcessAsync(
                "ipconfig",
                "/renew",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[IP] {line}"); },
                onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[IP] {err}"); }
            );

            if (code == 0)
                Logger.Success("✅ Đã làm mới địa chỉ IP (Release & Renew) thành công!");
            else
                Logger.Warning($"⚠️ Lệnh Renew IP kết thúc với mã: {code}");
        }

        // 4. TÍNH NĂNG 3: ĐẶT LẠI TCP/IP VÀ WINSOCK STACK (YÊU CẦU ADMIN)
        public async Task ResetTcpIpWinsockAsync()
        {
            Logger.Info("⚡ Đang đặt lại giao thức mạng TCP/IP và Winsock Catalog...");

            int codeWinsock = await ProcessRunner.RunProcessAsync(
                "netsh",
                "winsock reset",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[Winsock] {line}"); },
                onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[Winsock] {err}"); },
                runAsAdmin: true
            );

            int codeIp = await ProcessRunner.RunProcessAsync(
                "netsh",
                "int ip reset",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[TCP/IP] {line}"); },
                onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[TCP/IP] {err}"); },
                runAsAdmin: true
            );

            if (codeWinsock == 0 || codeIp == 0)
                Logger.Success("✅ Đã đặt lại TCP/IP & Winsock thành công! (Khuyến nghị khởi động lại máy nếu cần)");
            else
                Logger.Warning("⚠️ Đặt lại TCP/IP & Winsock hoàn tất.");
        }

        // 5. TÍNH NĂNG 4: KHÔI PHỤC WINDOWS FIREWALL VỀ MẶC ĐỊNH (YÊU CẦU ADMIN)
        public async Task ResetFirewallDefaultAsync()
        {
            Logger.Info("🛡️ Đang khôi phục Windows Firewall về cài đặt gốc...");

            int code = await ProcessRunner.RunProcessAsync(
                "netsh",
                "advfirewall reset",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[Firewall] {line}"); },
                onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[Firewall] {err}"); },
                runAsAdmin: true
            );

            if (code == 0)
                Logger.Success("✅ Đã khôi phục Windows Firewall về mặc định của Microsoft!");
            else
                Logger.Warning($"⚠️ Khôi phục Firewall kết thúc với mã: {code}");
        }

        // 6. KHÔI PHỤC TOÀN BỘ (1-CLICK ALL-IN-ONE)
        public async Task RepairAllNetworkAsync()
        {
            Logger.Info("🚀 BẮT ĐẦU CHẠY KHÔI PHỤC MẠNG TOÀN DIỆN 1-CLICK...");

            Logger.Info("👉 [Bước 1/4] Xóa Cache DNS...");
            await FlushDnsAsync();
            await Task.Delay(300);

            Logger.Info("👉 [Bước 2/4] Xin cấp lại địa chỉ IP...");
            await RenewIpAddressAsync();
            await Task.Delay(300);

            Logger.Info("👉 [Bước 3/4] Đặt lại giao thức TCP/IP & Winsock...");
            await ResetTcpIpWinsockAsync();
            await Task.Delay(300);

            Logger.Info("👉 [Bước 4/4] Khôi phục Firewall gốc...");
            await ResetFirewallDefaultAsync();

            Logger.Success("🎉 ĐÃ HOÀN TẤT TOÀN BỘ 4 BƯỚC KHÔI PHỤC MẠNG TOÀN DIỆN!");
        }

        // 7. TÍNH NĂNG 5: QUÉT TOÀN BỘ THIẾT BỊ TRONG MẠNG LAN / WI-FI (LAN SCANNER)
        public async Task<List<LanDeviceItem>> ScanLanDevicesAsync(IProgress<int>? progress = null)
        {
            Logger.Info("📡 [LAN Scanner] Đang bắt đầu quét các thiết bị đang kết nối Wi-Fi/LAN...");
            var deviceList = new List<LanDeviceItem>();

            return await Task.Run(async () =>
            {
                var (localIp, gateway, _, _) = await GetCurrentNetworkInfoAsync();
                if (string.IsNullOrWhiteSpace(localIp) || localIp.Contains("Chưa kết nối") || !localIp.Contains('.'))
                {
                    Logger.Warning("[LAN Scanner] Không tìm thấy kết nối mạng hợp lệ để quét!");
                    return deviceList;
                }

                // Tách dải subnet (Ví dụ: 192.168.1.)
                int lastDotIndex = localIp.LastIndexOf('.');
                string subnetPrefix = localIp.Substring(0, lastDotIndex + 1);

                // 1. Quét Ping nhanh song song để làm mới bảng ARP
                progress?.Report(15);
                var pingTasks = new List<Task>();
                using var semaphore = new System.Threading.SemaphoreSlim(40);

                for (int i = 1; i <= 254; i++)
                {
                    string targetIp = $"{subnetPrefix}{i}";
                    pingTasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            using var p = new Ping();
                            await p.SendPingAsync(targetIp, 200);
                        }
                        catch { }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(pingTasks);
                progress?.Report(60);

                // 2. Đọc bảng ARP từ hệ thống
                string arpOutput = await ProcessRunner.RunCommandAndGetOutputAsync("arp.exe", "-a");
                var ipMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(arpOutput))
                {
                    var lines = arpOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var regex = new Regex(@"^\s*([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)\s+([0-9a-fA-F\-]{17})\s+(\w+)", RegexOptions.Compiled);

                    foreach (var line in lines)
                    {
                        var match = regex.Match(line);
                        if (match.Success)
                        {
                            string ip = match.Groups[1].Value.Trim();
                            string mac = match.Groups[2].Value.Trim().ToUpperInvariant();
                            string type = match.Groups[3].Value.Trim();

                            // Bỏ qua dải broadcast 255 và multicast 224-239
                            if (ip.EndsWith(".255") || ip.StartsWith("224.") || ip.StartsWith("239.") || mac.StartsWith("FF-FF-FF"))
                                continue;

                            if (ip.StartsWith(subnetPrefix) && !ipMap.ContainsKey(ip))
                            {
                                ipMap[ip] = mac;
                            }
                        }
                    }
                }

                progress?.Report(80);

                // Luôn đảm bảo có máy tính hiện tại trong danh sách
                if (!ipMap.ContainsKey(localIp))
                {
                    ipMap[localIp] = GetLocalMacAddress();
                }

                // 3. Phân loại và phân giải Hostname chi tiết
                var analysisTasks = ipMap.Select(async kvp =>
                {
                    string ip = kvp.Key;
                    string mac = kvp.Value;
                    bool isLocal = string.Equals(ip, localIp, StringComparison.OrdinalIgnoreCase);
                    bool isGw = string.Equals(ip, gateway, StringComparison.OrdinalIgnoreCase);

                    string dnsHostName = "";
                    string netBiosName = "";

                    if (!isLocal && !isGw)
                    {
                        // Thử lấy NetBIOS tên máy tính Windows (LAN)
                        netBiosName = await TryGetNetBiosNameAsync(ip);

                        // Thử phân giải DNS
                        if (string.IsNullOrWhiteSpace(netBiosName))
                        {
                            try
                            {
                                var hostEntryTask = Dns.GetHostEntryAsync(ip);
                                if (await Task.WhenAny(hostEntryTask, Task.Delay(150)) == hostEntryTask)
                                {
                                    dnsHostName = hostEntryTask.Result.HostName;
                                    if (dnsHostName == ip) dnsHostName = "";
                                }
                            }
                            catch { }
                        }
                    }

                    var (hostName, deviceType) = ClassifyDevice(ip, mac, isLocal, isGw, netBiosName, dnsHostName);

                    return new LanDeviceItem
                    {
                        IpAddress = ip,
                        MacAddress = mac,
                        HostName = hostName,
                        DeviceType = deviceType,
                        IsLocalDevice = isLocal,
                        IsGateway = isGw,
                        Status = "Online"
                    };
                }).ToList();

                var results = await Task.WhenAll(analysisTasks);
                deviceList.AddRange(results);

                // Sắp xếp: Router đầu tiên, máy tính này thứ hai, còn lại theo thứ tự IP
                var sorted = deviceList
                    .OrderByDescending(d => d.IsGateway)
                    .ThenByDescending(d => d.IsLocalDevice)
                    .ThenBy(d =>
                    {
                        var parts = d.IpAddress.Split('.');
                        return parts.Length == 4 && int.TryParse(parts[3], out int lastOctet) ? lastOctet : 999;
                    })
                    .ToList();

                progress?.Report(100);
                Logger.Success($"📡 [LAN Scanner] Quét hoàn tất! Đã tìm thấy {sorted.Count} thiết bị đang kết nối:");
                for (int i = 0; i < sorted.Count; i++)
                {
                    var d = sorted[i];
                    string branch = (i == sorted.Count - 1) ? "└─" : "├─";
                    Logger.Info($" {branch} [{d.IpAddress,-15}] {d.MacAddress,-17} | {d.DeviceType,-25} | {d.HostName}");
                }
                return sorted;
            });
        }

        private static (string HostName, string DeviceType) ClassifyDevice(string ip, string mac, bool isLocal, bool isGw, string netBios, string dnsHost)
        {
            if (isLocal)
            {
                return (Environment.MachineName, "Máy tính này (This PC)");
            }

            if (isGw)
            {
                string gwName = !string.IsNullOrWhiteSpace(dnsHost) ? dnsHost : "Router / Modem Wi-Fi";
                return (gwName, "Router / Modem Wi-Fi");
            }

            if (!string.IsNullOrWhiteSpace(netBios))
            {
                return (netBios, "Máy tính Windows (LAN)");
            }

            if (!string.IsNullOrWhiteSpace(dnsHost))
            {
                return (dnsHost, "Thiết bị mạng (Đã định danh)");
            }

            // Kiểm tra địa chỉ MAC ngẫu nhiên (Private / Randomized MAC - Chuẩn bảo mật trên iPhone iOS 14+ và Android 10+)
            if (IsRandomizedPrivateMac(mac))
            {
                return ("Ẩn danh (Bảo mật MAC riêng tư)", "Điện thoại (iOS / Android)");
            }

            // Tra cứu hãng sản xuất phần cứng từ 3 byte đầu MAC (OUI)
            return LookupVendorFromMac(mac);
        }

        private static async Task<string> TryGetNetBiosNameAsync(string ip)
        {
            try
            {
                var nbtTask = ProcessRunner.RunCommandAndGetOutputAsync("nbtstat.exe", $"-A {ip}");
                if (await Task.WhenAny(nbtTask, Task.Delay(300)) == nbtTask)
                {
                    string output = nbtTask.Result;
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.Contains("<00>") && line.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 0 && !parts[0].StartsWith("__MSBROWSE__"))
                                {
                                    return parts[0].Trim();
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private static bool IsRandomizedPrivateMac(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac) || mac.Length < 2) return false;
            char secondHex = char.ToUpperInvariant(mac[1]);
            // Theo chuẩn IEEE: Nếu bit thứ 2 của byte đầu = 1 (kết thúc bằng 2, 6, A, E) -> Là Randomized MAC (Điện thoại iOS/Android)
            return secondHex == '2' || secondHex == '6' || secondHex == 'A' || secondHex == 'E';
        }

        private static (string HostName, string DeviceType) LookupVendorFromMac(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return ("Thiết bị không định danh", "Thiết bị mạng 📶");

            string clean = mac.Replace("-", "").Replace(":", "").ToUpperInvariant();
            if (clean.Length < 6) return ("Thiết bị không định danh", "Thiết bị mạng 📶");
            string oui = clean.Substring(0, 6);

            // Apple
            if (oui.StartsWith("A0BD1D") || oui.StartsWith("F01898") || oui.StartsWith("ACBC32") || oui.StartsWith("F8FFC2") ||
                oui.StartsWith("0017F2") || oui.StartsWith("3CD0F8") || oui.StartsWith("D89695") || oui.StartsWith("406C8F") ||
                oui.StartsWith("8C8590") || oui.StartsWith("B8782E") || oui.StartsWith("38F9D3") || oui.StartsWith("701124"))
                return ("Apple Device", "Apple iPhone / iPad / Mac");

            // Samsung
            if (oui.StartsWith("503275") || oui.StartsWith("A4C494") || oui.StartsWith("342387") || oui.StartsWith("88329B") ||
                oui.StartsWith("CC07AB") || oui.StartsWith("E458E7") || oui.StartsWith("784B87") || oui.StartsWith("404E36"))
                return ("Samsung Galaxy", "Điện thoại Samsung (Android)");

            // Xiaomi
            if (oui.StartsWith("54AF97") || oui.StartsWith("640980") || oui.StartsWith("502B73") || oui.StartsWith("7C49EB") ||
                oui.StartsWith("9C99A0") || oui.StartsWith("3480B3") || oui.StartsWith("186590"))
                return ("Xiaomi / Redmi", "Điện thoại Xiaomi (Android)");

            // Oppo / Vivo / Realme
            if (oui.StartsWith("8090D0") || oui.StartsWith("E0191D") || oui.StartsWith("9C7142") || oui.StartsWith("600CB8") || oui.StartsWith("C0B5D5"))
                return ("Oppo / Vivo / Realme", "Điện thoại Android");

            // TP-Link / Tenda / Totolink
            if (oui.StartsWith("F81A67") || oui.StartsWith("3C8CF8") || oui.StartsWith("74DA88") || oui.StartsWith("C0C9E3") || oui.StartsWith("50C7BF"))
                return ("TP-Link Device", "Thiết bị mạng (Wi-Fi/AP)");

            // Hikvision / Dahua (Camera)
            if (oui.StartsWith("38AF29") || oui.StartsWith("BC1401") || oui.StartsWith("C42F90") || oui.StartsWith("4419B6"))
                return ("IP Camera An ninh", "Camera IP / Smart Home");

            // Smart TV
            if (oui.StartsWith("001FE2") || oui.StartsWith("AC8B03") || oui.StartsWith("3C15C2") || oui.StartsWith("00248D"))
                return ("Smart TV", "Smart TV / TV Box");

            // Máy tính / Laptop
            if (oui.StartsWith("000C29") || oui.StartsWith("005056")) return ("VMware Virtual PC", "Máy ảo VMware");
            if (oui.StartsWith("080027")) return ("VirtualBox Virtual PC", "Máy ảo VirtualBox");
            if (oui.StartsWith("001A7D") || oui.StartsWith("00216A") || oui.StartsWith("106530") || oui.StartsWith("54EE75"))
                return ("Intel Desktop/Laptop", "Máy tính (PC/Laptop)");

            return ("Thiết bị mạng (Chưa đặt tên)", "Thiết bị kết nối Wi-Fi");
        }

        private static string GetLocalMacAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                if (nic != null)
                {
                    return string.Join("-", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                }
            }
            catch { }
            return "N/A";
        }
    }
}

