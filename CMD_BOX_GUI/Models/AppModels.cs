namespace CMD_BOX_GUI.Models
{
    public class WifiInfo
    {
        public string Ssid { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Authentication { get; set; } = string.Empty;
        public string Cipher { get; set; } = string.Empty;
    }

    public class BatteryInfo
    {
        public bool HasBattery { get; set; }
        public bool IsCharging { get; set; }
        public int Percent { get; set; }
        public string PowerSource { get; set; } = "Unknown";
        public string DeviceName { get; set; } = "N/A";
        public string Manufacturer { get; set; } = "N/A";
        public string Chemistry { get; set; } = "Li-ion";
        public long DesignCapacityMWh { get; set; }
        public long FullChargeCapacityMWh { get; set; }
        public long CycleCount { get; set; }
        public double HealthPercent { get; set; }
        public double WearPercent { get; set; }
        public string SystemModel { get; set; } = "N/A";
    }

    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Ipv4Address { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string DnsServers { get; set; } = string.Empty;
    }

    public class CleanCategory
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
        public string TargetPath { get; set; } = string.Empty;
        public long EstimatedBytes { get; set; }
    }
}
