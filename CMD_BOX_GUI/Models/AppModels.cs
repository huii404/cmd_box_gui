using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CMD_BOX_GUI.Core;

namespace CMD_BOX_GUI.Models
{
    public class MediaBatchItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string FileExtension => Path.GetExtension(FilePath).ToUpperInvariant();
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted => SystemCore.FormatBytes(FileSizeBytes);

        public bool IsImage { get; set; }
        public bool IsVideo { get; set; }
        public string MediaTypeText => IsImage ? "Ảnh 🖼️" : (IsVideo ? "Video 🎥" : "Tệp 📄");

        public List<string> AvailableExtensions { get; set; } = new();

        private string _targetExtension = string.Empty;
        public string TargetExtension
        {
            get => _targetExtension;
            set { _targetExtension = value; OnPropertyChanged(); }
        }

        private long _processedSizeBytes;
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

        public string ProcessedSizeFormatted => _processedSizeBytes > 0 ? SystemCore.FormatBytes(_processedSizeBytes) : "--";

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

        private string _outputPath = string.Empty;
        public string OutputPath
        {
            get => _outputPath;
            set { _outputPath = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = true;
        public bool AllowAdminCmd { get; set; } = false;
        public bool IsIpMasked { get; set; } = false;
        public string LastSelectedTab { get; set; } = "Optimizer";
        public DateTime LastSavedTime { get; set; } = DateTime.Now;
    }

    public class WorkspaceTabItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Icon { get; set; } = "📄";

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string ToolTipText { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public bool IsFileTab => !string.IsNullOrWhiteSpace(FilePath);
        public System.Windows.UIElement? Content { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LanDeviceItem
    {
        public string IpAddress { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = "Thiết bị kết nối 📱";
        public string Status { get; set; } = "Online 🟢";
        public bool IsLocalDevice { get; set; }
        public bool IsGateway { get; set; }

        public string StatusBadgeBg => "#142E1C";
        public string StatusBadgeBorder => "#10B981";
        public string StatusBadgeFg => "#34D399";
    }

    public class WifiProfileItem : INotifyPropertyChanged
    {
        public string Ssid { get; set; } = string.Empty;
        public string Authentication { get; set; } = "WPA2-Personal";
        public string Password { get; set; } = string.Empty;
        public bool HasPassword => !string.IsNullOrEmpty(Password);
        public string PasswordDisplay => HasPassword ? Password : "(Mạng mở / Không mật khẩu)";
        public string SecurityIcon => HasPassword ? "🔒" : "🌐";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SystemServiceItem : INotifyPropertyChanged
    {
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = "Chung";
        public string Description { get; set; } = string.Empty;
        public string Impact { get; set; } = "Tiết kiệm RAM / CPU";
        public string RiskLevel { get; set; } = "Safe"; // "Safe" or "Caution"

        public string RiskDisplay => RiskLevel == "Safe" ? "🟢 An Toàn" : "🟡 Cẩn Trọng";
        public string RiskBadgeBg => RiskLevel == "Safe" ? "#142E1C" : "#332600";
        public string RiskBadgeBorder => RiskLevel == "Safe" ? "#10B981" : "#F59E0B";
        public string RiskBadgeFg => RiskLevel == "Safe" ? "#34D399" : "#FBBF24";

        private bool _isDisabled;
        public bool IsDisabled
        {
            get => _isDisabled;
            set
            {
                _isDisabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(ActionButtonText));
            }
        }

        public string StatusText => _isDisabled ? "🛑 Đã Tắt" : "🟢 Đang Chạy";
        public string StatusColor => _isDisabled ? "#9E9E9E" : "#10B981";
        public string ActionButtonText => _isDisabled ? "Bật Lại" : "Tắt Service";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BloatwareAppItem : INotifyPropertyChanged
    {
        public string AppName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string Category { get; set; } = "App Rác";
        public string Description { get; set; } = string.Empty;
        public string EstimatedSize { get; set; } = "~50-200 MB";
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = "Safe"; // "Safe" or "Caution"

        public string RiskDisplay => RiskLevel == "Safe" ? "🟢 An Toàn" : "🟡 Cẩn Trọng";
        public string RiskBadgeBg => RiskLevel == "Safe" ? "#142E1C" : "#332600";
        public string RiskBadgeBorder => RiskLevel == "Safe" ? "#10B981" : "#F59E0B";
        public string RiskBadgeFg => RiskLevel == "Safe" ? "#34D399" : "#FBBF24";

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private bool _isInstalled = true;
        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(InstalledStatus)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public string InstalledStatus => _isInstalled ? "Chưa gỡ" : "Đã gỡ sạch ✨";
        public string StatusColor => _isInstalled ? "#F59E0B" : "#10B981";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
