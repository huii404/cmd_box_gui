using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;
using CMD_BOX_GUI.Services;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class NetworkView : UserControl
    {
        private readonly NetworkService _network = new();
        private readonly ObservableCollection<LanDeviceItem> _lanDevices = new();
        private readonly ObservableCollection<WifiProfileItem> _wifiProfiles = new();
        private readonly ICollectionView _wifiView;
        private bool _isLoaded = false;
        private string _currentGateway = "192.168.1.1";

        public NetworkView()
        {
            InitializeComponent();
            DgLanDevices.ItemsSource = _lanDevices;

            _wifiView = CollectionViewSource.GetDefaultView(_wifiProfiles);
            _wifiView.Filter = FilterWifi;
            DgWifiProfiles.ItemsSource = _wifiView;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                _isLoaded = true;
                await LoadNetworkOverviewAsync();
            }
        }

        // ================= SUB-TAB SWITCHING =================
        private void TabNet_Click(object sender, RoutedEventArgs e)
        {
            if (PnlNetOverview == null || PnlNetRepair == null || PnlNetDevices == null || PnlNetWifi == null) return;

            PnlNetOverview.Visibility = TabNetOverview.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlNetRepair.Visibility = TabNetRepair.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlNetDevices.Visibility = TabNetDevices.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlNetWifi.Visibility = TabNetWifi.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (TabNetWifi.IsChecked == true && _wifiProfiles.Count == 0)
            {
                _ = RefreshWifiProfilesAsync();
            }
        }

        // ================= 1. TAB OVERVIEW & DNS =================
        private async Task LoadNetworkOverviewAsync()
        {
            SetRunning(true, "Đang đọc thông số card mạng & IP...");
            try
            {
                var (localIp, gateway, dns, adapterName) = await _network.GetCurrentNetworkInfoAsync();
                TxtLocalIp.Text = localIp;
                TxtGateway.Text = gateway;
                TxtDns.Text = dns;
                TxtAdapterName.Text = $"Card: {adapterName}";
                TxtNetworkSummary.Text = $"Card: {adapterName} • IP: {localIp}";
                _currentGateway = (gateway != "N/A" && !string.IsNullOrWhiteSpace(gateway)) ? gateway : "192.168.1.1";

                string publicIp = await _network.GetPublicIpAsync();
                TxtPublicIp.Text = publicIp;

                SetRunning(false, "Đã đọc xong thông số mạng hiện tại.");
            }
            catch (Exception ex)
            {
                SetRunning(false, $"Lỗi đọc mạng: {ex.Message}");
            }
        }

        private async void BtnRefreshNetInfo_Click(object sender, RoutedEventArgs e)
        {
            await LoadNetworkOverviewAsync();
        }

        private async void BtnTestPing_Click(object sender, RoutedEventArgs e)
        {
            TxtPingLatency.Text = "Ping: ...";
            long ms = await _network.MeasurePingLatencyAsync("8.8.8.8");
            if (ms >= 0)
            {
                string quality = ms < 30 ? "Tuyệt vời 🟢" : (ms < 80 ? "Tốt 🟡" : "Chậm 🔴");
                TxtPingLatency.Text = $"Ping: {ms}ms ({quality})";
                SetRunning(false, $"Độ trễ ping tới Google DNS (8.8.8.8): {ms} ms ({quality})");
            }
            else
            {
                TxtPingLatency.Text = "Ping: Lỗi ❌";
                SetRunning(false, "Không thể ping tới máy chủ Internet.");
            }
        }

        private async void BtnSetDnsCloudflare_Click(object sender, RoutedEventArgs e)
        {
            await ApplyDnsAsync("1.1.1.1", "1.0.0.1", "Cloudflare DNS");
        }

        private async void BtnSetDnsGoogle_Click(object sender, RoutedEventArgs e)
        {
            await ApplyDnsAsync("8.8.8.8", "8.8.4.4", "Google Public DNS");
        }

        private async void BtnSetDnsAdGuard_Click(object sender, RoutedEventArgs e)
        {
            await ApplyDnsAsync("94.140.14.14", "94.140.15.15", "AdGuard DNS (Chặn quảng cáo)");
        }

        private async void BtnSetDnsDhcp_Click(object sender, RoutedEventArgs e)
        {
            await ApplyDnsAsync("DHCP", null, "DNS Tự Động (DHCP)");
        }

        private async Task ApplyDnsAsync(string primary, string? secondary, string dnsName)
        {
            SetRunning(true, $"Đang áp dụng {dnsName}...");
            bool ok = await _network.SetDnsAsync(primary, secondary);
            if (ok)
            {
                await LoadNetworkOverviewAsync();
                SetRunning(false, $"Đã đổi sang {dnsName} thành công!");
                MessageBox.Show($"Đã cấu hình {dnsName} thành công cho card mạng!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                SetRunning(false, $"Đổi {dnsName} thất bại.");
            }
        }

        // ================= 2. TAB REPAIR & RESCUE =================
        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang xóa cache DNS...");
            await _network.FlushDnsAsync();
            SetRunning(false, "Đã xóa sạch bộ nhớ đệm DNS!");
            MessageBox.Show("Đã xóa cache DNS (Flush DNS) thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnRenewIp_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang xin cấp lại IP từ Router...");
            await _network.RenewIpAddressAsync();
            await LoadNetworkOverviewAsync();
            SetRunning(false, "Đã làm mới địa chỉ IP thành công!");
            MessageBox.Show("Đã làm mới địa chỉ IP thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnResetWinsock_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang đặt lại giao thức TCP/IP & Winsock...");
            await _network.ResetTcpIpWinsockAsync();
            SetRunning(false, "Đã đặt lại TCP/IP & Winsock!");
            MessageBox.Show("Đã đặt lại TCP/IP & Winsock Catalog thành công!\n(Nên khởi động lại máy để có hiệu lực tối đa).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnResetFirewall_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Bạn có chắc muốn khôi phục Windows Firewall về mặc định của Microsoft?\n(Các rule tùy chỉnh trước đây sẽ được làm sạch).",
                "Xác nhận khôi phục Firewall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, "Đang khôi phục Firewall...");
            await _network.ResetFirewallDefaultAsync();
            SetRunning(false, "Đã khôi phục Windows Firewall về cài đặt gốc!");
            MessageBox.Show("Đã khôi phục Windows Firewall về mặc định thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnRepairAll_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Chạy khôi phục toàn diện 1-Click:\n1. Xóa Cache DNS\n2. Xin cấp lại IP\n3. Đặt lại TCP/IP & Winsock\n4. Khôi phục Firewall gốc\n\nBạn có muốn tiếp tục?",
                "Khôi Phục Mạng Toàn Diện",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            SetRunning(true, "Đang thực hiện khôi phục mạng toàn diện...");
            await _network.RepairAllNetworkAsync();
            await LoadNetworkOverviewAsync();
            SetRunning(false, "Khôi phục toàn diện mạng hoàn tất!");
            MessageBox.Show("Quá trình khôi phục mạng toàn diện đã hoàn tất thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ================= 3. TAB LAN SCANNER =================
        private async void BtnScanLan_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang quét thiết bị cùng mạng LAN / Wi-Fi...");
            _lanDevices.Clear();
            TxtNoDevices.Visibility = Visibility.Collapsed;

            var progress = new Progress<int>(v => PbNetwork.Value = v);
            var devices = await _network.ScanLanDevicesAsync(progress);

            foreach (var d in devices)
            {
                _lanDevices.Add(d);
            }

            TxtNoDevices.Visibility = _lanDevices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtDevicesStats.Text = $"• Tìm thấy {_lanDevices.Count} thiết bị đang kết nối";
            SetRunning(false, $"Đã tìm thấy {_lanDevices.Count} thiết bị trong mạng LAN.");
        }

        private void BtnOpenRouter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"http://{_currentGateway}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể mở trang Router: {ex.Message}");
            }
        }

        private void BtnCopySelectedIp_Click(object sender, RoutedEventArgs e)
        {
            if (DgLanDevices.SelectedItem is LanDeviceItem item)
            {
                Clipboard.SetText(item.IpAddress);
                SetRunning(false, $"Đã sao chép IP [{item.IpAddress}] vào Clipboard!");
            }
            else
            {
                MessageBox.Show("Vui lòng nhấp chọn một thiết bị trên bảng để sao chép IP!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ================= 4. TAB WI-FI PASSWORDS =================
        private async void BtnRefreshWifi_Click(object sender, RoutedEventArgs e)
        {
            await RefreshWifiProfilesAsync();
        }

        private async Task RefreshWifiProfilesAsync()
        {
            SetRunning(true, "Đang đọc danh sách mật khẩu Wi-Fi đã lưu...");
            _wifiProfiles.Clear();
            TxtNoWifi.Visibility = Visibility.Collapsed;

            var list = await _network.GetSavedWifiProfilesAsync();
            foreach (var item in list)
            {
                _wifiProfiles.Add(item);
            }

            _wifiView.Refresh();
            TxtNoWifi.Visibility = _wifiProfiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SetRunning(false, $"Đã đọc được {_wifiProfiles.Count} mạng Wi-Fi đã từng kết nối.");
        }

        private bool FilterWifi(object obj)
        {
            if (obj is not WifiProfileItem item) return false;
            string query = TxtSearchWifi?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query)) return true;
            return item.Ssid.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private void TxtSearchWifi_TextChanged(object sender, TextChangedEventArgs e)
        {
            _wifiView?.Refresh();
        }

        private void BtnCopyWifiPassword_Click(object sender, RoutedEventArgs e)
        {
            if (DgWifiProfiles.SelectedItem is WifiProfileItem item)
            {
                if (item.HasPassword)
                {
                    Clipboard.SetText(item.Password);
                    SetRunning(false, $"Đã sao chép mật khẩu Wi-Fi [{item.Ssid}]!");
                }
                else
                {
                    MessageBox.Show("Mạng này không có mật khẩu (Mạng mở)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng nhấp chọn một mạng Wi-Fi trên bảng để sao chép mật khẩu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCopySingleWifiPass_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is WifiProfileItem item)
            {
                if (item.HasPassword)
                {
                    Clipboard.SetText(item.Password);
                    SetRunning(false, $"Đã sao chép mật khẩu Wi-Fi [{item.Ssid}]!");
                }
                else
                {
                    MessageBox.Show("Mạng này không có mật khẩu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // ================= STATUS & BUSY HELPER =================
        private void SetRunning(bool running, string statusText)
        {
            PbNetwork.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtNetworkStatus.Text = statusText;

            BtnFlushDns.IsEnabled = !running;
            BtnRenewIp.IsEnabled = !running;
            BtnResetWinsock.IsEnabled = !running;
            BtnResetFirewall.IsEnabled = !running;
            BtnRepairAll.IsEnabled = !running;
            BtnScanLan.IsEnabled = !running;
            BtnRefreshWifi.IsEnabled = !running;
        }
    }
}