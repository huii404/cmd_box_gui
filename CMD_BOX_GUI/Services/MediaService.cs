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
            return ImageExtensions.Contains(Path.GetExtension(path));
        }

        public static bool IsVideoFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return VideoExtensions.Contains(Path.GetExtension(path));
        }

        public static string GetDefaultTargetExtension(string path)
        {
            if (IsImageFile(path)) return ".png";
            if (IsVideoFile(path)) return ".mp4";
            return Path.GetExtension(path);
        }

        public static List<string> GetAvailableExtensions(string path)
        {
            if (IsImageFile(path)) return new List<string>(DefaultImageExtensions);
            if (IsVideoFile(path)) return new List<string>(DefaultVideoExtensions);
            return new List<string> { Path.GetExtension(path).ToLowerInvariant() };
        }

        public void SetManualFfmpegPath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                _cachedFfmpegPath = Path.GetFullPath(path);
                _cachedFfmpegStatus = null;
            }
        }

        public string FindFFmpegPath(bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrWhiteSpace(_cachedFfmpegPath) && File.Exists(_cachedFfmpegPath))
            {
                return _cachedFfmpegPath;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchLocations = {
                Path.Combine(baseDir, "ffmpeg.exe"),
                Path.Combine(baseDir, "Cli_mediaEXE", "ffmpeg.exe"),
                Path.Combine(baseDir, "..", "Cli_mediaEXE", "ffmpeg.exe"),
                @"G:\Code\C#\code\WPF\Cli_mediaEXE\ffmpeg.exe"
            };

            foreach (var loc in searchLocations)
            {
                if (File.Exists(loc))
                {
                    _cachedFfmpegPath = Path.GetFullPath(loc);
                    return _cachedFfmpegPath;
                }
            }

            try
            {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    foreach (var p in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
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
            catch { }

            _cachedFfmpegPath = "ffmpeg.exe";
            return _cachedFfmpegPath;
        }

        public async Task<(bool isAvailable, string path, string versionInfo)> GetFFmpegStatusAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedFfmpegStatus.HasValue) return _cachedFfmpegStatus.Value;

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

            _cachedFfmpegStatus = (false, ffmpeg, "Chưa tìm thấy FFmpeg");
            return _cachedFfmpegStatus.Value;
        }

        public async Task<(double durationSec, int bitrateKbps)> ProbeMediaInfoAsync(string inputPath, string ffmpegPath)
        {
            try
            {
                string output = await ProcessRunner.RunCommandAndGetOutputAsync(ffmpegPath, $"-i \"{inputPath}\"");
                double duration = 0;
                int bitrate = 0;

                var durationMatch = System.Text.RegularExpressions.Regex.Match(output, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (durationMatch.Success)
                {
                    double hours = double.Parse(durationMatch.Groups[1].Value);
                    double minutes = double.Parse(durationMatch.Groups[2].Value);
                    double seconds = double.Parse(durationMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    duration = hours * 3600 + minutes * 60 + seconds;
                }

                var bitrateMatch = System.Text.RegularExpressions.Regex.Match(output, @"bitrate:\s*(\d+)\s*kb/s", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (bitrateMatch.Success)
                {
                    int.TryParse(bitrateMatch.Groups[1].Value, out bitrate);
                }

                if (bitrate <= 0 && duration > 0)
                {
                    long fileSize = new FileInfo(inputPath).Length;
                    bitrate = (int)((fileSize * 8.0) / (duration * 1000.0));
                }

                return (duration, bitrate);
            }
            catch
            {
                return (0, 0);
            }
        }

        public async Task<bool> CompressVideoAsync(string inputPath, string outputPath, int compressionLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            long oldSize = new FileInfo(inputPath).Length;
            var (durationSec, origBitrateKbps) = await ProbeMediaInfoAsync(inputPath, ffmpeg);

            // Mức CRF chuẩn điện ảnh: CRF 23 là mốc "Visually Lossless" (mắt thường không phân biệt được với bản gốc)
            int crf = compressionLevel switch
            {
                0 => 21, // Chất lượng cực cao
                2 => 26, // Nén khá
                3 => 29, // Nén sâu
                _ => 23  // Mặc định: Giữ độ nét tối đa, giảm ~35-45% dung lượng
            };

            double targetRatio = compressionLevel switch
            {
                0 => 0.80,
                2 => 0.50,
                3 => 0.38,
                _ => 0.65
            };

            int audioBitrate = compressionLevel == 0 ? 160 : (compressionLevel >= 2 ? 96 : 128);

            string args;
            if (origBitrateKbps > 0)
            {
                int targetTotalKbps = Math.Max(200, (int)(origBitrateKbps * targetRatio));
                int targetAudioKbps = Math.Min(audioBitrate, Math.Max(64, (int)(targetTotalKbps * 0.12)));
                int maxVideoBitrate = Math.Max(150, targetTotalKbps - targetAudioKbps);

                Logger.Info($"[FFmpeg] Nén Video Chuẩn HD [{Path.GetFileName(inputPath)}] (Gốc: {origBitrateKbps} kbps, MaxRate: {maxVideoBitrate} kbps, CRF {crf}, Tune: Film)...");
                // Dùng -preset slow + -tune film + -profile:v high để tối ưu thuật toán tìm vector chuyển động, giữ trọn chi tiết vi mô (da, tóc, vân vải) mà không dùng filter làm sai màu
                args = $"-y -i \"{inputPath}\" -c:v libx264 -profile:v high -preset slow -tune film -crf {crf} -maxrate {maxVideoBitrate}k -bufsize {maxVideoBitrate * 2}k -pix_fmt yuv420p -movflags +faststart -c:a aac -b:a {targetAudioKbps}k \"{outputPath}\"";
            }
            else
            {
                Logger.Info($"[FFmpeg] Nén Video Chuẩn HD [{Path.GetFileName(inputPath)}] (CRF {crf}, Tune: Film)...");
                args = $"-y -i \"{inputPath}\" -c:v libx264 -profile:v high -preset slow -tune film -crf {crf} -pix_fmt yuv420p -movflags +faststart -c:a aac -b:a {audioBitrate}k \"{outputPath}\"";
            }

            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long newSize = new FileInfo(outputPath).Length;

                // Tự động kiểm tra: Nếu sau khi nén mà file vẫn lớn hơn hoặc bằng file gốc -> Chạy Pass 2 an toàn
                if (newSize >= oldSize)
                {
                    Logger.Warning($"[FFmpeg] File sau khi encode ({SystemCore.FormatBytes(newSize)}) >= File gốc ({SystemCore.FormatBytes(oldSize)}). Tự động kích hoạt nén thích ứng (Safe Bitrate)...");
                    
                    double dur = durationSec > 0 ? durationSec : 10;
                    long targetSizeBytes = (long)(oldSize * 0.65); // Ép mục tiêu dung lượng ~65%
                    int strictTotalKbps = Math.Max(160, (int)((targetSizeBytes * 8.0) / (dur * 1000.0)));
                    int strictAudioKbps = Math.Min(96, Math.Max(48, (int)(strictTotalKbps * 0.12)));
                    int strictVideoKbps = Math.Max(120, strictTotalKbps - strictAudioKbps);

                    string pass2Args = $"-y -i \"{inputPath}\" -c:v libx264 -profile:v high -b:v {strictVideoKbps}k -maxrate {strictVideoKbps}k -bufsize {strictVideoKbps * 2}k -preset slow -tune film -pix_fmt yuv420p -movflags +faststart -c:a aac -b:a {strictAudioKbps}k \"{outputPath}\"";
                    int code2 = await ProcessRunner.RunProcessAsync(ffmpeg, pass2Args, cancellationToken: cancellationToken);

                    if (code2 == 0 && File.Exists(outputPath))
                    {
                        newSize = new FileInfo(outputPath).Length;
                    }
                }

                Logger.Success($"[FFmpeg] Nén xong Video [{Path.GetFileName(inputPath)}]: {SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)}");
                return true;
            }

            Logger.Error($"[FFmpeg] Nén Video thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        public async Task<bool> CompressImageAsync(string inputPath, string outputPath, int compressionLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant();
            long oldSize = new FileInfo(inputPath).Length;

            Logger.Info($"[FFmpeg] Nén Ảnh [{Path.GetFileName(inputPath)}]...");

            string args = (outExt, ext) switch
            {
                (".jpg" or ".jpeg", _) or (_, ".jpg" or ".jpeg") => $"-y -i \"{inputPath}\" -q:v {(compressionLevel == 0 ? 4 : (compressionLevel >= 2 ? 8 : 6))} -pix_fmt yuvj420p \"{outputPath}\"",
                (".webp", _) or (_, ".webp") => $"-y -i \"{inputPath}\" -c:v libwebp -quality {(compressionLevel == 0 ? 78 : (compressionLevel >= 2 ? 50 : 65))} -preset default \"{outputPath}\"",
                (".png", _) or (_, ".png") => $"-y -i \"{inputPath}\" -c:v png -compression_level {(compressionLevel == 0 ? 7 : 9)} \"{outputPath}\"",
                _ => $"-y -i \"{inputPath}\" -q:v 6 \"{outputPath}\""
            };

            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long newSize = new FileInfo(outputPath).Length;

                // Nếu ảnh xuất ra bị phình to hơn ảnh gốc
                if (newSize >= oldSize)
                {
                    if (outExt == ".jpg" || outExt == ".jpeg")
                    {
                        string fallbackArgs = $"-y -i \"{inputPath}\" -q:v 10 -pix_fmt yuvj420p \"{outputPath}\"";
                        await ProcessRunner.RunProcessAsync(ffmpeg, fallbackArgs, cancellationToken: cancellationToken);
                    }
                    else if (outExt == ".webp")
                    {
                        string fallbackArgs = $"-y -i \"{inputPath}\" -c:v libwebp -quality 48 -preset default \"{outputPath}\"";
                        await ProcessRunner.RunProcessAsync(ffmpeg, fallbackArgs, cancellationToken: cancellationToken);
                    }

                    if (File.Exists(outputPath))
                    {
                        newSize = new FileInfo(outputPath).Length;
                    }
                }

                Logger.Success($"[FFmpeg] Nén xong Ảnh [{Path.GetFileName(inputPath)}]: {SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)}");
                return true;
            }

            Logger.Error($"[FFmpeg] Nén Ảnh thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        public async Task<bool> CompressMediaAsync(string inputPath, string outputPath, int compressionLevel = 1, CancellationToken cancellationToken = default)
        {
            return IsImageFile(inputPath)
                ? await CompressImageAsync(inputPath, outputPath, compressionLevel, cancellationToken)
                : await CompressVideoAsync(inputPath, outputPath, compressionLevel, cancellationToken);
        }

        public async Task<bool> ConvertMediaFormatAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant();

            Logger.Info($"[FFmpeg] Chuyển định dạng [{Path.GetFileName(inputPath)}] ➔ [{outExt}]...");

            string args = (IsVideoFile(inputPath) || IsVideoFile(outputPath))
                ? $"-y -i \"{inputPath}\" -c:v libx264 -preset faster -crf 23 -c:a aac -b:a 128k \"{outputPath}\""
                : $"-y -i \"{inputPath}\" \"{outputPath}\"";

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

        public async Task<bool> EnhanceVideoAsync(string inputPath, string outputPath, int enhanceLevel = 1, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string filter = enhanceLevel switch
            {
                0 => "hqdn3d=2.0:1.5:3.0:2.5,unsharp=lx=3:ly=3:la=0.5:cx=3:cy=3:ca=0.0,eq=contrast=1.02:saturation=1.02",
                2 => "hqdn3d=4.0:3.0:5.0:4.0,unsharp=lx=5:ly=5:la=1.2:cx=5:cy=5:ca=0.0,eq=contrast=1.05:saturation=1.04",
                3 => "hqdn3d=5.0:3.5:6.0:4.5,unsharp=lx=7:ly=7:la=1.5:cx=5:cy=5:ca=0.0,eq=contrast=1.07:saturation=1.05",
                _ => "hqdn3d=3.0:2.0:4.0:3.0,unsharp=lx=5:ly=5:la=0.8:cx=3:cy=3:ca=0.0,eq=contrast=1.03:saturation=1.03"
            };

            Logger.Info($"[FFmpeg] Làm nét Video [{Path.GetFileName(inputPath)}] (Mức {enhanceLevel + 1})...");
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

        public async Task<bool> EnhanceImageAsync(string inputPath, string outputPath, int enhanceLevel = 1, CancellationToken cancellationToken = default)
        {
            Logger.Info($"[Native C#] Làm nét ảnh [{Path.GetFileName(inputPath)}] (Mức {enhanceLevel + 1})...");
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant();

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

        public async Task<bool> EnhanceMediaAsync(string inputPath, string outputPath, int enhanceLevel = 1, CancellationToken cancellationToken = default)
        {
            return IsImageFile(inputPath)
                ? await EnhanceImageAsync(inputPath, outputPath, enhanceLevel, cancellationToken)
                : await EnhanceVideoAsync(inputPath, outputPath, enhanceLevel, cancellationToken);
        }

        public async Task<bool> ExtractAudioAsync(string inputPath, string outputPath, string format = "mp3", CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath)) return false;

            string ffmpeg = FindFFmpegPath();
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant().TrimStart('.');
            if (string.IsNullOrWhiteSpace(outExt)) outExt = format.ToLowerInvariant().TrimStart('.');

            string codecArgs = outExt switch
            {
                "wav" => "-c:a pcm_s16le",
                "flac" => "-c:a flac",
                "aac" or "m4a" => "-c:a aac -b:a 256k",
                _ => "-c:a libmp3lame -b:a 320k"
            };

            Logger.Info($"[FFmpeg] Trích xuất Audio từ [{Path.GetFileName(inputPath)}] ➔ [.{outExt}]...");
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

        public async Task<bool> MuteVideoAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath)) return false;

            string ffmpeg = FindFFmpegPath();
            Logger.Info($"[FFmpeg] Tắt tiếng Video [{Path.GetFileName(inputPath)}] (0.1s Stream copy)...");

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
            if (startTimeSec > 0) trimArgs += $"-ss {startTimeSec} ";
            if (durationSec > 0) trimArgs += $"-t {durationSec} ";

            string scaleFilter = scaleWidth > 0 ? $",scale={scaleWidth}:-1:flags=lanczos" : "";
            string filter = $"fps={fps}{scaleFilter},split[s0][s1];[s0]palettegen=max_colors=256:reserve_transparent=0[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3";

            string durationLog = durationSec > 0 ? $"{durationSec}s" : "toàn bộ";
            Logger.Info($"[FFmpeg] Tạo GIF [{Path.GetFileName(inputPath)}] ({durationLog}, {scaleWidth}p, {fps} FPS)...");

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
