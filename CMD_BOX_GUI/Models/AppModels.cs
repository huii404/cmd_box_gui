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
        public bool AllowAdminCmd { get; set; } = true;
        public bool IsIpMasked { get; set; } = false;
        public string LastSelectedTab { get; set; } = "Optimizer";
        public DateTime LastSavedTime { get; set; } = DateTime.Now;
    }
}
