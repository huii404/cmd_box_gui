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
            await SystemCore.RunAdminCmdAsync("ipconfig /flushdns");
        }

        // 3. TÍNH NĂNG 2: LÀM MỚI ĐỊA CHỈ IP (DHCP RELEASE & RENEW)
        public async Task RenewIpAddressAsync()
        {
            Logger.Info("🔄 Đang giải phóng và xin cấp lại địa chỉ IP (Release / Renew)...");
            await SystemCore.RunAdminCmdAsync("ipconfig /release && ipconfig /renew");
        }

        // 4. TÍNH NĂNG 3: ĐẶT LẠI TCP/IP VÀ WINSOCK STACK
        public async Task ResetTcpIpWinsockAsync()
        {
            Logger.Info("⚡ Đang đặt lại giao thức mạng TCP/IP và Winsock Catalog...");
            await SystemCore.RunAdminCmdAsync("netsh winsock reset && netsh int ip reset");
        }

        // 5. TÍNH NĂNG 6: KHÔI PHỤC WINDOWS FIREWALL VỀ MẶC ĐỊNH
        public async Task ResetFirewallDefaultAsync()
        {
            Logger.Info("🛡️ Đang khôi phục Windows Firewall về cài đặt gốc...");
            await SystemCore.RunAdminCmdAsync("netsh advfirewall reset");
        }

        // 6. KHÔI PHỤC TOÀN BỘ (1-CLICK ALL-IN-ONE)
        public async Task RepairAllNetworkAsync()
        {
            Logger.Info("🚀 BẮT ĐẦU CHẠY KHÔI PHỤC MẠNG TOÀN DIỆN (Flush DNS -> Renew IP -> Reset TCP/IP -> Reset Firewall)...");
            await SystemCore.RunAdminCmdAsync("ipconfig /flushdns && ipconfig /release && ipconfig /renew && netsh winsock reset && netsh int ip reset && netsh advfirewall reset");
        }
    }
}

