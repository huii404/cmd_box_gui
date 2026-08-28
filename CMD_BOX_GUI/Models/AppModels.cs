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

        public bool IsImage { get; set; }
        public bool IsVideo { get; set; }
        public string MediaTypeText => IsImage ? "Ảnh 🖼️" : (IsVideo ? "Video 🎥" : "Tệp 📄");

        public System.Collections.Generic.List<string> AvailableExtensions { get; set; } = new();

        private string _targetExtension = string.Empty;
        public string TargetExtension
        {
            get => _targetExtension;
            set { _targetExtension = value; OnPropertyChanged(); }
        }

        private long _processedSizeBytes = 0;
        public long ProcessedSizeBytes
        {
            get => _processedSizeBytes;
            set
            {
                _processedSizeBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProcessedSizeFormatted));
                OnPropertyChanged(nameof(CompressionRatio));
            }
        }

        public string ProcessedSizeFormatted => _processedSizeBytes > 0 ? Core.SystemCore.FormatBytes(_processedSizeBytes) : "--";

        public string CompressionRatio
        {
            get
            {
                if (FileSizeBytes <= 0 || _processedSizeBytes <= 0) return "--";
                double diff = (double)(_processedSizeBytes - FileSizeBytes) / FileSizeBytes * 100.0;
                return diff < 0 ? $"{diff:0.#}%" : $"+{diff:0.#}%";
            }
        }

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

        private string _outputPath = string.Empty;
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                _outputPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCompare));
            }
        }

        public bool CanCompare => IsImage && !string.IsNullOrWhiteSpace(_outputPath) && System.IO.File.Exists(_outputPath);

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = true;
        public string LastSelectedTab { get; set; } = "Dashboard";
        public string CustomOutputDir { get; set; } = string.Empty;
        public int DefaultCrf { get; set; } = 26;
        public int AutoRefreshIntervalSec { get; set; } = 3;
        public DateTime LastSavedTime { get; set; } = DateTime.Now;
    }

    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Sender { get; set; } = "Bot"; // "User" or "Bot"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool IsUser => Sender.Equals("User", StringComparison.OrdinalIgnoreCase);
        public string FormattedTime => Timestamp.ToString("HH:mm");
        public string AvatarIcon => IsUser ? "👤" : "🤖";
        public string SenderName => IsUser ? "Bạn" : "CMD Assistant";
    }
}
