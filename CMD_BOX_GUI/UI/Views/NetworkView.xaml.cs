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
                TxtAdapterName.Text = adapterName;
                TxtLocalIp.Text = localIp;
                TxtGateway.Text = gateway;

                string publicIp = await _network.GetPublicIpAsync();
                TxtPublicIp.Text = publicIp;
            }
            finally
            {
                BtnRefreshNetInfo.IsEnabled = true;
            }
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
        }
    }
}

