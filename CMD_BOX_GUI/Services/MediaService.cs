using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CMD_BOX_GUI.Core;

namespace CMD_BOX_GUI.Services
{
    public class MediaService
    {
        public static readonly string[] DefaultImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".ico", ".gif" };
        public static readonly string[] DefaultVideoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".wmv", ".gif" };

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff", ".tif", ".ico", ".jfif", ".gif", ".heic", ".avif"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".m4v", ".ts", ".3gp"
        };

        private string? _cachedFfmpegPath;
        private (bool isAvailable, string path, string versionInfo)? _cachedFfmpegStatus;

        public static bool IsImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string ext = Path.GetExtension(path);
            return ImageExtensions.Contains(ext);
        }

        public static bool IsVideoFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string ext = Path.GetExtension(path);
            return VideoExtensions.Contains(ext);
        }

        public static string GetDefaultTargetExtension(string path)
        {
            if (IsImageFile(path)) return ".png";
            if (IsVideoFile(path)) return ".mp4";
            return Path.GetExtension(path);
        }

        public static List<string> GetAvailableExtensions(string path)
        {
            if (IsImageFile(path))
            {
                return new List<string>(DefaultImageExtensions);
            }
            if (IsVideoFile(path))
            {
                return new List<string>(DefaultVideoExtensions);
            }
            return new List<string> { Path.GetExtension(path).ToLowerInvariant() };
        }

        public void SetManualFfmpegPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                _cachedFfmpegPath = Path.GetFullPath(path);
                _cachedFfmpegStatus = null; // Reset cache để kiểm tra lại
            }
        }

        /// <summary>
        /// Dò tìm đường dẫn ffmpeg.exe siêu tốc (Ưu tiên Cli_mediaEXE, thư mục app, cache, PATH).
        /// </summary>
        public string FindFFmpegPath(bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrWhiteSpace(_cachedFfmpegPath) && File.Exists(_cachedFfmpegPath))
            {
                return _cachedFfmpegPath;
            }

            // 0. Ưu tiên cao nhất: Thư mục chứa Cli_mediaEXE và các vị trí thông dụng
            string[] directPaths = {
                @"G:\Code\C#\code\WPF\Cli_mediaEXE\ffmpeg.exe",
                @"G:\Code\C#\code\WPF\Cli_mediaEXE\bin\ffmpeg.exe",
                @"G:\Code\C#\code\WPF\ffmpeg.exe",
                @"G:\Code\C#\code\WPF\CMD_BOX_GUI\ffmpeg.exe",
                @"G:\Code\C#\code\WPF\CMD_BOX_GUI\bin\ffmpeg.exe"
            };
            foreach (var cp in directPaths)
            {
                if (File.Exists(cp))
                {
                    _cachedFfmpegPath = Path.GetFullPath(cp);
                    return _cachedFfmpegPath;
                }
            }

            // 1. Quét nhanh thư mục ứng dụng hiện tại
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new List<string>();
            AddCandidatesFromFolder(candidates, baseDir);
            foreach (var cand in candidates)
            {
                if (File.Exists(cand))
                {
                    _cachedFfmpegPath = Path.GetFullPath(cand);
                    return _cachedFfmpegPath;
                }
            }

            // 2. Kiểm tra biến môi trường PATH
            try
            {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    foreach (var p in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Directory.Exists(p))
                        {
                            string target = Path.Combine(p.Trim(), "ffmpeg.exe");
                            if (File.Exists(target))
                            {
                                _cachedFfmpegPath = Path.GetFullPath(target);
                                return _cachedFfmpegPath;
                            }
                        }
                    }
                }
            }
            catch { }

            // Fallback mặc định
            _cachedFfmpegPath = "ffmpeg.exe";
            return _cachedFfmpegPath;
        }

        private static void AddCandidatesFromFolder(List<string> list, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;

            string[] subPaths = {
                "ffmpeg.exe",
                Path.Combine("Cli_mediaEXE", "ffmpeg.exe"),
            };

            foreach (var sp in subPaths)
            {
                string full = Path.Combine(folder, sp);
                if (File.Exists(full))
                {
                    list.Add(full);
                }
            }
        }

        /// <summary>
        /// Kiểm tra trạng thái hoạt động của FFmpeg (Có cache kết quả để không lặp lại tiến trình gây lag)
        /// </summary>
        public async Task<(bool isAvailable, string path, string versionInfo)> GetFFmpegStatusAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedFfmpegStatus.HasValue)
            {
                return _cachedFfmpegStatus.Value;
            }

            string ffmpeg = FindFFmpegPath(forceRefresh);
            if (File.Exists(ffmpeg))
            {
                try
                {
                    string output = await ProcessRunner.RunCommandAndGetOutputAsync(ffmpeg, "-version");
                    if (!string.IsNullOrWhiteSpace(output) && output.Contains("ffmpeg version", StringComparison.OrdinalIgnoreCase))
                    {
                        string firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "FFmpeg Ready";
                        _cachedFfmpegStatus = (true, ffmpeg, firstLine);
                        return _cachedFfmpegStatus.Value;
                    }
                }
                catch { }
            }

            _cachedFfmpegStatus = (false, ffmpeg, "Chưa tìm thấy hoặc FFmpeg không chạy được");
            return _cachedFfmpegStatus.Value;
        }

        /// <summary>
        /// Nén Video (H.264 CRF tối ưu ~40% giảm dung lượng, giữ nét cao)
        /// </summary>
        public async Task<bool> CompressVideoAsync(string inputPath, string outputPath, int compressionLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            int crf = compressionLevel switch
            {
                0 => 24,
                2 => 32,
                3 => 36,
                _ => 28  // Tiêu chuẩn ~40% nén
            };

            int audioBitrate = compressionLevel == 0 ? 128 : (compressionLevel >= 2 ? 80 : 96);

            Logger.Info($"[FFmpeg] Đang nén Video [{Path.GetFileName(inputPath)}] (CRF {crf}, ~40% nén)...");
            string args = $"-y -i \"{inputPath}\" -vcodec libx264 -crf {crf} -preset faster -c:a aac -b:a {audioBitrate}k \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Nén xong Video [{Path.GetFileName(inputPath)}]: {SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)}");
                return true;
            }

            Logger.Error($"[FFmpeg] Nén Video thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        /// <summary>
        /// Nén Ảnh (Tự tối ưu theo JPG, PNG, WEBP... tối ưu ~40% nén)
        /// </summary>
        public async Task<bool> CompressImageAsync(string inputPath, string outputPath, int compressionLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant();

            Logger.Info($"[FFmpeg] Đang nén Ảnh [{Path.GetFileName(inputPath)}] (~40% nén)...");

            string args;
            if (outExt == ".jpg" || outExt == ".jpeg" || ext == ".jpg" || ext == ".jpeg")
            {
                int qscale = compressionLevel == 0 ? 3 : (compressionLevel >= 2 ? 8 : 5);
                args = $"-y -i \"{inputPath}\" -q:v {qscale} \"{outputPath}\"";
            }
            else if (outExt == ".webp" || ext == ".webp")
            {
                int quality = compressionLevel == 0 ? 80 : (compressionLevel >= 2 ? 55 : 68);
                args = $"-y -i \"{inputPath}\" -c:v libwebp -quality {quality} \"{outputPath}\"";
            }
            else if (outExt == ".png" || ext == ".png")
            {
                int compLevel = compressionLevel == 0 ? 7 : 9;
                args = $"-y -i \"{inputPath}\" -c:v png -compression_level {compLevel} \"{outputPath}\"";
            }
            else
            {
                args = $"-y -i \"{inputPath}\" -q:v 5 \"{outputPath}\"";
            }

            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Nén xong Ảnh [{Path.GetFileName(inputPath)}]: {SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)}");
                return true;
            }

            Logger.Error($"[FFmpeg] Nén Ảnh thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        /// <summary>
        /// Tự động nén Media (Tự nhận diện Ảnh hoặc Video)
        /// </summary>
        public async Task<bool> CompressMediaAsync(string inputPath, string outputPath, int compressionLevel = 1, CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath))
            {
                return await CompressImageAsync(inputPath, outputPath, compressionLevel, cancellationToken);
            }
            return await CompressVideoAsync(inputPath, outputPath, compressionLevel, cancellationToken);
        }

        /// <summary>
        /// Đổi đuôi tệp (Image / Video Format Converter)
        /// </summary>
        public async Task<bool> ConvertMediaFormatAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant();

            Logger.Info($"[FFmpeg] Chuyển đổi định dạng [{Path.GetFileName(inputPath)}] ➔ [{outExt}]...");

            string args;
            if (IsVideoFile(inputPath) || IsVideoFile(outputPath))
            {
                args = $"-y -i \"{inputPath}\" -c:v libx264 -preset faster -crf 23 -c:a aac -b:a 128k \"{outputPath}\"";
            }
            else
            {
                args = $"-y -i \"{inputPath}\" \"{outputPath}\"";
            }

            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Chuyển đổi thành công: {outputPath} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                return true;
            }

            Logger.Error($"[FFmpeg] Chuyển đổi định dạng thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        /// <summary>
        /// Làm nét & khử nhiễu Video đa yếu tố:
        /// - Tiền xử lý khử hạt vi mô mượt mà (hqdn3d).
        /// - Làm nét thích ứng vi điểm kênh độ sáng Luma (Unsharp lx/ly không gây nhiễu màu Chroma ca=0).
        /// - Cân bằng tương phản S-Curve và màu sắc mượt mà không vỡ hạt (eq).
        /// </summary>
        public async Task<bool> EnhanceVideoAsync(string inputPath, string outputPath, int enhanceLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string filter = enhanceLevel switch
            {
                0 => "hqdn3d=2.0:1.5:3.0:2.5,unsharp=lx=3:ly=3:la=0.5:cx=3:cy=3:ca=0.0,eq=contrast=1.02:saturation=1.02", // Nhẹ
                2 => "hqdn3d=4.0:3.0:5.0:4.0,unsharp=lx=5:ly=5:la=1.2:cx=5:cy=5:ca=0.0,eq=contrast=1.05:saturation=1.04", // Cao
                3 => "hqdn3d=5.0:3.5:6.0:4.5,unsharp=lx=7:ly=7:la=1.5:cx=5:cy=5:ca=0.0,eq=contrast=1.07:saturation=1.05", // Siêu nét
                _ => "hqdn3d=3.0:2.0:4.0:3.0,unsharp=lx=5:ly=5:la=0.8:cx=3:cy=3:ca=0.0,eq=contrast=1.03:saturation=1.03"  // Tiêu chuẩn (Mức 2)
            };

            Logger.Info($"[FFmpeg] Đang làm nét & khử hạt Video [{Path.GetFileName(inputPath)}] (Mức {enhanceLevel + 1})...");
            string args = $"-y -i \"{inputPath}\" -vf \"{filter}\" -c:v libx264 -crf 20 -preset faster -c:a copy \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Làm nét Video thành công: {outputPath} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                return true;
            }

            Logger.Error($"[FFmpeg] Làm nét Video thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        /// <summary>
        /// Làm nét & tối ưu ảnh siêu tốc độ cao bằng thuật toán thuần C# (.NET Native):
        /// - Xử lý trực tiếp trên RAM bằng đa luồng Parallel.For trên CPU.
        /// - Tích hợp Khử gai (Anti-Grain), Soft-Coring USM, Chống quầng viền (Halo Suppression) và S-Curve nổi khối.
        /// - Tốc độ tức thì (~0.05s - 0.2s), không phụ thuộc công cụ ngoài.
        /// </summary>
        public async Task<bool> EnhanceImageAsync(string inputPath, string outputPath, int enhanceLevel = 1, CancellationToken cancellationToken = default)
        {
            Logger.Info($"[Native C#] Đang làm nét ảnh [{Path.GetFileName(inputPath)}] (Mức {enhanceLevel + 1})...");
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant();

            // Nếu người dùng chọn định dạng .webp, xuất ra PNG rồi chuyển sang WebP bằng FFmpeg
            if (outExt == ".webp")
            {
                string tempPng = Path.Combine(Path.GetTempPath(), "CmdBox_Enhance_" + Guid.NewGuid().ToString("N") + ".png");
                try
                {
                    bool ok = await NativeImageEnhancer.EnhanceImageAsync(inputPath, tempPng, enhanceLevel);
                    if (ok && File.Exists(tempPng))
                    {
                        string ffmpeg = FindFFmpegPath();
                        string args = $"-y -threads 0 -i \"{tempPng}\" -c:v libwebp -quality 95 \"{outputPath}\"";
                        int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);
                        if (code == 0 && File.Exists(outputPath))
                        {
                            long oldSize = new FileInfo(inputPath).Length;
                            long newSize = new FileInfo(outputPath).Length;
                            Logger.Success($"[Native C#] Làm nét ảnh thành công: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                            return true;
                        }
                    }
                }
                finally
                {
                    try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch { }
                }
            }
            else
            {
                bool ok = await NativeImageEnhancer.EnhanceImageAsync(inputPath, outputPath, enhanceLevel);
                if (ok && File.Exists(outputPath))
                {
                    long oldSize = new FileInfo(inputPath).Length;
                    long newSize = new FileInfo(outputPath).Length;
                    Logger.Success($"[Native C#] Làm nét ảnh thành công: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                    return true;
                }
            }

            Logger.Error($"[Native C#] Làm nét ảnh thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        /// <summary>
        /// Tự động làm nét Media (Nhận diện Ảnh hoặc Video)
        /// </summary>
        public async Task<bool> EnhanceMediaAsync(string inputPath, string outputPath, int enhanceLevel = 1, CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath))
            {
                return await EnhanceImageAsync(inputPath, outputPath, enhanceLevel, cancellationToken);
            }
            return await EnhanceVideoAsync(inputPath, outputPath, enhanceLevel, cancellationToken);
        }

        /// <summary>
        /// 1. Trích xuất âm thanh từ Video sang file Audio chất lượng cao (.mp3, .aac, .wav, .flac, .m4a)
        /// </summary>
        public async Task<bool> ExtractAudioAsync(string inputPath, string outputPath, string format = "mp3", CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath))
            {
                Logger.Warning($"[FFmpeg] Tệp ảnh [{Path.GetFileName(inputPath)}] không có âm thanh để trích xuất!");
                return false;
            }

            string ffmpeg = FindFFmpegPath();
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant().TrimStart('.');
            if (string.IsNullOrWhiteSpace(outExt)) outExt = format.ToLowerInvariant().TrimStart('.');

            string codecArgs = outExt switch
            {
                "wav" => "-c:a pcm_s16le",
                "flac" => "-c:a flac",
                "aac" or "m4a" => "-c:a aac -b:a 256k",
                _ => "-c:a libmp3lame -b:a 320k" // Mặc định MP3 320kbps
            };

            Logger.Info($"[FFmpeg] Đang trích xuất Audio từ [{Path.GetFileName(inputPath)}] ➔ [.{outExt}]...");
            string args = $"-y -i \"{inputPath}\" -vn {codecArgs} \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Trích xuất Audio thành công: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(newSize)})");
                return true;
            }

            Logger.Error($"[FFmpeg] Trích xuất Audio thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        /// <summary>
        /// 2. Tắt tiếng Video siêu tốc trong 0.1s (Chế độ Stream Copy - Không cần encode lại)
        /// </summary>
        public async Task<bool> MuteVideoAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath))
            {
                Logger.Warning($"[FFmpeg] Tệp ảnh [{Path.GetFileName(inputPath)}] không phải là video!");
                return false;
            }

            string ffmpeg = FindFFmpegPath();
            Logger.Info($"[FFmpeg] Đang tắt tiếng Video [{Path.GetFileName(inputPath)}] (Stream copy 0.1s)...");

            string args = $"-y -i \"{inputPath}\" -c:v copy -an \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Tắt tiếng Video thành công: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                return true;
            }

            Logger.Error($"[FFmpeg] Tắt tiếng Video thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        /// <summary>
        /// 3. Tạo ảnh động GIF chất lượng cao & nhẹ từ Video (Cắt đoạn ngắn, Scale kích thước & Two-pass Palettegen)
        /// </summary>
        public async Task<bool> ConvertToGifAsync(
            string inputPath, 
            string outputPath, 
            double startTimeSec = 0, 
            double durationSec = 5, 
            int scaleWidth = 480, 
            int fps = 12, 
            CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string trimArgs = "";
            if (startTimeSec > 0)
            {
                trimArgs += $"-ss {startTimeSec} ";
            }
            if (durationSec > 0)
            {
                trimArgs += $"-t {durationSec} ";
            }

            string scaleFilter = scaleWidth > 0 ? $",scale={scaleWidth}:-1:flags=lanczos" : "";
            string filter = $"fps={fps}{scaleFilter},split[s0][s1];[s0]palettegen=max_colors=256:reserve_transparent=0[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3";

            string durationLog = durationSec > 0 ? $"{durationSec}s" : "toàn bộ";
            Logger.Info($"[FFmpeg] Đang tạo GIF [{Path.GetFileName(inputPath)}] (Cắt {durationLog} từ {startTimeSec}s, {scaleWidth}p, {fps} FPS)...");

            string args = $"-y {trimArgs}-i \"{inputPath}\" -vf \"{filter}\" \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Tạo ảnh GIF thành công: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                return true;
            }

            Logger.Error($"[FFmpeg] Tạo ảnh GIF thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }
    }
}
