using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;
using CMD_BOX_GUI.Services;
using Microsoft.Win32;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class MediaView : UserControl
    {
        private readonly MediaService _media = new();
        private readonly ObservableCollection<MediaBatchItem> _mediaItems = new();
        private string _lastOutputDir = string.Empty;
        private CancellationTokenSource? _batchCts;
        private CancellationTokenSource? _stegoCts;
        private bool _isBatchRunning = false;

        public MediaView()
        {
            InitializeComponent();
            DgMediaFiles.ItemsSource = _mediaItems;
            UpdateTableNotice();
        }

        // ================= TAB CHUYỂN ĐỔI =================
        private void SubTab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelBatchStudio == null || PanelSteganography == null) return;

            if (TabBatchStudio.IsChecked == true)
            {
                PanelBatchStudio.Visibility = Visibility.Visible;
                PanelSteganography.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanelBatchStudio.Visibility = Visibility.Collapsed;
                PanelSteganography.Visibility = Visibility.Visible;
            }
        }

        // ================= QUẢN LÝ DANH SÁCH BẢNG MEDIA =================
        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn các tệp Video / Media cần chỉnh sửa",
                Filter = "Media Files (*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.mp3;*.wav;*.jpg;*.png)|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.mp3;*.wav;*.jpg;*.png|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames)
                {
                    AddMediaFile(file);
                }
                UpdateTableNotice();
            }
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Chọn thư mục chứa các tệp Media"
            };

            if (dlg.ShowDialog() == true)
            {
                string folder = dlg.FolderName;
                if (Directory.Exists(folder))
                {
                    var extensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".mp3", ".wav", ".m4a" };
                    var files = Directory.GetFiles(folder)
                        .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

                    foreach (var file in files)
                    {
                        AddMediaFile(file);
                    }
                    UpdateTableNotice();
                }
            }
        }

        private void AddMediaFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
            if (_mediaItems.Any(i => i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))) return;

            var info = new FileInfo(filePath);
            _mediaItems.Add(new MediaBatchItem
            {
                FilePath = filePath,
                FileSizeBytes = info.Length,
                Status = "Sẵn sàng",
                StatusColor = "#9CA3AF"
            });
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchRunning)
            {
                MessageBox.Show("Tác vụ đang chạy, không thể xóa dòng lúc này!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is Button btn && btn.DataContext is MediaBatchItem item)
            {
                _mediaItems.Remove(item);
                UpdateTableNotice();
            }
        }

        private void BtnClearTable_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchRunning)
            {
                MessageBox.Show("Tác vụ đang chạy, không thể xóa bảng lúc này!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _mediaItems.Clear();
            UpdateTableNotice();
        }

        private void UpdateTableNotice()
        {
            int count = _mediaItems.Count;
            TxtTableCount.Text = $"{count} tệp trong danh sách";
            PnlEmptyNotice.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnBrowseOutputDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Chọn thư mục lưu kết quả xuất"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtCustomOutputDir.Text = dlg.FolderName;
            }
        }

        // ================= ĐIỀU KHIỂN DROPDOWN TÁC VỤ =================
        private void CmbProcessingMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PnlOptionCompress == null) return;

            // Ẩn tất cả các panel cấu hình con
            PnlOptionCompress.Visibility = Visibility.Collapsed;
            PnlOptionMp3.Visibility = Visibility.Collapsed;
            PnlOptionSpeed.Visibility = Visibility.Collapsed;
            PnlOptionFormat.Visibility = Visibility.Collapsed;
            PnlOptionGif.Visibility = Visibility.Collapsed;
            PnlOptionThumbnail.Visibility = Visibility.Collapsed;
            PnlOptionResize.Visibility = Visibility.Collapsed;
            PnlOptionTrim.Visibility = Visibility.Collapsed;

            int index = CmbProcessingMode.SelectedIndex;
            switch (index)
            {
                case 0: // Nén Video
                    PnlOptionCompress.Visibility = Visibility.Visible;
                    break;
                case 1: // Làm nét & khử nhiễu
                    break;
                case 2: // MP4 sang MP3
                    PnlOptionMp3.Visibility = Visibility.Visible;
                    break;
                case 3: // Tốc độ
                    PnlOptionSpeed.Visibility = Visibility.Visible;
                    break;
                case 4: // Chuyển định dạng
                    PnlOptionFormat.Visibility = Visibility.Visible;
                    break;
                case 5: // Tắt âm thanh
                    break;
                case 6: // Video to GIF
                    PnlOptionGif.Visibility = Visibility.Visible;
                    break;
                case 7: // Snapshot Frame
                    PnlOptionThumbnail.Visibility = Visibility.Visible;
                    break;
                case 8: // Đổi độ phân giải
                    PnlOptionResize.Visibility = Visibility.Visible;
                    break;
                case 9: // Trim Video
                    PnlOptionTrim.Visibility = Visibility.Visible;
                    break;
            }
        }

        // ================= THỰC THI XỬ LÝ HÀNG LOẠT (ASYNC + CANCEL) =================
        private async void BtnStartBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchRunning)
            {
                // Người dùng bấm Dừng lại
                _batchCts?.Cancel();
                Logger.Warning("Đang yêu cầu dừng tác vụ xử lý hàng loạt...");
                return;
            }

            if (_mediaItems.Count == 0)
            {
                MessageBox.Show("Vui lòng thêm ít nhất một tệp Video vào bảng danh sách!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _batchCts = new CancellationTokenSource();
            var token = _batchCts.Token;

            _isBatchRunning = true;
            SetBatchRunning(true, "Đang xử lý các tệp trong danh sách... (ESC/F6 hoặc bấm Dừng để hủy)");
            PbBatchTotal.Maximum = _mediaItems.Count;
            PbBatchTotal.Value = 0;

            int mode = CmbProcessingMode.SelectedIndex;
            string customOutDir = TxtCustomOutputDir.Text.Trim();

            // Đọc thông số cấu hình trên UI thread
            int crf = CmbCompressCrf.SelectedIndex switch { 0 => 22, 2 => 30, _ => 26 };
            int bitrate = CmbMp3Bitrate.SelectedIndex switch { 0 => 128, 2 => 320, _ => 192 };
            double speed = CmbSpeedVal.SelectedIndex switch { 0 => 0.5, 1 => 0.75, 2 => 1.25, 4 => 2.0, _ => 1.5 };
            string targetExt = ((ComboBoxItem)CmbTargetFormat.SelectedItem)?.Content?.ToString() ?? ".mp4";
            int gifWidth = CmbGifWidth.SelectedIndex switch { 0 => 360, 2 => 640, _ => 480 };
            string snapTime = string.IsNullOrWhiteSpace(TxtThumbnailTime.Text) ? "00:00:01" : TxtThumbnailTime.Text.Trim();
            (int resW, int resH) = CmbResolution.SelectedIndex switch
            {
                1 => (1280, 720),
                2 => (854, 480),
                3 => (3840, 2160),
                _ => (1920, 1080)
            };
            string tStart = string.IsNullOrWhiteSpace(TxtTrimStart.Text) ? "00:00:00" : TxtTrimStart.Text.Trim();
            string tEnd = string.IsNullOrWhiteSpace(TxtTrimEnd.Text) ? "00:00:30" : TxtTrimEnd.Text.Trim();

            int successCount = 0;
            bool wasCancelled = false;

            await Task.Run(async () =>
            {
                for (int i = 0; i < _mediaItems.Count; i++)
                {
                    if (token.IsCancellationRequested || SystemCore.CheckEmergencyStop())
                    {
                        wasCancelled = true;
                        break;
                    }

                    var item = _mediaItems[i];
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = "⏳ Đang xử lý...";
                        item.StatusColor = "#06B6D4";
                    });

                    string inDir = Path.GetDirectoryName(item.FilePath)!;
                    string outDir = string.IsNullOrWhiteSpace(customOutDir) ? inDir : customOutDir;
                    if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                    _lastOutputDir = outDir;

                    string baseName = Path.GetFileNameWithoutExtension(item.FilePath);
                    string outPath = "";
                    bool ok = false;

                    try
                    {
                        switch (mode)
                        {
                            case 0: // Nén Video
                                outPath = Path.Combine(outDir, $"{baseName}_compressed.mp4");
                                ok = await _media.CompressVideoAsync(item.FilePath, outPath, crf, token);
                                break;
                            case 1: // Làm nét
                                outPath = Path.Combine(outDir, $"{baseName}_enhanced.mp4");
                                ok = await _media.EnhanceMediaAsync(item.FilePath, outPath, token);
                                break;
                            case 2: // MP3
                                outPath = Path.Combine(outDir, $"{baseName}.mp3");
                                ok = await _media.ExtractAudioMp3Async(item.FilePath, outPath, bitrate, token);
                                break;
                            case 3: // Tốc độ
                                outPath = Path.Combine(outDir, $"{baseName}_{speed}x.mp4");
                                ok = await _media.ChangeVideoSpeedAsync(item.FilePath, outPath, speed, token);
                                break;
                            case 4: // Chuyển định dạng
                                outPath = Path.Combine(outDir, $"{baseName}_converted{targetExt}");
                                ok = await _media.ConvertFormatAsync(item.FilePath, outPath, token);
                                break;
                            case 5: // Tắt âm thanh
                                outPath = Path.Combine(outDir, $"{baseName}_muted.mp4");
                                ok = await _media.RemoveAudioAsync(item.FilePath, outPath, token);
                                break;
                            case 6: // Video to GIF
                                outPath = Path.Combine(outDir, $"{baseName}.gif");
                                ok = await _media.VideoToGifAsync(item.FilePath, outPath, 12, gifWidth, token);
                                break;
                            case 7: // Thumbnail Frame
                                outPath = Path.Combine(outDir, $"{baseName}_thumb.jpg");
                                ok = await _media.ExtractThumbnailAsync(item.FilePath, outPath, snapTime, token);
                                break;
                            case 8: // Đổi độ phân giải
                                outPath = Path.Combine(outDir, $"{baseName}_{resH}p.mp4");
                                ok = await _media.ResizeVideoAsync(item.FilePath, outPath, resW, resH, token);
                                break;
                            case 9: // Trim Video
                                outPath = Path.Combine(outDir, $"{baseName}_trimmed.mp4");
                                ok = await _media.TrimVideoAsync(item.FilePath, outPath, tStart, tEnd, token);
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        wasCancelled = true;
                        break;
                    }
                    catch
                    {
                        ok = false;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        if (ok)
                        {
                            item.Status = "✅ Hoàn thành";
                            item.StatusColor = "#10B981";
                            item.OutputPath = outPath;
                            successCount++;
                        }
                        else
                        {
                            item.Status = "❌ Thất bại";
                            item.StatusColor = "#EF4444";
                        }

                        PbBatchTotal.Value = i + 1;
                    });
                }
            });

            _isBatchRunning = false;
            string finalStatus = wasCancelled 
                ? $"Đã ngắt xử lý! Hoàn tất {successCount}/{_mediaItems.Count} tệp."
                : $"Đã xử lý xong: {successCount}/{_mediaItems.Count} tệp thành công!";

            SetBatchRunning(false, finalStatus);
        }

        private void BtnOpenResultFolder_Click(object sender, RoutedEventArgs e)
        {
            string dirToOpen = !string.IsNullOrWhiteSpace(TxtCustomOutputDir.Text) && Directory.Exists(TxtCustomOutputDir.Text)
                ? TxtCustomOutputDir.Text
                : _lastOutputDir;

            if (string.IsNullOrWhiteSpace(dirToOpen) || !Directory.Exists(dirToOpen))
            {
                if (_mediaItems.Count > 0 && File.Exists(_mediaItems[0].FilePath))
                {
                    dirToOpen = Path.GetDirectoryName(_mediaItems[0].FilePath)!;
                }
            }

            if (!string.IsNullOrWhiteSpace(dirToOpen) && Directory.Exists(dirToOpen))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dirToOpen,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show("Chưa có thư mục kết quả để mở!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SetBatchRunning(bool running, string statusText)
        {
            PbBatchTotal.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtBatchStatus.Visibility = Visibility.Visible;
            TxtBatchStatus.Text = statusText;

            BtnStartBatch.Content = running ? "🛑 DỪNG LẠI (CANCEL)" : "🚀 BẮT ĐẦU XỬ LÝ HÀNG LOẠT";
            BtnStartBatch.Style = running 
                ? (Style)FindResource("DangerButton") 
                : (Style)FindResource("PrimaryButton");

            BtnAddFiles.IsEnabled = !running;
            BtnAddFolder.IsEnabled = !running;
            BtnClearTable.IsEnabled = !running;
            CmbProcessingMode.IsEnabled = !running;
        }

        // ================= TAB 2: STEGANOGRAPHY & TIỆN ÍCH =================
        private void BtnBrowseContainer_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Media vỏ bọc (Ảnh / Video)",
                Filter = "Media Files (*.mp4;*.mkv;*.jpg;*.png;*.mp3)|*.mp4;*.mkv;*.jpg;*.png;*.mp3|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true) TxtStegoContainer.Text = dlg.FileName;
        }

        private void BtnBrowseSecret_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp bí mật cần giấu",
                Filter = "All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true) TxtStegoSecret.Text = dlg.FileName;
        }

        private void BtnBrowseExtractTarget_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Media có chứa dữ liệu ẩn",
                Filter = "All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true) TxtStegoExtractTarget.Text = dlg.FileName;
        }

        private void BtnBrowseNormalizeFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Chọn thư mục cần chuẩn hóa tên tệp"
            };
            if (dlg.ShowDialog() == true) TxtNormalizeFolder.Text = dlg.FolderName;
        }

        private async void BtnHideFile_Click(object sender, RoutedEventArgs e)
        {
            string container = TxtStegoContainer.Text.Trim();
            string secret = TxtStegoSecret.Text.Trim();
            if (!File.Exists(container) || !File.Exists(secret))
            {
                MessageBox.Show("Vui lòng chọn đầy đủ Tệp vỏ bọc và Tệp bí mật!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ext = Path.GetExtension(container);
            string outPath = Path.Combine(Path.GetDirectoryName(container)!, $"{Path.GetFileNameWithoutExtension(container)}_hidden{ext}");

            _stegoCts?.Cancel();
            _stegoCts = new CancellationTokenSource();

            SetStegoRunning(true, "Đang mã hóa & giấu tệp vào Media...");
            bool ok = await _media.HideFileInMediaAsync(container, secret, outPath, _stegoCts.Token);
            SetStegoRunning(false, ok ? $"Đã giấu tệp thành công: {outPath}" : "Giấu tệp thất bại!");
        }

        private async void BtnExtractSecret_Click(object sender, RoutedEventArgs e)
        {
            string container = TxtStegoExtractTarget.Text.Trim();
            if (!File.Exists(container))
            {
                MessageBox.Show("Vui lòng chọn tệp Media cần quét!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outDir = Path.GetDirectoryName(container)!;

            _stegoCts?.Cancel();
            _stegoCts = new CancellationTokenSource();

            SetStegoRunning(true, "Đang quét & giải mã tệp ẩn...");
            bool ok = await _media.ExtractHiddenFileAsync(container, outDir, _stegoCts.Token);
            SetStegoRunning(false, ok ? $"Đã giải nén tệp ẩn vào thư mục: {outDir}" : "Không tìm thấy dữ liệu ẩn!");
        }

        private void BtnNormalizeNames_Click(object sender, RoutedEventArgs e)
        {
            string folder = TxtNormalizeFolder.Text.Trim();
            if (!Directory.Exists(folder))
            {
                MessageBox.Show("Vui lòng chọn một thư mục hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _media.NormalizeFilenamesInDirectory(folder);
            MessageBox.Show("Đã hoàn tất chuẩn hóa tên tệp trong thư mục!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetStegoRunning(bool running, string text)
        {
            PbStego.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            TxtStegoStatus.Visibility = Visibility.Visible;
            TxtStegoStatus.Text = text;
            BtnHideFile.IsEnabled = !running;
            BtnExtractSecret.IsEnabled = !running;
            BtnNormalizeNames.IsEnabled = !running;
        }
    }
}
