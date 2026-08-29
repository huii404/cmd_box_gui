using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Services;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class NetworkView : UserControl
    {
        private readonly NetworkService _network = new();
        private bool _isBusy = false;
        private bool _isIpMasked = false;

        private string _rawAdapterName = "...";
        private string _rawLocalIp = "...";
        private string _rawGateway = "...";
        private string _rawPublicIp = "...";

        public NetworkView()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadNetworkInfoAsync();
        }

        private async Task LoadNetworkInfoAsync()
        {
            BtnRefreshNetInfo.IsEnabled = false;
            try
            {
                var (localIp, gateway, dns, adapterName) = await _network.GetCurrentNetworkInfoAsync();
                _rawAdapterName = adapterName;
                _rawLocalIp = localIp;
                _rawGateway = gateway;

                string publicIp = await _network.GetPublicIpAsync();
                _rawPublicIp = publicIp;

                UpdateIpDisplay();
            }
            finally
            {
                BtnRefreshNetInfo.IsEnabled = true;
            }
        }

        private void BtnToggleMaskIp_Click(object sender, RoutedEventArgs e)
        {
            _isIpMasked = !_isIpMasked;
            UpdateIpDisplay();
        }

        private void UpdateIpDisplay()
        {
            TxtAdapterName.Text = _rawAdapterName;

            if (_isIpMasked)
            {
                TxtLocalIp.Text = MaskIpAddress(_rawLocalIp);
                TxtGateway.Text = MaskIpAddress(_rawGateway);
                TxtPublicIp.Text = MaskIpAddress(_rawPublicIp);
                BtnToggleMaskIp.Content = "👁️‍🗨️ Hiện IP";
                BtnToggleMaskIp.ToolTip = "Đang bật chế độ ẩn IP riêng tư. Bấm để hiển thị đầy đủ IP.";
            }
            else
            {
                TxtLocalIp.Text = _rawLocalIp;
                TxtGateway.Text = _rawGateway;
                TxtPublicIp.Text = _rawPublicIp;
                BtnToggleMaskIp.Content = "👁️ Che IP";
                BtnToggleMaskIp.ToolTip = "Bật / Tắt che địa chỉ IP để bảo vệ quyền riêng tư khi stream hoặc chụp ảnh.";
            }
        }

        private static string MaskIpAddress(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || ip == "..." || ip == "Không có" || ip == "N/A" || ip == "Lỗi kết nối")
                return ip;

            var parts = ip.Split('.');
            if (parts.Length == 4)
            {
                return $"{parts[0]}.{parts[1]}.***.***";
            }

            return "••••••••";
        }

        private async void BtnRefreshNetInfo_Click(object sender, RoutedEventArgs e)
        {
            await LoadNetworkInfoAsync();
        }

        // 1. Xóa Cache DNS
        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteNetworkTaskAsync(async () =>
            {
                await _network.FlushDnsAsync();
            });
        }

        // 2. Làm Mới IP
        private async void BtnRenewIp_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteNetworkTaskAsync(async () =>
            {
                await _network.RenewIpAddressAsync();
                await LoadNetworkInfoAsync();
            });
        }

        // 3. Đặt Lại TCP/IP & Winsock
        private async void BtnResetWinsock_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteNetworkTaskAsync(async () =>
            {
                await _network.ResetTcpIpWinsockAsync();
            });
        }

        // 4. Khôi Phục Firewall Gốc
        private async void BtnResetFirewall_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteNetworkTaskAsync(async () =>
            {
                await _network.ResetFirewallDefaultAsync();
            });
        }

        // 5. Khôi Phục Toàn Diện 1-Click
        private async void BtnRepairAll_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteNetworkTaskAsync(async () =>
            {
                await _network.RepairAllNetworkAsync();
                await LoadNetworkInfoAsync();
            });
        }

        private async Task ExecuteNetworkTaskAsync(Func<Task> taskAction)
        {
            if (_isBusy) return;
            SetButtonsState(false);
            _isBusy = true;

            try
            {
                await taskAction();
            }
            finally
            {
                _isBusy = false;
                SetButtonsState(true);
            }
        }

        private void SetButtonsState(bool isEnabled)
        {
            BtnFlushDns.IsEnabled = isEnabled;
            BtnRenewIp.IsEnabled = isEnabled;
            BtnResetWinsock.IsEnabled = isEnabled;
            BtnResetFirewall.IsEnabled = isEnabled;
            BtnRepairAll.IsEnabled = isEnabled;
            BtnRefreshNetInfo.IsEnabled = isEnabled;
            BtnToggleMaskIp.IsEnabled = isEnabled;
        }
    }
}
