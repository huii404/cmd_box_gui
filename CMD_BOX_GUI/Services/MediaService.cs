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
                Path.Combine("bin", "ffmpeg.exe"),
                Path.Combine("ffmpeg", "ffmpeg.exe"),
                Path.Combine("ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine("tools", "ffmpeg.exe"),
                Path.Combine("tools", "bin", "ffmpeg.exe"),
                Path.Combine("Cli_mediaEXE", "ffmpeg.exe"),
                Path.Combine("Cli_mediaEXE", "bin", "ffmpeg.exe"),
                Path.Combine("cmd_box_gui", "ffmpeg.exe"),
                Path.Combine("cmd_box_gui", "bin", "ffmpeg.exe"),
                Path.Combine("CMD_BOX_GUI", "ffmpeg.exe"),
                Path.Combine("CMD_BOX_GUI", "bin", "ffmpeg.exe"),
                Path.Combine("CMD_BOX_GUI", "CMD_BOX_GUI", "ffmpeg.exe"),
                Path.Combine("CMD_BOX_GUI", "CMD_BOX_GUI", "bin", "ffmpeg.exe"),
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
        /// Nén Video (H.264 CRF cố định ~30% giảm dung lượng, giữ nét cao)
        /// </summary>
        public async Task<bool> CompressVideoAsync(string inputPath, string outputPath, int compressionLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            int crf = compressionLevel switch
            {
                0 => 22,
                2 => 30,
                3 => 34,
                _ => 26  // Tiêu chuẩn ~30% nén
            };

            int audioBitrate = compressionLevel == 0 ? 160 : (compressionLevel >= 2 ? 96 : 128);

            Logger.Info($"[FFmpeg] Đang nén Video [{Path.GetFileName(inputPath)}] (CRF {crf})...");
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
        /// Nén Ảnh (Tự tối ưu theo JPG, PNG, WEBP... cố định ~30% nén)
        /// </summary>
        public async Task<bool> CompressImageAsync(string inputPath, string outputPath, int compressionLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant();

            Logger.Info($"[FFmpeg] Đang nén Ảnh [{Path.GetFileName(inputPath)}]...");

            string args;
            if (outExt == ".jpg" || outExt == ".jpeg" || ext == ".jpg" || ext == ".jpeg")
            {
                int qscale = compressionLevel == 0 ? 2 : (compressionLevel >= 2 ? 8 : 4);
                args = $"-y -i \"{inputPath}\" -q:v {qscale} \"{outputPath}\"";
            }
            else if (outExt == ".webp" || ext == ".webp")
            {
                int quality = compressionLevel == 0 ? 85 : (compressionLevel >= 2 ? 65 : 78);
                args = $"-y -i \"{inputPath}\" -c:v libwebp -quality {quality} \"{outputPath}\"";
            }
            else if (outExt == ".png" || ext == ".png")
            {
                int compLevel = compressionLevel == 0 ? 6 : 9;
                args = $"-y -i \"{inputPath}\" -c:v png -compression_level {compLevel} \"{outputPath}\"";
            }
            else
            {
                args = $"-y -i \"{inputPath}\" -q:v 4 \"{outputPath}\"";
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
        /// Làm nét & khử nhiễu Video theo cấp độ (HQ Denoise + Unsharp + Tương phản nổi khối)
        /// </summary>
        public async Task<bool> EnhanceVideoAsync(string inputPath, string outputPath, int enhanceLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string filter = enhanceLevel switch
            {
                0 => "hqdn3d=1.5:1.0:2.0:1.5,unsharp=3:3:0.6:3:3:0.0,eq=contrast=1.02:saturation=1.02",
                2 => "hqdn3d=3.0:2.0:4.0:3.0,unsharp=5:5:1.5:5:5:0.0,eq=contrast=1.06:saturation=1.05",
                3 => "hqdn3d=4.0:3.0:5.0:4.0,unsharp=7:7:2.0:7:7:0.0,eq=contrast=1.10:saturation=1.08",
                _ => "hqdn3d=2.0:1.5:3.0:2.0,unsharp=5:5:1.0:5:5:0.0,eq=contrast=1.04:saturation=1.03" // Mặc định (Mức 2)
            };

            Logger.Info($"[FFmpeg] Đang làm nét & khử nhiễu Video [{Path.GetFileName(inputPath)}] (Mức {enhanceLevel + 1})...");
            string args = $"-y -i \"{inputPath}\" -vf \"{filter}\" -c:v libx264 -crf 19 -preset faster -c:a copy \"{outputPath}\"";
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
        /// - Xử lý trực tiếp trên RAM bằng con trỏ bộ nhớ (Unsafe pointers) và đa luồng Parallel.For trên 16 luồng CPU
        /// - Áp dụng Unsharp Masking (USM) vi điểm, Tương phản nổi khối 3D (S-Curve) và Tăng rực rỡ thông minh (Vibrance).
        /// - Tốc độ tức thì (~0.05s - 0.2s), không phụ thuộc CLI Vulkan ngoài.
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
    }
}

