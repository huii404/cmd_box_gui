using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using CMD_BOX_GUI.Core;

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
    }
}

