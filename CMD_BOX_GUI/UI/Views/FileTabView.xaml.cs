using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Services;
using Microsoft.Win32;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class FileTabView : UserControl
    {
        private string? _filePath;
        private readonly Action<string>? _onSendToMedia;
        private readonly Action<string>? _onTitleChanged;
        private bool _isTextMode = false;

        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".md", ".json", ".xml", ".yaml", ".yml", ".ini", ".cfg", ".conf", ".toml",
            ".bat", ".cmd", ".ps1", ".sh", ".cs", ".xaml", ".js", ".ts", ".py", ".html", ".css",
            ".sql", ".csv", ".gitignore", ".env", ".reg", ".slnx", ".csproj"
        };

        private static readonly HashSet<string> PreviewableImages = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".webp", ".tiff", ".tif"
        };

        /// <summary>
        /// Tạo Tab nháp văn bản (Scratchpad)
        /// </summary>
        public FileTabView(string initialTitle = "Scratchpad.txt", Action<string>? onTitleChanged = null)
        {
            InitializeComponent();
            _onTitleChanged = onTitleChanged;
            _isTextMode = true;

            TxtFileIcon.Text = "📝";
            TxtFileName.Text = initialTitle;
            TxtFileMeta.Text = "Ghi chú / Nháp";
            TxtFilePath.Text = "Chưa lưu vào ổ đĩa — Bấm 'Lưu' để xuất ra file";

            BtnSaveFile.Visibility = Visibility.Visible;
            BtnCopyText.Visibility = Visibility.Visible;
            TxtEditor.Text = "// Ghi chú nhanh hoặc dán nội dung / kịch bản vào đây...\r\n";
        }

        /// <summary>
        /// Tạo Tab từ tệp thực tế được nhập (Import / Drag & Drop)
        /// </summary>
        public FileTabView(string filePath, Action<string>? onSendToMedia = null, Action<string>? onTitleChanged = null)
        {
            InitializeComponent();
            _filePath = filePath;
            _onSendToMedia = onSendToMedia;
            _onTitleChanged = onTitleChanged;

            LoadFile(filePath);
        }

        public string GetEditorText() => _isTextMode ? TxtEditor.Text : (_filePath ?? string.Empty);

        private void LoadFile(string path)
        {
            if (!File.Exists(path))
            {
                TxtFileName.Text = Path.GetFileName(path);
                TxtFilePath.Text = "Tệp không tồn tại!";
                return;
            }

            var info = new FileInfo(path);
            string ext = info.Extension.ToLowerInvariant();

            TxtFileName.Text = info.Name;
            TxtFilePath.Text = info.FullName;
            BtnOpenExplorer.Visibility = Visibility.Visible;

            bool isImg = MediaService.IsImageFile(path);
            bool isVid = MediaService.IsVideoFile(path);

            if (isImg || isVid)
            {
                BtnSendToMedia.Visibility = Visibility.Visible;
            }

            // 1. Xem trước hình ảnh (Không khóa file nhờ BitmapCacheOption.OnLoad)
            if (PreviewableImages.Contains(ext))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(info.FullName, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    ImgPreview.Source = bmp;
                    TxtFileIcon.Text = "🖼️";
                    TxtFileMeta.Text = $"{ext.ToUpperInvariant()} • {bmp.PixelWidth}×{bmp.PixelHeight} • {SystemCore.FormatBytes(info.Length)}";

                    PnlTextEditor.Visibility = Visibility.Collapsed;
                    PnlImagePreview.Visibility = Visibility.Visible;
                    PnlBinaryInfo.Visibility = Visibility.Collapsed;
                    return;
                }
                catch
                {
                    // Fallback xuống binary card nếu codec ảnh đặc biệt không đọc được
                }
            }

            // 2. Đọc file văn bản / code / script (dưới 5MB hoặc không chứa null byte nhị phân)
            if (!isVid && !isImg && info.Length <= 5 * 1024 * 1024 && (TextExtensions.Contains(ext) || LooksLikeTextFile(path)))
            {
                try
                {
                    string content = File.ReadAllText(path);
                    _isTextMode = true;
                    TxtEditor.Text = content;

                    bool isScript = ext is ".bat" or ".cmd" or ".ps1";
                    TxtFileIcon.Text = isScript ? "⚡" : "📄";
                    int lines = content.Split('\n').Length;
                    TxtFileMeta.Text = $"{(string.IsNullOrEmpty(ext) ? "TEXT" : ext.ToUpperInvariant())} • {lines} dòng • {SystemCore.FormatBytes(info.Length)}";

                    BtnSaveFile.Visibility = Visibility.Visible;
                    BtnCopyText.Visibility = Visibility.Visible;
                    if (isScript) BtnRunScript.Visibility = Visibility.Visible;

                    PnlTextEditor.Visibility = Visibility.Visible;
                    PnlImagePreview.Visibility = Visibility.Collapsed;
                    PnlBinaryInfo.Visibility = Visibility.Collapsed;
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Không thể đọc văn bản '{info.Name}': {ex.Message}");
                }
            }

            // 3. Tệp Video / Audio / Binary lớn
            TxtFileIcon.Text = isVid ? "🎬" : "📦";
            TxtBinaryBigIcon.Text = isVid ? "🎬" : (isImg ? "🖼️" : "📦");
            TxtBinaryTitle.Text = info.Name;
            TxtFileMeta.Text = $"{(string.IsNullOrEmpty(ext) ? "FILE" : ext.ToUpperInvariant())} • {SystemCore.FormatBytes(info.Length)}";
            TxtBinaryDesc.Text = $"Dung lượng: {SystemCore.FormatBytes(info.Length)}  |  Cập nhật: {info.LastWriteTime:dd/MM/yyyy HH:mm}\nĐường dẫn: {info.FullName}";

            PnlTextEditor.Visibility = Visibility.Collapsed;
            PnlImagePreview.Visibility = Visibility.Collapsed;
            PnlBinaryInfo.Visibility = Visibility.Visible;
        }

        private static bool LooksLikeTextFile(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] buffer = new byte[Math.Min(4096, (int)fs.Length)];
                int read = fs.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] == 0) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void BtnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (!_isTextMode) return;

            if (string.IsNullOrWhiteSpace(_filePath))
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Lưu tệp văn bản / kịch bản",
                    FileName = TxtFileName.Text,
                    Filter = "Text Files (*.txt)|*.txt|Batch Script (*.bat;*.cmd)|*.bat;*.cmd|PowerShell (*.ps1)|*.ps1|All Files (*.*)|*.*"
                };

                if (sfd.ShowDialog() != true) return;
                _filePath = sfd.FileName;
                BtnOpenExplorer.Visibility = Visibility.Visible;
            }

            try
            {
                File.WriteAllText(_filePath, TxtEditor.Text, new UTF8Encoding(false));
                var info = new FileInfo(_filePath);
                TxtFileName.Text = info.Name;
                TxtFilePath.Text = info.FullName;
                TxtFileMeta.Text = $"{info.Extension.ToUpperInvariant()} • {SystemCore.FormatBytes(info.Length)}";
                _onTitleChanged?.Invoke(info.Name);
                Logger.Success($"💾 Đã lưu tệp: {info.FullName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Lỗi khi lưu tệp: {ex.Message}");
                MessageBox.Show($"Không thể lưu tệp:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCopyText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetDataObject(TxtEditor.Text ?? string.Empty, false);
                Logger.Info($"📋 Đã sao chép nội dung tab [{TxtFileName.Text}] vào Clipboard.");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Không thể truy cập Clipboard: {ex.Message}");
            }
        }

        private void BtnSendToMedia_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath))
            {
                _onSendToMedia?.Invoke(_filePath);
            }
        }

        private async void BtnRunScript_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath)) return;

            var confirm = MessageBox.Show(
                $"Bạn muốn chạy thử kịch bản '{Path.GetFileName(_filePath)}' và xuất kết quả ra Terminal?",
                "Chạy thử Script",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            BtnRunScript.IsEnabled = false;
            try
            {
                // Lưu nội dung hiện tại trước khi chạy nếu đã chỉnh sửa
                if (_isTextMode)
                {
                    File.WriteAllText(_filePath, TxtEditor.Text, new UTF8Encoding(false));
                }

                string ext = Path.GetExtension(_filePath).ToLowerInvariant();
                Logger.Info($"▶ Bắt đầu thực thi [{Path.GetFileName(_filePath)}]...");

                int code;
                if (ext == ".ps1")
                {
                    code = await ProcessRunner.RunProcessAsync(
                        "powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{_filePath}\"",
                        line => Logger.Info($"[PS1] {line}"),
                        err => Logger.Warning($"[PS1] {err}"));
                }
                else
                {
                    code = await ProcessRunner.RunProcessAsync(
                        "cmd.exe",
                        $"/c \"{_filePath}\"",
                        line => Logger.Info($"[BAT] {line}"),
                        err => Logger.Warning($"[BAT] {err}"));
                }

                Logger.Success($"✅ Thực thi hoàn tất [{Path.GetFileName(_filePath)}] (Exit code: {code}).");
            }
            catch (Exception ex)
            {
                Logger.Error($"Lỗi chạy script: {ex.Message}");
            }
            finally
            {
                BtnRunScript.IsEnabled = true;
            }
        }

        private void BtnOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_filePath}\"",
                    UseShellExecute = true
                });
            }
        }

        private void BtnOpenDefaultApp_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = _filePath, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Không mở được tệp: {ex.Message}");
                }
            }
        }
    }
}