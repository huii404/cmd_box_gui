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
                _ = Task.Run(async () =>
                {
                    var (ok, path, info) = await _media.GetFFmpegStatusAsync();
                    Dispatcher.Invoke(() =>
                    {
                        BtnBrowseFfmpeg.ToolTip = ok
                            ? $"FFmpeg: Sẵn sàng\n{info}\nĐường dẫn: {path}"
                            : "FFmpeg: Chưa tìm thấy!\nBấm để chọn file ffmpeg.exe thủ công";
                    });
                });
            }
        }

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
                MessageBox.Show($"Đã cấu hình FFmpeg thành công:\n{dlg.FileName}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CmbActionFeature_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PnlOptionCompress == null || PnlOptionConvert == null || PnlOptionEnhance == null ||
                PnlOptionExtractAudio == null || PnlOptionMuteVideo == null || PnlOptionMakeGif == null) return;

            PnlOptionCompress.Visibility = Visibility.Collapsed;
            PnlOptionConvert.Visibility = Visibility.Collapsed;
            PnlOptionEnhance.Visibility = Visibility.Collapsed;
            PnlOptionExtractAudio.Visibility = Visibility.Collapsed;
            PnlOptionMuteVideo.Visibility = Visibility.Collapsed;
            PnlOptionMakeGif.Visibility = Visibility.Collapsed;

            switch (CmbActionFeature.SelectedIndex)
            {
                case 0: PnlOptionCompress.Visibility = Visibility.Visible; break;
                case 1: PnlOptionConvert.Visibility = Visibility.Visible; break;
                case 2: PnlOptionEnhance.Visibility = Visibility.Visible; break;
                case 3: PnlOptionExtractAudio.Visibility = Visibility.Visible; break;
                case 4: PnlOptionMuteVideo.Visibility = Visibility.Visible; break;
                case 5: PnlOptionMakeGif.Visibility = Visibility.Visible; break;
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
            }
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

            string audioFormat = (CmbAudioFormat?.SelectedIndex ?? 0) switch
            {
                1 => "aac",
                2 => "wav",
                3 => "flac",
                4 => "m4a",
                _ => "mp3"
            };

            double gifStartSec = 0;
            if (TxtGifStart != null && double.TryParse(TxtGifStart.Text.Trim(), out double parsedStart) && parsedStart >= 0)
            {
                gifStartSec = parsedStart;
            }

            double gifDurationSec = CmbGifDuration?.SelectedIndex switch
            {
                0 => 3,
                1 => 5,
                2 => 10,
                _ => 5
            };

            const int gifScaleWidth = 480;
            const int gifFps = 12;

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
                    });

                    string outDir = Path.GetDirectoryName(item.FilePath)!;
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
                            case 3:
                                outPath = Path.Combine(outDir, $"{baseName}_audio.{audioFormat}");
                                ok = await _media.ExtractAudioAsync(item.FilePath, outPath, audioFormat, token);
                                break;
                            case 4:
                                outPath = Path.Combine(outDir, $"{baseName}_muted{origExt}");
                                ok = await _media.MuteVideoAsync(item.FilePath, outPath, token);
                                break;
                            case 5:
                                outPath = Path.Combine(outDir, $"{baseName}_anim.gif");
                                ok = await _media.ConvertToGifAsync(item.FilePath, outPath, gifStartSec, gifDurationSec, gifScaleWidth, gifFps, token);
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
            BtnStartBatch.Content = running ? "🛑 Dừng Lại" : "🚀 Bắt Đầu";
            BtnStartBatch.Style = (Style)FindResource(running ? "DangerButton" : "PrimaryButton");

            BtnAddFiles.IsEnabled = !running;
            BtnAddFolder.IsEnabled = !running;
            BtnClearTable.IsEnabled = !running;
            BtnBrowseFfmpeg.IsEnabled = !running;
            CmbActionFeature.IsEnabled = !running;
            CmbEnhanceLevel.IsEnabled = !running;
            if (CmbAudioFormat != null) CmbAudioFormat.IsEnabled = !running;
            if (TxtGifStart != null) TxtGifStart.IsEnabled = !running;
            if (CmbGifDuration != null) CmbGifDuration.IsEnabled = !running;
            DgMediaFiles.IsEnabled = !running;
        }
    }
}
