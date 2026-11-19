using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Services;
using Microsoft.Win32;

namespace CMD_BOX_GUI.UI.Views
{
    public partial class MediaView : UserControl
    {
        private sealed class MediaCardConfig
        {
            public string Key { get; init; } = string.Empty;
            public string Title { get; init; } = string.Empty;
            public string DialogTitle { get; init; } = string.Empty;
            public string DialogFilter { get; init; } = string.Empty;
            public Func<string, bool> FileFilter { get; init; } = _ => true;
            public TextBlock CountLabel { get; init; } = null!;
            public Border CardBorder { get; init; } = null!;
            public Func<string, string, CancellationToken, Task<bool>> ProcessFileAsync { get; init; } = null!;
            public List<string> Files { get; } = new();
        }

        private readonly MediaService _mediaService = new();
        private readonly Dictionary<string, MediaCardConfig> _cards = new(StringComparer.OrdinalIgnoreCase);
        private string _activeCardKey = "compress_media";
        private bool _isAutoFormatMode = true;
        private string _customImageFormat = "auto";
        private string _customVideoFormat = ".mp4";
        private int _customFormatQuality = 0;
        private bool _isRunning;
        private CancellationTokenSource? _cts;
        private string? _lastOutputDirectory;

        private const string MediaFilter = "Video & Ảnh|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.ico;*.gif;*.heic;*.heif;*.avif;*.jfif|Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.flv;*.m4v|Image Files|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.ico;*.tiff;*.gif;*.heic;*.heif;*.avif;*.jfif|All Files (*.*)|*.*";
        private const string VideoFilter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.flv;*.m4v;*.ts;*.3gp|All Files (*.*)|*.*";
        private const string AudioOrVideoFilter = "Audio & Video|*.mp3;*.wav;*.flac;*.aac;*.m4a;*.ogg;*.mp4;*.mkv;*.avi;*.mov;*.webm|All Files (*.*)|*.*";

        public MediaView()
        {
            InitializeComponent();
            InitializeCardRegistry();
            SetActiveCard("compress_media");
        }

        private void MediaView_Loaded(object sender, RoutedEventArgs e)
        {
            _ = RefreshFfmpegStatusAsync(false);
        }

        // =========================================================================
        // 1. ĐĂNG KÝ 6 CARD GỌN NHẸ (GỘP CHUNG VIDEO & ẢNH)
        // =========================================================================

