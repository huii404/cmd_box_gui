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

    public enum ServiceStartupMode
    {
        Automatic,
        AutomaticDelayed,
        Manual,
        Disabled
    }

    public class ServiceStartupModeOption
    {
        public ServiceStartupMode Mode { get; init; }
        public string DisplayName { get; init; } = string.Empty;
    }

    public class SystemServiceItem : INotifyPropertyChanged
    {
        public static IReadOnlyList<ServiceStartupModeOption> StartupModeOptions { get; } = new[]
        {
            new ServiceStartupModeOption { Mode = ServiceStartupMode.Disabled, DisplayName = "🛑 Disabled (Tắt)" },
            new ServiceStartupModeOption { Mode = ServiceStartupMode.Manual, DisplayName = "✋ Manual (Thủ công)" },
            new ServiceStartupModeOption { Mode = ServiceStartupMode.Automatic, DisplayName = "⚡ Auto (Tự động)" },
            new ServiceStartupModeOption { Mode = ServiceStartupMode.AutomaticDelayed, DisplayName = "⏳ Auto Delayed (Trễ)" }
        };

        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = "Chung";
        public string Description { get; set; } = string.Empty;
        public string Impact { get; set; } = "Tiết kiệm RAM / CPU";
        public string RiskLevel { get; set; } = "Safe"; // "Safe", "Moderate", "Complex"

        public string RiskDisplay => RiskLevel switch
        {
            "Safe" => "🟢 An Toàn",
            "Moderate" => "🟡 Cân Nhắc",
            _ => "🟠 Ảnh Hưởng Hệ Thống"
        };

        public string RiskBadgeBg => RiskLevel switch
        {
            "Safe" => "#142E1C",
            "Moderate" => "#332600",
            _ => "#3B1A14"
        };

        public string RiskBadgeBorder => RiskLevel switch
        {
            "Safe" => "#10B981",
            "Moderate" => "#F59E0B",
            _ => "#F97316"
        };

        public string RiskBadgeFg => RiskLevel switch
        {
            "Safe" => "#34D399",
            "Moderate" => "#FBBF24",
            _ => "#FB923C"
        };

        private ServiceStartupMode _currentStartupMode = ServiceStartupMode.Manual;
        public ServiceStartupMode CurrentStartupMode
        {
            get => _currentStartupMode;
            set
            {
                _currentStartupMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDisabled));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        private ServiceStartupMode _targetStartupMode = ServiceStartupMode.Disabled;
        public ServiceStartupMode TargetStartupMode
        {
            get => _targetStartupMode;
            set
            {
                _targetStartupMode = value;
                OnPropertyChanged();
            }
        }

        public bool IsDisabled
        {
            get => _currentStartupMode == ServiceStartupMode.Disabled;
            set => CurrentStartupMode = value ? ServiceStartupMode.Disabled : ServiceStartupMode.Manual;
        }

        public string StatusText => _currentStartupMode switch
        {
            ServiceStartupMode.Disabled => "🛑 Disabled",
            ServiceStartupMode.Manual => "✋ Manual",
            ServiceStartupMode.Automatic => "⚡ Auto",
            ServiceStartupMode.AutomaticDelayed => "⏳ Auto (Delayed)",
            _ => "❓ Không rõ"
        };

        public string StatusColor => _currentStartupMode switch
        {
            ServiceStartupMode.Disabled => "#9E9E9E",
            ServiceStartupMode.Manual => "#38BDF8",
            ServiceStartupMode.Automatic => "#10B981",
            ServiceStartupMode.AutomaticDelayed => "#F59E0B",
            _ => "#9E9E9E"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class JunkScanSummary
    {
        public long QuickCleanBytes { get; set; }
        public long DeepCleanProBytes { get; set; }
        public long WindowsOldBytes { get; set; }
        public long DevCacheBytes { get; set; }
        public long BrowserCacheBytes { get; set; }
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

        // Nguồn gốc theo chuẩn BloatFinder: Sponsored, OEM, Microsoft
        public string Origin { get; set; } = "Microsoft";
        public string OriginDisplay => Origin switch
        {
            "Sponsored" => "🌟 Quảng Cáo",
            "OEM" => "💻 Hãng Laptop",
            _ => "🪟 Windows In-Box"
        };
        public string OriginBadgeBg => Origin switch
        {
            "Sponsored" => "#2E1C38",
            "OEM" => "#1E2B38",
            _ => "#1B2A32"
        };
        public string OriginBadgeBorder => Origin switch
        {
            "Sponsored" => "#A855F7",
            "OEM" => "#38BDF8",
            _ => "#0EA5E9"
        };
        public string OriginBadgeFg => Origin switch
        {
            "Sponsored" => "#C084FC",
            "OEM" => "#7DD3FC",
            _ => "#38BDF8"
        };

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

    public class SystemHealthReport
    {
        public int TotalIssuesFound { get; set; }
        public bool IsTelemetryActive { get; set; }
        public bool IsWin11AdsActive { get; set; }
        public bool IsCopilotActive { get; set; }
        public bool IsGameDvrActive { get; set; }
        public int DetectedBloatwareCount { get; set; }
        public string HealthScoreText { get; set; } = "Chưa chẩn đoán";
        public string HealthScoreColor { get; set; } = "#9CA3AF";
        public List<string> IssueDescriptions { get; set; } = new();
    }

    public class DocumentBatchItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string FileExtension => Path.GetExtension(FilePath).ToUpperInvariant();
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted => SystemCore.FormatBytes(FileSizeBytes);

        public string DocType { get; set; } = "Ảnh OCR";

        private string _status = "Sẵn sàng";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public string StatusColor => Status switch
        {
            "Thành công" or "Hoàn tất" => "#10B981",
            "Đang xử lý..." => "#38BDF8",
            "Lỗi" => "#EF4444",
            _ => "#9CA3AF"
        };

        private string _resultText = string.Empty;
        public string ResultText
        {
            get => _resultText;
            set
            {
                _resultText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CharCount));
                OnPropertyChanged(nameof(WordCount));
            }
        }

        public int CharCount => ResultText.Length;
        public int WordCount => string.IsNullOrWhiteSpace(ResultText)
            ? 0
            : ResultText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        public string OutputPath { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class OcrLanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
