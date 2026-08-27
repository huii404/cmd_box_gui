using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;
using CMD_BOX_GUI.Services;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class NetworkView : UserControl
    {
        private readonly NetworkService _network = new();

        public NetworkView()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadNetworkInfoAsync();
        }

        private async Task LoadNetworkInfoAsync()
        {
            var adapters = await _network.GetAdaptersInfoAsync();
            var primary = adapters.FirstOrDefault();

            if (primary != null)
            {
                TxtLocalIp.Text = $"LAN: {primary.Ipv4Address} ({primary.Name})";
                TxtGateway.Text = $"Gateway: {primary.Gateway}";
                TxtDns.Text = $"DNS: {primary.DnsServers}";
            }
            else
            {
                TxtLocalIp.Text = "LAN: Chưa kết nối";
            }

            string publicIp = await _network.GetPublicIpAsync();
            TxtPublicIp.Text = $"Public IP: {publicIp}";
        }

        private async void BtnRefreshNetInfo_Click(object sender, RoutedEventArgs e)
        {
            await LoadNetworkInfoAsync();
        }

        private async void BtnAuditWifi_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang quét Wi-Fi...");
            var list = await _network.AuditSavedWifiAsync();
            DgWifi.ItemsSource = list;
            SetRunning(false, $"Tìm thấy {list.Count} Wi-Fi.");
        }

        private void BtnCopyPass_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WifiInfo info)
            {
                Clipboard.SetText(info.Password);
                Logger.Success($"Đã copy pass Wi-Fi [{info.Ssid}]!");
            }
        }

        private async void BtnRepairNetwork_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang khôi phục mạng...");
            var progress = new Progress<int>(v => PbNetwork.Value = v);
            await _network.RepairNetworkProAsync(progress);
            await LoadNetworkInfoAsync();
            SetRunning(false, "Khôi phục mạng xong!");
        }

        private async void BtnSecurityShield_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang bật Lá chắn bảo mật...");
            await _network.ApplySecurityShieldAsync();
            SetRunning(false, "Đã kích hoạt bảo mật!");
        }

        private async void BtnCheckHosts_Click(object sender, RoutedEventArgs e)
        {
            SetRunning(true, "Đang kiểm tra file hosts...");
            await _network.CheckHostsFileSecurityAsync();
            SetRunning(false, "Kiểm tra hosts xong!");
        }

        private void SetRunning(bool running, string statusText)
        {
            PbNetwork.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtStatus.Visibility = Visibility.Visible;
            TxtStatus.Text = statusText;
            BtnRefreshNetInfo.IsEnabled = !running;
            BtnAuditWifi.IsEnabled = !running;
            BtnRepairNetwork.IsEnabled = !running;
            BtnSecurityShield.IsEnabled = !running;
            BtnCheckHosts.IsEnabled = !running;
        }
    }
}