        private void InitializeCardRegistry()
        {
            RegisterCard(new MediaCardConfig
            {
                Key = "compress_media",
                Title = "Nén Dung Lượng",
                DialogTitle = "Chọn Video hoặc Ảnh cần nén giảm dung lượng",
                DialogFilter = MediaFilter,
                FileFilter = p => MediaService.IsVideoFile(p) || MediaService.IsImageFile(p),
                CountLabel = TxtCount_compress_media,
                CardBorder = Card_compress_media,
                ProcessFileAsync = (input, outDir, ct) =>
                {
                    int level = CmbCompressLevel.SelectedIndex;
                    string ext = MediaService.IsVideoFile(input) ? ".mp4" : Path.GetExtension(input);
                    string outPath = BuildUniqueOutputPath(outDir, input, "_compressed", ext);
                    return _mediaService.CompressMediaAsync(input, outPath, level, ct);
                }
            });

            RegisterCard(new MediaCardConfig
            {
                Key = "convert_media",
                Title = "Đổi Định Dạng",
                DialogTitle = "Chọn Video hoặc Ảnh cần đổi định dạng",
                DialogFilter = MediaFilter,
                FileFilter = p => MediaService.IsVideoFile(p) || MediaService.IsImageFile(p),
                CountLabel = TxtCount_convert_media,
                CardBorder = Card_convert_media,
                ProcessFileAsync = (input, outDir, ct) =>
                {
                    string ext = MediaService.ResolveTargetExtension(input, _isAutoFormatMode, _customImageFormat, _customVideoFormat);
                    int quality = _isAutoFormatMode ? 0 : _customFormatQuality;
                    string outPath = BuildUniqueOutputPath(outDir, input, "", ext);
                    return _mediaService.ConvertMediaFormatAsync(input, outPath, quality, ct);
                }
            });

            RegisterCard(new MediaCardConfig
            {
                Key = "enhance_media",
                Title = "Làm Nét Media",
                DialogTitle = "Chọn Video hoặc Ảnh cần làm nét & khử nhiễu",
                DialogFilter = MediaFilter,
                FileFilter = p => MediaService.IsVideoFile(p) || MediaService.IsImageFile(p),
                CountLabel = TxtCount_enhance_media,
                CardBorder = Card_enhance_media,
                ProcessFileAsync = (input, outDir, ct) =>
                {
                    int level = CmbEnhanceLevel.SelectedIndex;
                    string ext = MediaService.IsVideoFile(input) ? ".mp4" : Path.GetExtension(input);
                    string outPath = BuildUniqueOutputPath(outDir, input, "_enhanced", ext);
                    return _mediaService.EnhanceMediaAsync(input, outPath, level, ct);
                }
            });

            RegisterCard(new MediaCardConfig
            {
                Key = "strip_metadata",
                Title = "Xóa Metadata",
                DialogTitle = "Chọn Video hoặc Ảnh cần xóa Metadata (EXIF/GPS/Thông tin ẩn)",
                DialogFilter = MediaFilter,
                FileFilter = p => MediaService.IsVideoFile(p) || MediaService.IsImageFile(p),
                CountLabel = TxtCount_strip_metadata,
                CardBorder = Card_strip_metadata,
                ProcessFileAsync = (input, outDir, ct) =>
                {
                    string ext = MediaService.IsImageFile(input)
                        ? MediaService.GetDefaultTargetExtension(input)
                        : Path.GetExtension(input);
                    string outPath = BuildUniqueOutputPath(outDir, input, "_clean", ext);
                    return _mediaService.StripMetadataAsync(input, outPath, ct);
                }
            });

            RegisterCard(new MediaCardConfig
            {
                Key = "extract_audio",
                Title = "Tách Âm Thanh",
                DialogTitle = "Chọn Video cần tách nhạc",
                DialogFilter = VideoFilter,
                FileFilter = MediaService.IsVideoFile,
                CountLabel = TxtCount_extract_audio,
                CardBorder = Card_extract_audio,
                ProcessFileAsync = (input, outDir, ct) =>
                {
                    string fmt = (CmbExtractAudioFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "mp3";
                    string outPath = BuildUniqueOutputPath(outDir, input, "_audio", "." + fmt);
                    return _mediaService.ExtractAudioAsync(input, outPath, fmt, ct);
                }
            });

            RegisterCard(new MediaCardConfig
            {
                Key = "volume_audio",
                Title = "Chỉnh Âm Lượng",
                DialogTitle = "Chọn Audio hoặc Video cần chỉnh âm lượng",
                DialogFilter = AudioOrVideoFilter,
                FileFilter = p => MediaService.IsAudioFile(p) || MediaService.IsVideoFile(p),
                CountLabel = TxtCount_volume_audio,
                CardBorder = Card_volume_audio,
                ProcessFileAsync = (input, outDir, ct) =>
                {
                    string tag = (CmbAudioVolume.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1.5";
                    double mult = ParseDoubleOrDefault(tag, 1.5);
                    string ext = Path.GetExtension(input);
                    string outPath = BuildUniqueOutputPath(outDir, input, $"_vol{mult:0.#}x", ext);
                    return _mediaService.AdjustAudioVolumeAsync(input, outPath, mult, ct);
                }
            });

            RegisterCard(new MediaCardConfig
            {
                Key = "trim_video",
                Title = "Cắt Video Nhanh",
                DialogTitle = "Chọn Video cần cắt đoạn",
                DialogFilter = VideoFilter,
                FileFilter = MediaService.IsVideoFile,
                CountLabel = TxtCount_trim_video,
                CardBorder = Card_trim_video,
                ProcessFileAsync = (input, outDir, ct) =>
                {
                    double start = ParseDoubleOrDefault(TxtTrimStart.Text, 0);
                    double dur = ParseDoubleOrDefault(TxtTrimDuration.Text, 15);
                    string ext = Path.GetExtension(input);
                    string outPath = BuildUniqueOutputPath(outDir, input, $"_cut_{start:0}s", ext);
                    return _mediaService.TrimVideoAsync(input, outPath, start, dur, ct);
                }
            });
        }

        private void RegisterCard(MediaCardConfig config) => _cards[config.Key] = config;

        // =========================================================================
        // 2. ĐIỀU HƯỚNG TAB & QUẢN LÝ FILE TRÊN CARD
        // =========================================================================

        private void TabMedia_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;

            PnlTabGeneral.Visibility = rb == TabGeneral ? Visibility.Visible : Visibility.Collapsed;
            PnlTabAudioTrim.Visibility = rb == TabAudioTrim ? Visibility.Visible : Visibility.Collapsed;
            PnlTabTools.Visibility = rb == TabTools ? Visibility.Visible : Visibility.Collapsed;

            if (rb == TabGeneral)
            {
                SetActiveCard("compress_media");
            }
            else if (rb == TabAudioTrim)
            {
                SetActiveCard("extract_audio");
            }

            bool isToolTab = rb == TabTools;
            BtnRunActiveCard.Visibility = isToolTab ? Visibility.Collapsed : Visibility.Visible;
            BtnClearActiveCard.Visibility = isToolTab ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Card_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is string key)
            {
                SetActiveCard(key);
            }
        }

