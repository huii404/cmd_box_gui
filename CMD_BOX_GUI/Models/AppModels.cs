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

    public class DriveStorageInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public long UsedBytes { get; set; }
        public double UsedPercent { get; set; }
        public string TotalFormatted { get; set; } = string.Empty;
        public string FreeFormatted { get; set; } = string.Empty;
        public string UsedFormatted { get; set; } = string.Empty;
        public string SummaryText { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#06B6D4";
    }

    public class MediaBatchItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(FilePath);
        public string FileExtension => System.IO.Path.GetExtension(FilePath).ToUpperInvariant();
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted => Core.SystemCore.FormatBytes(FileSizeBytes);

        private string _status = "Sẵn sàng";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private string _statusColor = "#9CA3AF";
        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        private double _progressPercent = 0;
        public double ProgressPercent
        {
            get => _progressPercent;
            set { _progressPercent = value; OnPropertyChanged(); }
        }

        public string OutputPath { get; set; } = string.Empty;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
