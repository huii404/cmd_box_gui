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
        private bool _isBatchRunning = false;
        private bool _isFfmpegChecked = false;

        // Tệp video nguồn cho các Tab chuyên sâu
        private string _audioSourceVideo = string.Empty;
        private string _trimSourceVideo = string.Empty;

        public MediaView()
        {
            InitializeComponent();
            DgMediaFiles.ItemsSource = _mediaItems;
            UpdateTableNotice();
        }

        private void MediaView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isFfmpegChecked)
            {
                _isFfmpegChecked = true;
                CheckFfmpegStatus();
            }
        }

        private void CheckFfmpegStatus(bool force = false)
        {
            _ = Task.Run(async () =>
            {
                var (ok, path, info) = await _media.GetFFmpegStatusAsync(force);
                Dispatcher.Invoke(() =>
                {
                    if (ok)
                    {
                        TxtFfmpegStatus.Text = "FFmpeg: Sẵn sàng";
                        TxtFfmpegStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentSuccess");
                        BtnBrowseFfmpeg.ToolTip = $"FFmpeg: Sẵn sàng\n{info}\nĐường dẫn: {path}";
                    }
                    else
                    {
                        TxtFfmpegStatus.Text = "FFmpeg: Chưa cấu hình";
                        TxtFfmpegStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentDanger");
                        BtnBrowseFfmpeg.ToolTip = "FFmpeg: Chưa tìm thấy!\nBấm để chọn file ffmpeg.exe thủ công";
                    }
                });
            });
        }

        private void BtnRefreshFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            CheckFfmpegStatus(true);
        }

        #region ================= 0. SUB-TABS NAVIGATION =================
        private void TabMedia_Click(object sender, RoutedEventArgs e)
        {
            if (PnlMediaBatch == null || PnlMediaAudio == null || PnlMediaTrimGif == null || PnlMediaToolbox == null) return;

            PnlMediaBatch.Visibility = Visibility.Collapsed;
            PnlMediaAudio.Visibility = Visibility.Collapsed;
            PnlMediaTrimGif.Visibility = Visibility.Collapsed;
            PnlMediaToolbox.Visibility = Visibility.Collapsed;

            if (TabMediaBatch.IsChecked == true)
            {
                PnlMediaBatch.Visibility = Visibility.Visible;
                TxtStatus.Text = "Tab Nén & Đổi Đuôi Hàng Loạt: Kéo thả nhiều ảnh/video để xử lý cùng lúc.";
            }
            else if (TabMediaAudio.IsChecked == true)
            {
                PnlMediaAudio.Visibility = Visibility.Visible;
                TxtStatus.Text = "Tab Tách Âm Thanh: Chọn Video để trích xuất MP3, AAC, FLAC, WAV, M4A.";
            }
            else if (TabMediaTrimGif.IsChecked == true)
            {
                PnlMediaTrimGif.Visibility = Visibility.Visible;
                TxtStatus.Text = "Tab Cắt & GIF: Nhập thời gian cắt siêu tốc hoặc xuất ảnh GIF palette tối ưu.";
            }
            else if (TabMediaToolbox.IsChecked == true)
            {
                PnlMediaToolbox.Visibility = Visibility.Visible;
                TxtStatus.Text = "Tab Hộp Công Cụ: Các tác vụ nhanh 1-click (Mute audio, Chụp frame PNG, Watermark, yt-dlp).";
            }
        }
        #endregion

        #region ================= TAB 1: BATCH PROCESSOR =================
        private async void BtnBrowseFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp thực thi ffmpeg.exe",
                Filter = "FFmpeg (ffmpeg.exe)|ffmpeg.exe|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true && File.Exists(dlg.FileName))
            {
                _media.SetManualFfmpegPath(dlg.FileName);
                var (ok, path, info) = await _media.GetFFmpegStatusAsync(true);
                BtnBrowseFfmpeg.ToolTip = ok ? $"FFmpeg: Sẵn sàng\n{info}\nĐường dẫn: {path}" : "FFmpeg lỗi";
                CheckFfmpegStatus(true);
                MessageBox.Show($"Đã cấu hình FFmpeg thành công:\n{dlg.FileName}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CmbActionFeature_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PnlOptionCompress == null || PnlOptionConvert == null || PnlOptionEnhance == null) return;

            PnlOptionCompress.Visibility = Visibility.Collapsed;
            PnlOptionConvert.Visibility = Visibility.Collapsed;
            PnlOptionEnhance.Visibility = Visibility.Collapsed;

            switch (CmbActionFeature.SelectedIndex)
            {
                case 0: PnlOptionCompress.Visibility = Visibility.Visible; break;
                case 1: PnlOptionConvert.Visibility = Visibility.Visible; break;
                case 2: PnlOptionEnhance.Visibility = Visibility.Visible; break;
            }
        }

        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn các tệp Media",
                Filter = "Media Files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var file in dlg.FileNames) AddMediaFile(file);
                UpdateTableNotice();
                e.Handled = true;
            }
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog { Title = "Chọn thư mục Media" };
            if (dlg.ShowDialog() == true)
            {
                var files = Directory.GetFiles(dlg.FolderName, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => MediaService.IsImageFile(f) || MediaService.IsVideoFile(f));
                foreach (var file in files) AddMediaFile(file);
                UpdateTableNotice();
                e.Handled = true;
            }
        }

        private void MediaGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void MediaGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var path in files)
                {
                    if (File.Exists(path)) AddMediaFile(path);
                    else if (Directory.Exists(path))
                    {
                        var dirFiles = Directory.GetFiles(path).Where(f => MediaService.IsImageFile(f) || MediaService.IsVideoFile(f));
                        foreach (var df in dirFiles) AddMediaFile(df);
                    }
                }
                UpdateTableNotice();
                e.Handled = true;
            }
        }

        public void ImportExternalFiles(System.Collections.Generic.IEnumerable<string> paths)
        {
            if (_isBatchRunning) return;
            foreach (var path in paths)
            {
                if (File.Exists(path)) AddMediaFile(path);
                else if (Directory.Exists(path))
                {
                    var dirFiles = Directory.GetFiles(path).Where(f => MediaService.IsImageFile(f) || MediaService.IsVideoFile(f));
                    foreach (var df in dirFiles) AddMediaFile(df);
                }
            }
            UpdateTableNotice();
        }

        private void AddMediaFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
            if (_mediaItems.Any(i => i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))) return;

            bool isImg = MediaService.IsImageFile(filePath);
            bool isVid = MediaService.IsVideoFile(filePath);

            _mediaItems.Add(new MediaBatchItem
            {
                FilePath = filePath,
                FileSizeBytes = new FileInfo(filePath).Length,
                IsImage = isImg,
                IsVideo = isVid,
                AvailableExtensions = MediaService.GetAvailableExtensions(filePath),
                TargetExtension = MediaService.GetDefaultTargetExtension(filePath),
                Status = "⚪ Sẵn sàng",
                StatusColor = "#9CA3AF"
            });
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchRunning) return;
            if (sender is Button btn && btn.DataContext is MediaBatchItem item)
            {
                _mediaItems.Remove(item);
                UpdateTableNotice();
                e.Handled = true;
            }
        }

        private void BtnClearTable_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchRunning) return;
            _mediaItems.Clear();
            UpdateTableNotice();
        }

        private void UpdateTableNotice()
        {
            int count = _mediaItems.Count;
            TxtTableCount.Text = $"{count} tệp trong danh sách";
            PnlEmptyNotice.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BtnStartBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_isBatchRunning)
            {
                _batchCts?.Cancel();
                Logger.Warning("Đang yêu cầu dừng tác vụ xử lý hàng loạt...");
                return;
            }

            if (_mediaItems.Count == 0)
            {
                MessageBox.Show("Vui lòng thêm ít nhất một tệp Media vào danh sách!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _batchCts = new CancellationTokenSource();
            var token = _batchCts.Token;

            _isBatchRunning = true;
            SetBatchRunning(true);
            PbBatchTotal.Maximum = _mediaItems.Count;
            PbBatchTotal.Value = 0;

            int actionIndex = CmbActionFeature.SelectedIndex;
            int enhanceLevel = CmbEnhanceLevel?.SelectedIndex switch
            {
                0 => 0,
                2 => 2,
                3 => 3,
                _ => 1
            };

            await Task.Run(async () =>
            {
                for (int i = 0; i < _mediaItems.Count; i++)
                {
                    if (token.IsCancellationRequested || SystemCore.CheckEmergencyStop()) break;

                    var item = _mediaItems[i];
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = "⏳ Đang chạy...";
                        item.StatusColor = "#06B6D4";
                        TxtStatus.Text = $"Đang xử lý [{i + 1}/{_mediaItems.Count}]: {item.FileName}";
                    });

                    string outDir = Path.GetDirectoryName(item.FilePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                    _lastOutputDir = outDir;

                    string baseName = Path.GetFileNameWithoutExtension(item.FilePath);
                    string origExt = Path.GetExtension(item.FilePath);
                    string targetExt = string.IsNullOrWhiteSpace(item.TargetExtension) ? origExt : item.TargetExtension;

                    string outPath;
                    bool ok = false;

                    try
                    {
                        switch (actionIndex)
                        {
                            case 0:
                                outPath = Path.Combine(outDir, $"{baseName}_compressed{origExt}");
                                ok = await _media.CompressMediaAsync(item.FilePath, outPath, 1, token);
                                break;
                            case 1:
                                outPath = Path.Combine(outDir, $"{baseName}_converted{targetExt}");
                                ok = await _media.ConvertMediaFormatAsync(item.FilePath, outPath, token);
                                break;
                            case 2:
                                outPath = Path.Combine(outDir, $"{baseName}_enhanced{targetExt}");
                                ok = await _media.EnhanceMediaAsync(item.FilePath, outPath, enhanceLevel, token);
                                break;
                            default:
                                outPath = Path.Combine(outDir, $"{baseName}_output{origExt}");
                                ok = false;
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Lỗi xử lý file [{item.FileName}]: {ex.Message}");
                        ok = false;
                        outPath = "";
                    }

                    Dispatcher.Invoke(() =>
                    {
                        if (ok && File.Exists(outPath))
                        {
                            item.OutputPath = outPath;
                            item.ProcessedSizeBytes = new FileInfo(outPath).Length;
                            item.Status = "✅ Đã xong";
                            item.StatusColor = "#10B981";
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
            SetBatchRunning(false);
            TxtStatus.Text = "Hoàn tất phiên xử lý hàng loạt!";
        }

        private void BtnOpenResultFolder_Click(object sender, RoutedEventArgs e)
        {
            string dirToOpen = _lastOutputDir;
            if (string.IsNullOrWhiteSpace(dirToOpen) || !Directory.Exists(dirToOpen))
            {
                if (_mediaItems.Count > 0 && File.Exists(_mediaItems[0].FilePath))
                {
                    dirToOpen = Path.GetDirectoryName(_mediaItems[0].FilePath)!;
                }
            }

            if (!string.IsNullOrWhiteSpace(dirToOpen) && Directory.Exists(dirToOpen))
            {
                Process.Start(new ProcessStartInfo { FileName = dirToOpen, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("Chưa có thư mục kết quả để mở!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SetBatchRunning(bool running)
        {
            PbBatchTotal.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            BtnStartBatch.Content = running ? "🛑 Dừng Lại" : "🚀 Bắt Đầu Xử Lý";
            BtnStartBatch.Style = (Style)FindResource(running ? "DangerButton" : "PrimaryButton");

            BtnAddFiles.IsEnabled = !running;
            BtnAddFolder.IsEnabled = !running;
            BtnClearTable.IsEnabled = !running;
            BtnBrowseFfmpeg.IsEnabled = !running;
            CmbActionFeature.IsEnabled = !running;
            CmbEnhanceLevel.IsEnabled = !running;
            DgMediaFiles.IsEnabled = !running;
        }
        #endregion

        #region ================= TAB 2: AUDIO EXTRACTOR =================
        private void AudioDrop_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void AudioDrop_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                var vid = files.FirstOrDefault(f => MediaService.IsVideoFile(f));
                if (!string.IsNullOrEmpty(vid))
                {
                    SetAudioSelectedFile(vid);
                }
                else
                {
                    MessageBox.Show("Vui lòng kéo thả tệp Video hợp lệ (MP4, MKV, AVI, MOV...)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                e.Handled = true;
            }
        }

        private void BtnSelectAudioVideo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Video cần tách âm thanh",
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.wmv;*.ts|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                SetAudioSelectedFile(dlg.FileName);
            }
        }

        private void SetAudioSelectedFile(string path)
        {
            _audioSourceVideo = path;
            TxtAudioSelectedFile.Text = $"{Path.GetFileName(path)} ({SystemCore.FormatBytes(new FileInfo(path).Length)})";
            TxtAudioTrackInfo.Text = "Định dạng âm thanh: Tự động nhận diện qua FFmpeg";
            TxtStatus.Text = $"Đã nạp video: {Path.GetFileName(path)}";
        }

        private async void BtnExtractAudioRun_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_audioSourceVideo) || !File.Exists(_audioSourceVideo))
            {
                MessageBox.Show("Vui lòng chọn một tệp Video nguồn trước khi trích xuất âm thanh!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fmt = (CmbAudioOutFormat.SelectedIndex) switch
            {
                1 => "aac",
                2 => "flac",
                3 => "wav",
                4 => "m4a",
                _ => "mp3"
            };

            string outDir = Path.GetDirectoryName(_audioSourceVideo) ?? AppDomain.CurrentDomain.BaseDirectory;
            string baseName = Path.GetFileNameWithoutExtension(_audioSourceVideo);
            string outPath = Path.Combine(outDir, $"{baseName}_audio.{fmt}");

            BtnExtractAudioRun.IsEnabled = false;
            PbBatchTotal.Visibility = Visibility.Visible;
            PbBatchTotal.IsIndeterminate = true;
            TxtStatus.Text = $"Đang trích xuất âm thanh .{fmt} từ {Path.GetFileName(_audioSourceVideo)}...";

            try
            {
                bool ok = await _media.ExtractAudioAsync(_audioSourceVideo, outPath, fmt);
                if (ok && File.Exists(outPath))
                {
                    TxtStatus.Text = $"Trích xuất thành công: {Path.GetFileName(outPath)} ({SystemCore.FormatBytes(new FileInfo(outPath).Length)})";
                    _lastOutputDir = outDir;
                    MessageBox.Show($"Trích xuất âm thanh thành công!\n\nĐã lưu: {outPath}", "Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    TxtStatus.Text = "Trích xuất âm thanh thất bại! Vui lòng kiểm tra FFmpeg.";
                    MessageBox.Show("Không thể trích xuất âm thanh từ video này. Hãy đảm bảo video có chứa track âm thanh và FFmpeg đã sẵn sàng.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                BtnExtractAudioRun.IsEnabled = true;
                PbBatchTotal.IsIndeterminate = false;
                PbBatchTotal.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region ================= TAB 3: TRIM & GIF MAKER =================
        private void BtnSelectTrimVideo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Video để Cắt hoặc Tạo GIF",
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.wmv;*.ts|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                _trimSourceVideo = dlg.FileName;
                TxtTrimSelectedFile.Text = $"{Path.GetFileName(dlg.FileName)} ({SystemCore.FormatBytes(new FileInfo(dlg.FileName).Length)})";
                TxtStatus.Text = $"Đã nạp video cho Cắt / GIF: {Path.GetFileName(dlg.FileName)}";
            }
        }

        private async void BtnTrimVideoFast_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_trimSourceVideo) || !File.Exists(_trimSourceVideo))
            {
                MessageBox.Show("Vui lòng chọn một tệp Video trước khi cắt!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtTrimStartSec.Text.Trim(), out double startSec) || startSec < 0)
            {
                MessageBox.Show("Thời điểm bắt đầu không hợp lệ (nhập số giây, ví dụ: 0, 15, 60)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtTrimDurationSec.Text.Trim(), out double durSec) || durSec <= 0)
            {
                MessageBox.Show("Thời lượng cắt không hợp lệ (nhập số giây, ví dụ: 5, 10, 30)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outDir = Path.GetDirectoryName(_trimSourceVideo) ?? AppDomain.CurrentDomain.BaseDirectory;
            string baseName = Path.GetFileNameWithoutExtension(_trimSourceVideo);
            string ext = Path.GetExtension(_trimSourceVideo);
            string outPath = Path.Combine(outDir, $"{baseName}_cut_{startSec}s_to_{startSec + durSec}s{ext}");

            BtnTrimVideoFast.IsEnabled = false;
            PbBatchTotal.Visibility = Visibility.Visible;
            PbBatchTotal.IsIndeterminate = true;
            TxtStatus.Text = $"Đang cắt video qua Stream Copy: {startSec}s ➔ {startSec + durSec}s...";

            try
            {
                bool ok = await _media.TrimVideoAsync(_trimSourceVideo, outPath, startSec, durSec);
                if (ok && File.Exists(outPath))
                {
                    _lastOutputDir = outDir;
                    TxtStatus.Text = $"Cắt video hoàn tất: {Path.GetFileName(outPath)}";
                    MessageBox.Show($"Cắt video siêu tốc thành công!\n\nĐã lưu: {outPath}", "Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    TxtStatus.Text = "Cắt video thất bại!";
                    MessageBox.Show("Cắt video thất bại. Vui lòng kiểm tra lại mốc thời gian và định dạng video.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                BtnTrimVideoFast.IsEnabled = true;
                PbBatchTotal.IsIndeterminate = false;
                PbBatchTotal.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnMakeGifFast_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_trimSourceVideo) || !File.Exists(_trimSourceVideo))
            {
                MessageBox.Show("Vui lòng chọn một tệp Video trước khi tạo GIF!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(TxtTrimStartSec.Text.Trim(), out double startSec) || startSec < 0) startSec = 0;
            if (!double.TryParse(TxtTrimDurationSec.Text.Trim(), out double durSec) || durSec <= 0) durSec = 5;

            int scaleWidth = CmbGifScale.SelectedIndex switch
            {
                1 => 360,
                2 => 720,
                3 => 0,
                _ => 480
            };

            int fps = CmbGifFpsRate.SelectedIndex switch
            {
                0 => 12,
                2 => 24,
                3 => 30,
                _ => 15
            };

            string outDir = Path.GetDirectoryName(_trimSourceVideo) ?? AppDomain.CurrentDomain.BaseDirectory;
            string baseName = Path.GetFileNameWithoutExtension(_trimSourceVideo);
            string outPath = Path.Combine(outDir, $"{baseName}_anim_{scaleWidth}p.gif");

            BtnMakeGifFast.IsEnabled = false;
            PbBatchTotal.Visibility = Visibility.Visible;
            PbBatchTotal.IsIndeterminate = true;
            TxtStatus.Text = $"Đang tạo ảnh GIF ({durSec}s, {scaleWidth}p, {fps}fps, Palettegen 256 màu)...";

            try
            {
                bool ok = await _media.ConvertToGifAsync(_trimSourceVideo, outPath, startSec, durSec, scaleWidth, fps);
                if (ok && File.Exists(outPath))
                {
                    _lastOutputDir = outDir;
                    TxtStatus.Text = $"Tạo ảnh GIF thành công: {Path.GetFileName(outPath)} ({SystemCore.FormatBytes(new FileInfo(outPath).Length)})";
                    MessageBox.Show($"Tạo ảnh GIF chất lượng cao thành công!\n\nĐã lưu: {outPath}", "Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    TxtStatus.Text = "Tạo ảnh GIF thất bại!";
                    MessageBox.Show("Tạo ảnh GIF thất bại. Vui lòng kiểm tra lại video nguồn và cấu hình FFmpeg.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                BtnMakeGifFast.IsEnabled = true;
                PbBatchTotal.IsIndeterminate = false;
                PbBatchTotal.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region ================= TAB 4: MEDIA TOOLBOX =================
        private async void BtnToolMuteVideo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Video cần tắt tiếng (Mute)",
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.wmv;*.ts|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string input = dlg.FileName;
                string outDir = Path.GetDirectoryName(input) ?? AppDomain.CurrentDomain.BaseDirectory;
                string baseName = Path.GetFileNameWithoutExtension(input);
                string ext = Path.GetExtension(input);
                string outPath = Path.Combine(outDir, $"{baseName}_muted{ext}");

                PbBatchTotal.Visibility = Visibility.Visible;
                PbBatchTotal.IsIndeterminate = true;
                TxtStatus.Text = $"Đang loại bỏ âm thanh từ {Path.GetFileName(input)}...";

                try
                {
                    bool ok = await _media.MuteVideoAsync(input, outPath);
                    if (ok && File.Exists(outPath))
                    {
                        _lastOutputDir = outDir;
                        TxtStatus.Text = $"Tắt tiếng thành công: {Path.GetFileName(outPath)}";
                        MessageBox.Show($"Đã tạo bản sao không tiếng thành công (0.1s Stream Copy):\n{outPath}", "Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        TxtStatus.Text = "Tắt tiếng thất bại!";
                        MessageBox.Show("Không thể tắt tiếng video. Vui lòng kiểm tra FFmpeg.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                finally
                {
                    PbBatchTotal.IsIndeterminate = false;
                    PbBatchTotal.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void BtnToolExtractFrame_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp Video để chụp frame ảnh",
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.wmv;*.ts|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string input = dlg.FileName;
                string outDir = Path.GetDirectoryName(input) ?? AppDomain.CurrentDomain.BaseDirectory;
                string baseName = Path.GetFileNameWithoutExtension(input);
                string outPath = Path.Combine(outDir, $"{baseName}_snapshot.png");

                PbBatchTotal.Visibility = Visibility.Visible;
                PbBatchTotal.IsIndeterminate = true;
                TxtStatus.Text = $"Đang trích xuất frame từ {Path.GetFileName(input)} ra PNG...";

                try
                {
                    bool ok = await _media.ExtractFrameAsync(input, outPath, 1.0);
                    if (ok && File.Exists(outPath))
                    {
                        _lastOutputDir = outDir;
                        TxtStatus.Text = $"Chụp frame ảnh thành công: {Path.GetFileName(outPath)}";
                        MessageBox.Show($"Đã chụp frame ảnh sắc nét ra PNG thành công:\n{outPath}", "Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        TxtStatus.Text = "Chụp frame ảnh thất bại!";
                        MessageBox.Show("Không thể trích xuất frame ảnh từ video này.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                finally
                {
                    PbBatchTotal.IsIndeterminate = false;
                    PbBatchTotal.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnToolWatermark_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Tính năng Đóng Dấu Watermark:\n\n" +
                "Cho phép chèn logo ảnh mờ (PNG trong suốt) hoặc dòng chữ bản quyền vào góc video.\n" +
                "Tính năng này sẽ được kích hoạt với bộ lọc FFmpeg overlay trong bản cập nhật tới!",
                "Đóng Dấu Bản Quyền (Watermark)",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnToolYtDlp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Trình Tải Video yt-dlp:\n\n" +
                "Hỗ trợ dán đường link (URL) từ YouTube, TikTok, Facebook, Bilibili để tải video chất lượng 1080p/4K hoặc tách riêng track âm thanh MP3.\n" +
                "Hệ thống sẽ tự động gọi lệnh yt-dlp cùng FFmpeg để xuất file hoàn chỉnh.",
                "Tải Video Trực Tuyến (yt-dlp)",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        #endregion
    }
}