        private void SetActiveCard(string key)
        {
            if (!_cards.TryGetValue(key, out var activeCard)) return;
            _activeCardKey = key;

            var activeBrush = TryFindResource("AccentCyan") as Brush ?? Brushes.DeepSkyBlue;
            var normalBrush = TryFindResource("BorderSubtle") as Brush ?? Brushes.DimGray;

            foreach (var c in _cards.Values)
            {
                if (c.CardBorder == null) continue;
                bool isSelected = string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase);
                c.CardBorder.BorderBrush = isSelected ? activeBrush : normalBrush;
                c.CardBorder.BorderThickness = new Thickness(isSelected ? 1.5 : 1);
            }

            UpdateGlobalRunButton();
        }

        private void CardPickFiles_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.Tag is not string key) return;
            if (!_cards.TryGetValue(key, out var card)) return;

            SetActiveCard(key);

            var dlg = new OpenFileDialog
            {
                Title = card.DialogTitle,
                Filter = card.DialogFilter,
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                AddFilesToCard(card, dlg.FileNames);
            }
        }

        private void Card_Drop(object sender, DragEventArgs e)
        {
            if (sender is not FrameworkElement el || el.Tag is not string key) return;
            if (!_cards.TryGetValue(key, out var card)) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] dropped) return;

            SetActiveCard(key);
            var valid = dropped.Where(f => File.Exists(f) && card.FileFilter(f)).ToArray();
            if (valid.Length > 0)
            {
                AddFilesToCard(card, valid);
            }
            e.Handled = true;
        }

        /// <summary>
        /// Tiếp nhận tệp từ bên ngoài (MainWindow kéo thả hoặc nút mở nhanh)
        /// </summary>
        public void ImportExternalFiles(string[] files)
        {
            if (files == null || files.Length == 0) return;

            var media = files.Where(f => File.Exists(f) && (MediaService.IsVideoFile(f) || MediaService.IsImageFile(f))).ToArray();
            var audios = files.Where(f => File.Exists(f) && MediaService.IsAudioFile(f)).ToArray();

            if (media.Length > 0 && _cards.TryGetValue("compress_media", out var mediaCard))
            {
                AddFilesToCard(mediaCard, media);
                TabGeneral.IsChecked = true;
                TabMedia_Click(TabGeneral, new RoutedEventArgs());
                SetActiveCard("compress_media");
            }
            if (audios.Length > 0 && _cards.TryGetValue("volume_audio", out var audioCard))
            {
                AddFilesToCard(audioCard, audios);
                if (media.Length == 0)
                {
                    TabAudioTrim.IsChecked = true;
                    TabMedia_Click(TabAudioTrim, new RoutedEventArgs());
                    SetActiveCard("volume_audio");
                }
            }
        }

        private void AddFilesToCard(MediaCardConfig card, IEnumerable<string> paths)
        {
            int added = 0;
            foreach (var p in paths)
            {
                if (File.Exists(p) && !card.Files.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    card.Files.Add(p);
                    added++;
                }
            }

            UpdateCardCountDisplay(card);
            if (added > 0)
            {
                TxtStatus.Text = $"[{card.Title}] Đã thêm {added} tệp (Tổng: {card.Files.Count} tệp).";
                Logger.Info($"[Media] [{card.Title}] Đã nạp {added} tệp.");
            }
        }

        private void UpdateCardCountDisplay(MediaCardConfig card)
        {
            int count = card.Files.Count;
            card.CountLabel.Text = count == 0 ? "0 tệp" : $"📂 {count} tệp";
            card.CountLabel.Foreground = count > 0
                ? (TryFindResource("AccentSuccess") as Brush ?? Brushes.MediumSeaGreen)
                : (TryFindResource("TextSecondary") as Brush ?? Brushes.Gray);

            UpdateGlobalRunButton();
        }

        private void UpdateGlobalRunButton()
        {
            if (!_cards.TryGetValue(_activeCardKey, out var activeCard)) return;
            BtnRunActiveCard.Content = $"▶ Chạy: {activeCard.Title} ({activeCard.Files.Count})";
        }

        // =========================================================================
        // 3. THỰC THI 1 CARD ĐANG CHỌN MỖI PHIÊN (TIẾT KIỆM TÀI NGUYÊN)
        // =========================================================================

        private async void BtnRunActiveCard_Click(object sender, RoutedEventArgs e)
        {
            if (!_cards.TryGetValue(_activeCardKey, out var activeCard)) return;

            if (activeCard.Files.Count == 0)
            {
                var dlg = new OpenFileDialog
                {
                    Title = activeCard.DialogTitle,
                    Filter = activeCard.DialogFilter,
                    Multiselect = true
                };

                if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
                {
                    AddFilesToCard(activeCard, dlg.FileNames);
                }

                if (activeCard.Files.Count == 0) return;
            }

            await ExecuteCardsAsync(new[] { activeCard });
        }

        private async Task ExecuteCardsAsync(IReadOnlyList<MediaCardConfig> cardsToRun)
        {
            if (_isRunning) return;

            var status = await _mediaService.GetFFmpegStatusAsync();
            if (!status.isAvailable)
            {
                MessageBox.Show("Chưa tìm thấy ffmpeg.exe! Vui lòng vào tab 'Cấu Hình' để chọn đường dẫn ffmpeg.exe.", "Thiếu FFmpeg", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalFiles = cardsToRun.Sum(c => c.Files.Count);
            if (totalFiles == 0) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            SetControlsEnabled(false);

            PbBatchTotal.Visibility = Visibility.Visible;
            PbBatchTotal.Value = 0;
            PbBatchTotal.Maximum = totalFiles;

            int processed = 0;
            int successCount = 0;
            int failCount = 0;

            try
            {
                foreach (var card in cardsToRun)
                {
                    foreach (var inputPath in card.Files.ToList())
                    {
                        if (_cts.Token.IsCancellationRequested || SystemCore.CheckEmergencyStop())
                        {
                            Logger.Warning("[Media] Đã dừng tác vụ theo yêu cầu người dùng.");
                            break;
                        }

                        processed++;
                        string fileName = Path.GetFileName(inputPath);
                        TxtStatus.Text = $"[{card.Title}] Đang xử lý ({processed}/{totalFiles}): {fileName}...";

                        try
                        {
                            string baseDir = Path.GetDirectoryName(inputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                            string outDir = Path.Combine(baseDir, "CMD_BOX_Output");
                            Directory.CreateDirectory(outDir);
                            _lastOutputDirectory = outDir;

                            bool ok = await card.ProcessFileAsync(inputPath, outDir, _cts.Token);
                            if (ok) successCount++;
                            else failCount++;
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            Logger.Error($"[Media] [{card.Title}] Lỗi xử lý {fileName}: {ex.Message}");
                        }

                        PbBatchTotal.Value = processed;
                    }
                }

                TxtStatus.Text = $"Hoàn tất! Thành công: {successCount} tệp | Thất bại: {failCount} tệp.";
                Logger.Success($"[Media] Hoàn tất xử lý {processed} tệp (Thành công: {successCount}, Lỗi: {failCount}).");
            }
            finally
            {
                PbBatchTotal.Visibility = Visibility.Collapsed;
                _cts?.Dispose();
                _cts = null;
                _isRunning = false;
                SetControlsEnabled(true);
            }
        }

        private void BtnClearActiveCard_Click(object sender, RoutedEventArgs e)
        {
            if (!_cards.TryGetValue(_activeCardKey, out var activeCard)) return;
            activeCard.Files.Clear();
            UpdateCardCountDisplay(activeCard);
            TxtStatus.Text = $"Đã xóa danh sách tệp của '{activeCard.Title}'.";
        }

        private void BtnOpenLastOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_lastOutputDirectory) && Directory.Exists(_lastOutputDirectory))
            {
                Process.Start(new ProcessStartInfo { FileName = _lastOutputDirectory, UseShellExecute = true });
                return;
            }

            var firstFile = _cards.Values.SelectMany(c => c.Files).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstFile))
            {
                string dir = Path.GetDirectoryName(firstFile) ?? string.Empty;
                string outDir = Path.Combine(dir, "CMD_BOX_Output");
                string target = Directory.Exists(outDir) ? outDir : dir;
                if (Directory.Exists(target))
                {
                    Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
                    return;
                }
            }

            MessageBox.Show("Chưa có thư mục kết quả nào được tạo trong phiên làm việc này.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =========================================================================
        // 4. TRẠNG THÁI FFMPEG & HELPERS
        // =========================================================================

        private async void BtnRefreshFfmpeg_Click(object sender, RoutedEventArgs e) => await RefreshFfmpegStatusAsync(true);

        private async void BtnBrowseFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn tệp ffmpeg.exe",
                Filter = "FFmpeg Executable (ffmpeg.exe)|ffmpeg.exe|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true && File.Exists(dlg.FileName))
            {
                _mediaService.SetManualFfmpegPath(dlg.FileName);
                await RefreshFfmpegStatusAsync(true);
            }
        }

        private async Task RefreshFfmpegStatusAsync(bool force)
        {
            var status = await _mediaService.GetFFmpegStatusAsync(force);
            if (status.isAvailable)
            {
                TxtFfmpegStatus.Text = "FFmpeg: Sẵn sàng ✓";
                TxtFfmpegStatus.Foreground = TryFindResource("AccentSuccess") as Brush ?? Brushes.MediumSeaGreen;
                TxtFfmpegPathInfo.Text = $"Đang sử dụng: {status.path}";
            }
            else
            {
                TxtFfmpegStatus.Text = "FFmpeg: Chưa tìm thấy ✕";
                TxtFfmpegStatus.Foreground = TryFindResource("AccentDanger") as Brush ?? Brushes.IndianRed;
                TxtFfmpegPathInfo.Text = "Không tìm thấy ffmpeg.exe trong PATH hoặc thư mục ứng dụng. Nhấn nút bên dưới để chỉ định thủ công.";
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            BtnRunActiveCard.IsEnabled = enabled;
            BtnClearActiveCard.IsEnabled = enabled;
        }

        private static string BuildUniqueOutputPath(string outDir, string inputPath, string suffix, string targetExt)
        {
            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            string ext = targetExt.StartsWith('.') ? targetExt : "." + targetExt;
            string candidate = Path.Combine(outDir, $"{baseName}{suffix}{ext}");

            int idx = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(outDir, $"{baseName}{suffix}_{idx}{ext}");
                idx++;
            }
            return candidate;
        }

        private static double ParseDoubleOrDefault(string? raw, double fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            return double.TryParse(raw.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)
                ? val
                : fallback;
        }

        // =========================================================================
        // 5. BẢNG TÙY CHỈNH ĐỔI ĐỊNH DẠNG (MODAL OVERLAY)
        // =========================================================================

        private void BtnOpenFormatModal_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            SetActiveCard("convert_media");

            RbFormatAuto.IsChecked = _isAutoFormatMode;
            RbFormatCustom.IsChecked = !_isAutoFormatMode;
            SelectComboItemByTag(CmbFormatTargetImage, _customImageFormat);
            SelectComboItemByTag(CmbFormatTargetVideo, _customVideoFormat);
            CmbFormatQuality.SelectedIndex = Math.Clamp(_customFormatQuality, 0, 2);

            UpdateCustomFormatPanelState();
            PnlFormatModal.Visibility = Visibility.Visible;
        }

        private void BtnCloseFormatModal_Click(object sender, RoutedEventArgs e)
        {
            PnlFormatModal.Visibility = Visibility.Collapsed;
        }

        private void FormatModeRadio_Changed(object sender, RoutedEventArgs e)
        {
            UpdateCustomFormatPanelState();
        }

        private void UpdateCustomFormatPanelState()
        {
            if (PnlCustomFormatOptions == null) return;
            bool isCustom = RbFormatCustom?.IsChecked == true;
            PnlCustomFormatOptions.IsEnabled = isCustom;
            PnlCustomFormatOptions.Opacity = isCustom ? 1.0 : 0.6;
        }

        private void BtnResetFormatAuto_Click(object sender, RoutedEventArgs e)
        {
            RbFormatAuto.IsChecked = true;
            CmbFormatTargetImage.SelectedIndex = 0;
            CmbFormatTargetVideo.SelectedIndex = 0;
            CmbFormatQuality.SelectedIndex = 0;
            UpdateCustomFormatPanelState();
        }

        private void BtnApplyFormatModal_Click(object sender, RoutedEventArgs e)
        {
            _isAutoFormatMode = RbFormatAuto.IsChecked == true;
            _customImageFormat = (CmbFormatTargetImage.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
            _customVideoFormat = (CmbFormatTargetVideo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ".mp4";
            _customFormatQuality = Math.Max(0, CmbFormatQuality.SelectedIndex);

            UpdateConvertModeSummary();
            PnlFormatModal.Visibility = Visibility.Collapsed;
        }

        private void UpdateConvertModeSummary()
        {
            if (_isAutoFormatMode)
            {
                TxtConvertModeSummary.Text = "Tự động: Ảnh giữ chuẩn (HEIC ➔ JPG) • Video ➔ MP4";
                BtnOpenFormatModal.Content = "🎛️ Bảng Định Dạng";
            }
            else
            {
                string imgLabel = _customImageFormat == "auto" ? "Tự động" : _customImageFormat.ToUpperInvariant();
                string vidLabel = _customVideoFormat == "keep" ? "Giữ gốc" : _customVideoFormat.ToUpperInvariant();
                TxtConvertModeSummary.Text = $"Tùy chỉnh: Ảnh ➔ {imgLabel} • Video ➔ {vidLabel}";
                BtnOpenFormatModal.Content = $"🎛️ {imgLabel} / {vidLabel}";
            }
        }

        private static void SelectComboItemByTag(ComboBox combo, string tag)
        {
            foreach (var item in combo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
    }
}
