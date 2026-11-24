using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CMD_BOX_GUI.Core;

namespace CMD_BOX_GUI.Services
{
    public class MediaService
    {
        public static readonly string[] DefaultImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".ico", ".gif" };
        public static readonly string[] DefaultVideoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".flv", ".wmv", ".gif" };

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff", ".tif", ".ico", ".jfif", ".gif", ".heic", ".heif", ".avif"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".m4v", ".ts", ".3gp"
        };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma", ".opus"
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

        public static bool IsAudioFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return AudioExtensions.Contains(Path.GetExtension(path));
        }

        public static string GetDefaultTargetExtension(string path)
        {
            if (IsImageFile(path))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext) || ext is ".heic" or ".heif" or ".avif" or ".jfif")
                    return ".jpg";
                return ext;
            }
            if (IsVideoFile(path)) return ".mp4";
            return Path.GetExtension(path);
        }

        public static string ResolveTargetExtension(string inputPath, bool isAutoMode, string? customImageExt, string? customVideoExt)
        {
            if (isAutoMode)
                return GetDefaultTargetExtension(inputPath);

            if (IsImageFile(inputPath))
            {
                if (string.IsNullOrWhiteSpace(customImageExt) || customImageExt.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    return GetDefaultTargetExtension(inputPath);
                return customImageExt.StartsWith('.') ? customImageExt : "." + customImageExt;
            }

            if (IsVideoFile(inputPath))
            {
                if (string.IsNullOrWhiteSpace(customVideoExt) || customVideoExt.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    return ".mp4";
                if (customVideoExt.Equals("keep", StringComparison.OrdinalIgnoreCase))
                    return Path.GetExtension(inputPath).ToLowerInvariant();
                return customVideoExt.StartsWith('.') ? customVideoExt : "." + customVideoExt;
            }

            return Path.GetExtension(inputPath);
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
                // Dùng -preset slow + -tune film + -profile:v high + -map_metadata 0 để tối ưu nén mà giữ trọn chi tiết và metadata gốc
                args = $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -profile:v high -preset slow -tune film -crf {crf} -maxrate {maxVideoBitrate}k -bufsize {maxVideoBitrate * 2}k -pix_fmt yuv420p -movflags +faststart+use_metadata_tags -c:a aac -b:a {targetAudioKbps}k \"{outputPath}\"";
            }
            else
            {
                Logger.Info($"[FFmpeg] Nén Video Chuẩn HD [{Path.GetFileName(inputPath)}] (CRF {crf}, Tune: Film)...");
                args = $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -profile:v high -preset slow -tune film -crf {crf} -pix_fmt yuv420p -movflags +faststart+use_metadata_tags -c:a aac -b:a {audioBitrate}k \"{outputPath}\"";
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

                    string pass2Args = $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -profile:v high -b:v {strictVideoKbps}k -maxrate {strictVideoKbps}k -bufsize {strictVideoKbps * 2}k -preset slow -tune film -pix_fmt yuv420p -movflags +faststart+use_metadata_tags -c:a aac -b:a {strictAudioKbps}k \"{outputPath}\"";
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
                (".jpg" or ".jpeg", _) or (_, ".jpg" or ".jpeg") => $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -q:v {(compressionLevel == 0 ? 4 : (compressionLevel >= 2 ? 8 : 6))} -pix_fmt yuvj420p -color_range 2 \"{outputPath}\"",
                (".webp", _) or (_, ".webp") => $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -c:v libwebp -quality {(compressionLevel == 0 ? 78 : (compressionLevel >= 2 ? 50 : 65))} -preset default \"{outputPath}\"",
                (".png", _) or (_, ".png") => $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -c:v png -compression_level {(compressionLevel == 0 ? 7 : 9)} \"{outputPath}\"",
                _ => $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -q:v 6 \"{outputPath}\""
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
                        string fallbackArgs = $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -q:v 10 -pix_fmt yuvj420p -color_range 2 \"{outputPath}\"";
                        await ProcessRunner.RunProcessAsync(ffmpeg, fallbackArgs, cancellationToken: cancellationToken);
                    }
                    else if (outExt == ".webp")
                    {
                        string fallbackArgs = $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -c:v libwebp -quality 48 -preset default \"{outputPath}\"";
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

        public async Task<bool> ConvertMediaFormatAsync(string inputPath, string outputPath, int qualityPreset = 0, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string outExt = Path.GetExtension(outputPath).ToLowerInvariant();

            Logger.Info($"[FFmpeg] Chuyển định dạng [{Path.GetFileName(inputPath)}] ➔ [{outExt}] (Bảo toàn metadata)...");

            string args = BuildSafeConvertArgs(inputPath, outputPath, outExt, qualityPreset);

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

        private static string BuildSafeConvertArgs(string inputPath, string outputPath, string outExt, int qualityPreset)
        {
            if (IsImageFile(outputPath) && !IsVideoFile(inputPath))
            {
                int jpgQ = qualityPreset == 0 ? 2 : (qualityPreset >= 2 ? 7 : 4);
                int webpQ = qualityPreset == 0 ? 95 : (qualityPreset >= 2 ? 72 : 85);
                int pngLevel = qualityPreset == 0 ? 6 : 9;

                return outExt switch
                {
                    ".jpg" or ".jpeg" or ".jfif" =>
                        $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -pix_fmt yuvj420p -color_range 2 -q:v {jpgQ} \"{outputPath}\"",
                    ".png" =>
                        $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -c:v png -compression_level {pngLevel} \"{outputPath}\"",
                    ".webp" =>
                        $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -c:v libwebp -quality {webpQ} -preset default \"{outputPath}\"",
                    ".bmp" =>
                        $"-y -i \"{inputPath}\" -frames:v 1 -update 1 -pix_fmt bgr24 -c:v bmp \"{outputPath}\"",
                    ".ico" =>
                        $"-y -i \"{inputPath}\" -frames:v 1 -update 1 -vf \"scale='min(256,iw)':'min(256,ih)':force_original_aspect_ratio=decrease\" -pix_fmt rgba \"{outputPath}\"",
                    ".tiff" or ".tif" =>
                        $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -c:v tiff \"{outputPath}\"",
                    ".gif" =>
                        $"-y -i \"{inputPath}\" -vf \"split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" \"{outputPath}\"",
                    _ =>
                        $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 \"{outputPath}\""
                };
            }

            // Video hoặc chuyển từ Video/Ảnh sang định dạng Video (ép chẵn kích thước + pix_fmt yuv420p để không sập codec/mã màu)
            int crf = qualityPreset == 0 ? 18 : (qualityPreset >= 2 ? 26 : 22);
            return outExt switch
            {
                ".mp4" or ".m4v" =>
                    $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -profile:v high -preset medium -crf {crf} -pix_fmt yuv420p -movflags +faststart+use_metadata_tags -c:a aac -b:a 192k \"{outputPath}\"",
                ".mov" =>
                    $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf {crf} -pix_fmt yuv420p -movflags +faststart+use_metadata_tags -c:a aac -b:a 192k \"{outputPath}\"",
                ".mkv" =>
                    $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf {crf} -pix_fmt yuv420p -c:a aac -b:a 192k \"{outputPath}\"",
                ".webm" =>
                    $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libvpx-vp9 -crf {crf + 8} -b:v 0 -pix_fmt yuv420p -c:a libopus -b:a 128k \"{outputPath}\"",
                ".avi" =>
                    $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf {crf} -pix_fmt yuv420p -c:a libmp3lame -b:a 192k \"{outputPath}\"",
                ".gif" =>
                    $"-y -i \"{inputPath}\" -vf \"fps=15,scale=480:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" \"{outputPath}\"",
                _ =>
                    $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset medium -crf {crf} -pix_fmt yuv420p -c:a aac -b:a 192k \"{outputPath}\""
            };
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
            string args = $"-y -i \"{inputPath}\" -map_metadata 0 -vf \"{filter},scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -crf 20 -preset faster -pix_fmt yuv420p -movflags +faststart+use_metadata_tags -c:a copy \"{outputPath}\"";
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
                        string args = $"-y -threads 0 -i \"{tempPng}\" -map_metadata 0 -c:v libwebp -quality 95 \"{outputPath}\"";
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
                "ogg" => "-c:a libvorbis -q:a 5",
                "aac" or "m4a" => "-c:a aac -b:a 256k",
                _ => "-c:a libmp3lame -b:a 320k"
            };

            Logger.Info($"[FFmpeg] Trích xuất Audio từ [{Path.GetFileName(inputPath)}] ➔ [.{outExt}]...");
            string args = $"-y -i \"{inputPath}\" -map_metadata 0 -vn {codecArgs} \"{outputPath}\"";
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

            string args = $"-y -i \"{inputPath}\" -map_metadata 0 -c:v copy -an \"{outputPath}\"";
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

        public async Task<bool> TrimVideoAsync(
            string inputPath, 
            string outputPath, 
            double startTimeSec, 
            double durationSec, 
            CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath)) return false;

            string ffmpeg = FindFFmpegPath();
            string durArg = durationSec > 0 ? $"-t {durationSec}" : "";
            Logger.Info($"[FFmpeg] Cắt Video [{Path.GetFileName(inputPath)}] từ {startTimeSec}s ({durationSec}s) qua Stream Copy 0.1s...");

            string args = $"-y -ss {startTimeSec} -i \"{inputPath}\" {durArg} -map_metadata 0 -c copy \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Cắt Video thành công: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                return true;
            }

            Logger.Error($"[FFmpeg] Cắt Video thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        public async Task<bool> ExtractFrameAsync(
            string inputPath, 
            string outputPath, 
            double timeSec = 0, 
            CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath)) return false;

            string ffmpeg = FindFFmpegPath();
            Logger.Info($"[FFmpeg] Trích xuất frame tại {timeSec}s từ [{Path.GetFileName(inputPath)}] ra PNG...");

            string args = $"-y -ss {timeSec} -i \"{inputPath}\" -vframes 1 -q:v 2 \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Trích xuất frame thành công: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(newSize)})");
                return true;
            }

            Logger.Error($"[FFmpeg] Trích xuất frame thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        public async Task<bool> AdjustAudioVolumeAsync(
            string inputPath,
            string outputPath,
            double volumeMultiplier = 1.5,
            CancellationToken cancellationToken = default)
        {
            if (IsImageFile(inputPath)) return false;

            string ffmpeg = FindFFmpegPath();
            string volArg = volumeMultiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            Logger.Info($"[FFmpeg] Điều chỉnh âm lượng x{volArg} cho [{Path.GetFileName(inputPath)}]...");

            string args = IsAudioFile(inputPath)
                ? $"-y -i \"{inputPath}\" -map_metadata 0 -filter:a \"volume={volArg}\" \"{outputPath}\""
                : $"-y -i \"{inputPath}\" -map_metadata 0 -c:v copy -filter:a \"volume={volArg}\" \"{outputPath}\"";

            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"[FFmpeg] Điều chỉnh âm lượng thành công: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(newSize)})");
                return true;
            }

            Logger.Error($"[FFmpeg] Điều chỉnh âm lượng thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        public async Task<bool> StripMetadataAsync(
            string inputPath,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            string fileName = Path.GetFileName(inputPath);
            long oldSize = new FileInfo(inputPath).Length;

            if (IsVideoFile(inputPath))
            {
                Logger.Info($"[FFmpeg] Xóa toàn bộ Metadata Video [{fileName}] (Stream Copy)...");
                string args = $"-y -i \"{inputPath}\" -map 0 -map_metadata -1 -map_chapters -1 -c copy \"{outputPath}\"";
                int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

                if (code != 0 || !File.Exists(outputPath))
                {
                    string fallbackArgs = $"-y -i \"{inputPath}\" -map_metadata -1 -map_chapters -1 -c:v copy -c:a copy \"{outputPath}\"";
                    code = await ProcessRunner.RunProcessAsync(ffmpeg, fallbackArgs, cancellationToken: cancellationToken);
                }

                if (code == 0 && File.Exists(outputPath))
                {
                    long newSize = new FileInfo(outputPath).Length;
                    Logger.Success($"[FFmpeg] Đã xóa sạch Metadata Video: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                    return true;
                }

                Logger.Error($"[FFmpeg] Xóa Metadata Video thất bại cho {fileName}");
                return false;
            }

            if (IsImageFile(inputPath))
            {
                string outExt = Path.GetExtension(outputPath).ToLowerInvariant();
                Logger.Info($"[FFmpeg] Xóa toàn bộ Metadata/EXIF Ảnh [{fileName}]...");

                string args = outExt switch
                {
                    ".jpg" or ".jpeg" or ".jfif" => $"-y -i \"{inputPath}\" -map_metadata -1 -frames:v 1 -update 1 -q:v 2 -pix_fmt yuvj420p -color_range 2 \"{outputPath}\"",
                    ".png" => $"-y -i \"{inputPath}\" -map_metadata -1 -frames:v 1 -update 1 -c:v png -compression_level 6 \"{outputPath}\"",
                    ".webp" => $"-y -i \"{inputPath}\" -map_metadata -1 -frames:v 1 -update 1 -c:v libwebp -quality 95 \"{outputPath}\"",
                    ".bmp" => $"-y -i \"{inputPath}\" -map_metadata -1 -frames:v 1 -update 1 -c:v bmp \"{outputPath}\"",
                    ".gif" => $"-y -i \"{inputPath}\" -map_metadata -1 \"{outputPath}\"",
                    _ => $"-y -i \"{inputPath}\" -map_metadata -1 -frames:v 1 -update 1 -q:v 2 \"{outputPath}\""
                };

                int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);
                if (code == 0 && File.Exists(outputPath))
                {
                    long newSize = new FileInfo(outputPath).Length;
                    Logger.Success($"[FFmpeg] Đã xóa sạch Metadata Ảnh: {Path.GetFileName(outputPath)} ({SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)})");
                    return true;
                }

                Logger.Error($"[FFmpeg] Xóa Metadata Ảnh thất bại cho {fileName}");
                return false;
            }

            return false;
        }

        private sealed class ExifPhotoInfo
        {
            public string Make { get; init; } = string.Empty;
            public string Model { get; init; } = string.Empty;
            public string Lens { get; init; } = string.Empty;
            public string FocalLength { get; init; } = string.Empty;
            public string Aperture { get; init; } = string.Empty;
            public string ShutterSpeed { get; init; } = string.Empty;
            public string Iso { get; init; } = string.Empty;
            public string DateTaken { get; init; } = string.Empty;
        }

        public Task<bool> CreateExifFrameAsync(
            string inputPath,
            string outputPath,
            int frameStyle = 0,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetResult(false);
                        return;
                    }

                    string fileName = Path.GetFileName(inputPath);
                    Logger.Info($"[ExifFrame] Đang trích xuất metadata & render khung ảnh [{fileName}]...");

                    string decodePath = inputPath;
                    string? tempFallbackPath = null;
                    BitmapFrame? rawFrame = null;

                    try
                    {
                        rawFrame = LoadBitmapFrame(decodePath);
                    }
                    catch
                    {
                        string ffmpeg = FindFFmpegPath();
                        if (!File.Exists(ffmpeg))
                        {
                            Logger.Error($"[ExifFrame] Windows thiếu codec đọc {Path.GetExtension(inputPath).ToUpperInvariant()} và chưa có ffmpeg.exe để giải mã thay thế.");
                            tcs.SetResult(false);
                            return;
                        }

                        Logger.Info($"[ExifFrame] Dùng FFmpeg giải mã định dạng {Path.GetExtension(inputPath).ToUpperInvariant()}...");
                        tempFallbackPath = Path.Combine(Path.GetTempPath(), $"cmdbox_exif_{Guid.NewGuid():N}.jpg");
                        string ffArgs = $"-y -i \"{inputPath}\" -map_metadata 0 -frames:v 1 -update 1 -q:v 1 -pix_fmt yuvj420p -color_range 2 \"{tempFallbackPath}\"";
                        int ffCode = ProcessRunner.RunProcessAsync(ffmpeg, ffArgs, cancellationToken: cancellationToken).GetAwaiter().GetResult();

                        if (ffCode != 0 || !File.Exists(tempFallbackPath))
                        {
                            Logger.Error($"[ExifFrame] FFmpeg không thể giải mã tệp {fileName}.");
                            tcs.SetResult(false);
                            return;
                        }

                        decodePath = tempFallbackPath;
                        rawFrame = LoadBitmapFrame(decodePath);
                    }

                    try
                    {
                        if (rawFrame == null)
                        {
                            Logger.Error($"[ExifFrame] Không đọc được frame ảnh: {fileName}");
                            tcs.SetResult(false);
                            return;
                        }

                        var metadata = rawFrame.Metadata as BitmapMetadata;
                        ExifPhotoInfo exif = ExtractExifInfo(metadata, inputPath);
                        if (string.IsNullOrWhiteSpace(exif.Model) && string.IsNullOrWhiteSpace(exif.Make))
                        {
                            exif = EnrichExifFromFfmpeg(exif, inputPath);
                        }
                        BitmapSource source = ApplyExifOrientation(rawFrame, metadata);

                    int imgW = source.PixelWidth;
                    int imgH = source.PixelHeight;
                    int shortSide = Math.Min(imgW, imgH);

                    bool isDark = frameStyle == 1;
                    bool isPolaroid = frameStyle == 2;

                    double sidePad = Math.Max(24, Math.Round(shortSide * (isPolaroid ? 0.055 : 0.032)));
                    double topPad = sidePad;
                    double bottomPad = Math.Max(90, Math.Round(shortSide * (isPolaroid ? 0.175 : 0.125)));

                    int canvasW = (int)Math.Round(imgW + sidePad * 2);
                    int canvasH = (int)Math.Round(imgH + topPad + bottomPad);

                    // Giới hạn kích thước render tối đa an toàn tránh tràn VRAM với ảnh quá lớn
                    const int maxDim = 4800;
                    double scale = 1.0;
                    if (Math.Max(canvasW, canvasH) > maxDim)
                    {
                        scale = (double)maxDim / Math.Max(canvasW, canvasH);
                        canvasW = (int)Math.Round(canvasW * scale);
                        canvasH = (int)Math.Round(canvasH * scale);
                        sidePad *= scale;
                        topPad *= scale;
                        bottomPad *= scale;
                    }

                    double drawImgW = canvasW - sidePad * 2;
                    double drawImgH = canvasH - topPad - bottomPad;

                    Brush bgBrush = isDark ? new SolidColorBrush(Color.FromRgb(18, 18, 20)) : Brushes.White;
                    Brush primaryText = isDark ? new SolidColorBrush(Color.FromRgb(244, 244, 246)) : new SolidColorBrush(Color.FromRgb(24, 24, 27));
                    Brush secondaryText = isDark ? new SolidColorBrush(Color.FromRgb(161, 161, 170)) : new SolidColorBrush(Color.FromRgb(113, 113, 122));
                    Brush accentBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Chấm đỏ kiểu Leica
                    Pen dividerPen = new Pen(isDark ? new SolidColorBrush(Color.FromRgb(55, 55, 62)) : new SolidColorBrush(Color.FromRgb(228, 228, 231)), Math.Max(1.0, bottomPad * 0.014));

                    string cameraTitle = BuildCameraTitle(exif);
                    string leftSub = !string.IsNullOrWhiteSpace(exif.DateTaken)
                        ? exif.DateTaken
                        : $"{imgW} × {imgH}";

                    var specs = new[] { exif.FocalLength, exif.Aperture, exif.ShutterSpeed, exif.Iso }
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();
                    string specsText = specs.Length > 0 ? string.Join("  ·  ", specs) : $"{imgW} × {imgH} px";
                    string rightSub = !string.IsNullOrWhiteSpace(exif.Lens) ? exif.Lens : Path.GetExtension(inputPath).TrimStart('.').ToUpperInvariant();

                    double titleSize = Math.Max(13, bottomPad * 0.22);
                    double subSize = Math.Max(10, bottomPad * 0.145);
                    var typefaceBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var typefaceSemi = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                    var typefaceReg = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

                    var visual = new DrawingVisual();
                    using (DrawingContext dc = visual.RenderOpen())
                    {
                        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, canvasW, canvasH));
                        dc.DrawImage(source, new Rect(sidePad, topPad, drawImgW, drawImgH));

                        double barTop = topPad + drawImgH;
                        double centerY = barTop + bottomPad * 0.5;

                        if (isPolaroid)
                        {
                            var ftTitle = CreateFormattedText(cameraTitle, typefaceBold, titleSize, primaryText);
                            var ftSpecs = CreateFormattedText($"{specsText}   |   {leftSub}", typefaceReg, subSize, secondaryText);

                            double totalTextH = ftTitle.Height + ftSpecs.Height + bottomPad * 0.08;
                            double startY = centerY - totalTextH / 2.0;

                            dc.DrawText(ftTitle, new Point((canvasW - ftTitle.Width) / 2.0, startY));
                            dc.DrawText(ftSpecs, new Point((canvasW - ftSpecs.Width) / 2.0, startY + ftTitle.Height + bottomPad * 0.08));
                        }
                        else
                        {
                            var ftLeftTitle = CreateFormattedText(cameraTitle, typefaceBold, titleSize, primaryText);
                            var ftLeftSub = CreateFormattedText(leftSub, typefaceReg, subSize, secondaryText);

                            var ftRightTitle = CreateFormattedText(specsText, typefaceSemi, titleSize * 0.92, primaryText);
                            var ftRightSub = CreateFormattedText(rightSub, typefaceReg, subSize, secondaryText);

                            double leftH = ftLeftTitle.Height + ftLeftSub.Height + bottomPad * 0.05;
                            double leftY = centerY - leftH / 2.0;
                            dc.DrawText(ftLeftTitle, new Point(sidePad, leftY));
                            dc.DrawText(ftLeftSub, new Point(sidePad, leftY + ftLeftTitle.Height + bottomPad * 0.05));

                            double rightH = ftRightTitle.Height + ftRightSub.Height + bottomPad * 0.05;
                            double rightY = centerY - rightH / 2.0;
                            double rightTitleX = canvasW - sidePad - ftRightTitle.Width;
                            double rightSubX = canvasW - sidePad - ftRightSub.Width;
                            dc.DrawText(ftRightTitle, new Point(rightTitleX, rightY));
                            dc.DrawText(ftRightSub, new Point(rightSubX, rightY + ftRightTitle.Height + bottomPad * 0.05));

                            // Dấu chấm đỏ + vạch ngăn tinh tế bên trái cụm thông số
                            double rightBlockLeft = Math.Min(rightTitleX, rightSubX);
                            double divX = rightBlockLeft - sidePad * 0.65;
                            if (divX > sidePad + ftLeftTitle.Width + sidePad * 0.5)
                            {
                                double dotRadius = Math.Max(3.0, bottomPad * 0.065);
                                dc.DrawEllipse(accentBrush, null, new Point(divX - dotRadius * 2.4, centerY), dotRadius, dotRadius);
                                dc.DrawLine(dividerPen, new Point(divX, centerY - bottomPad * 0.24), new Point(divX, centerY + bottomPad * 0.24));
                            }
                        }
                    }

                    var rtb = new RenderTargetBitmap(canvasW, canvasH, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(visual);

                    string outExt = Path.GetExtension(outputPath).ToLowerInvariant();
                    BitmapEncoder encoder = outExt == ".png"
                        ? new PngBitmapEncoder()
                        : new JpegBitmapEncoder { QualityLevel = 95 };
                    encoder.Frames.Add(BitmapFrame.Create(rtb));

                    using (var outFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(outFs);
                    }

                    Logger.Success($"[ExifFrame] Đã tạo khung EXIF: {Path.GetFileName(outputPath)} ({canvasW}×{canvasH})");
                    tcs.SetResult(true);
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(tempFallbackPath) && File.Exists(tempFallbackPath))
                        {
                            try { File.Delete(tempFallbackPath); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ExifFrame] Lỗi tạo khung EXIF: {ex.Message}");
                    tcs.SetResult(false);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }

        private static FormattedText CreateFormattedText(string text, Typeface typeface, double emSize, Brush brush)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                emSize,
                brush,
                1.0);
        }

        private static string BuildCameraTitle(ExifPhotoInfo exif)
        {
            if (string.IsNullOrWhiteSpace(exif.Model) && string.IsNullOrWhiteSpace(exif.Make))
                return "SHOT ON CAMERA";

            if (string.IsNullOrWhiteSpace(exif.Make))
                return exif.Model.Trim();

            if (string.IsNullOrWhiteSpace(exif.Model))
                return exif.Make.Trim();

            if (exif.Model.Contains(exif.Make, StringComparison.OrdinalIgnoreCase))
                return exif.Model.Trim();

            return $"{exif.Make.Trim()} {exif.Model.Trim()}";
        }

        private static ExifPhotoInfo ExtractExifInfo(BitmapMetadata? meta, string filePath)
        {
            if (meta == null)
            {
                return new ExifPhotoInfo
                {
                    DateTaken = File.GetLastWriteTime(filePath).ToString("yyyy.MM.dd HH:mm")
                };
            }

            string make = ReadMetaString(meta, "/app1/ifd/{ushort=271}");
            string model = ReadMetaString(meta, "/app1/ifd/{ushort=272}");
            string lens = ReadMetaString(meta, "/app1/ifd/exif/{ushort=42036}");

            double? focal35 = ReadMetaNumber(meta, "/app1/ifd/exif/{ushort=41989}");
            double? focal = focal35 ?? ReadMetaRational(meta, "/app1/ifd/exif/{ushort=37386}");
            string focalStr = focal.HasValue && focal.Value > 0 ? $"{Math.Round(focal.Value):0}mm" : string.Empty;

            double? fNum = ReadMetaRational(meta, "/app1/ifd/exif/{ushort=33437}");
            string fStr = fNum.HasValue && fNum.Value > 0 ? $"f/{fNum.Value.ToString("0.#", CultureInfo.InvariantCulture)}" : string.Empty;

            double? exp = ReadMetaRational(meta, "/app1/ifd/exif/{ushort=33434}");
            string expStr = string.Empty;
            if (exp.HasValue && exp.Value > 0)
            {
                expStr = exp.Value >= 1.0
                    ? $"{exp.Value.ToString("0.#", CultureInfo.InvariantCulture)}s"
                    : $"1/{Math.Round(1.0 / exp.Value):0}s";
            }

            double? iso = ReadMetaNumber(meta, "/app1/ifd/exif/{ushort=34855}");
            string isoStr = iso.HasValue && iso.Value > 0 ? $"ISO {(int)iso.Value}" : string.Empty;

            string rawDate = ReadMetaString(meta, "/app1/ifd/exif/{ushort=36867}");
            string dateStr = FormatExifDate(rawDate, filePath);

            return new ExifPhotoInfo
            {
                Make = make,
                Model = model,
                Lens = lens,
                FocalLength = focalStr,
                Aperture = fStr,
                ShutterSpeed = expStr,
                Iso = isoStr,
                DateTaken = dateStr
            };
        }

        private static string FormatExifDate(string rawDate, string filePath)
        {
            if (!string.IsNullOrWhiteSpace(rawDate) &&
                DateTime.TryParseExact(rawDate.Trim(), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt.ToString("yyyy.MM.dd HH:mm");
            }
            return File.GetLastWriteTime(filePath).ToString("yyyy.MM.dd HH:mm");
        }

        private static string ReadMetaString(BitmapMetadata meta, string query)
        {
            try
            {
                if (meta.ContainsQuery(query))
                    return meta.GetQuery(query)?.ToString()?.Trim() ?? string.Empty;
            }
            catch { }
            return string.Empty;
        }

        private static double? ReadMetaRational(BitmapMetadata meta, string query)
        {
            try
            {
                if (!meta.ContainsQuery(query)) return null;
                object? val = meta.GetQuery(query);
                if (val is ulong u)
                {
                    uint num = (uint)(u & 0xFFFFFFFF);
                    uint den = (uint)(u >> 32);
                    if (den != 0) return (double)num / den;
                }
                else if (val is long l)
                {
                    int num = (int)(l & 0xFFFFFFFF);
                    int den = (int)(l >> 32);
                    if (den != 0) return (double)num / den;
                }
                else if (val != null && double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                {
                    return d;
                }
            }
            catch { }
            return null;
        }

        private static double? ReadMetaNumber(BitmapMetadata meta, string query)
        {
            try
            {
                if (!meta.ContainsQuery(query)) return null;
                object? val = meta.GetQuery(query);
                if (val is ushort[] arr && arr.Length > 0) return arr[0];
                if (val != null) return Convert.ToDouble(val, CultureInfo.InvariantCulture);
            }
            catch { }
            return null;
        }

        private static BitmapSource ApplyExifOrientation(BitmapSource source, BitmapMetadata? meta)
        {
            double? ori = meta != null ? ReadMetaNumber(meta, "/app1/ifd/{ushort=274}") : null;
            int angle = ori switch
            {
                3 => 180,
                6 => 90,
                8 => 270,
                _ => 0
            };

            if (angle == 0) return source;
            return new TransformedBitmap(source, new RotateTransform(angle));
        }

        private static BitmapFrame? LoadBitmapFrame(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return decoder.Frames.Count > 0 ? decoder.Frames[0] : null;
        }

        private ExifPhotoInfo EnrichExifFromFfmpeg(ExifPhotoInfo current, string inputPath)
        {
            try
            {
                string ffmpeg = FindFFmpegPath();
                if (!File.Exists(ffmpeg)) return current;

                string output = ProcessRunner.RunCommandAndGetOutputAsync(ffmpeg, $"-i \"{inputPath}\"").GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(output)) return current;

                string make = current.Make;
                string model = current.Model;
                string dateTaken = current.DateTaken;

                foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int colonIdx = rawLine.IndexOf(':');
                    if (colonIdx <= 0) continue;

                    string key = rawLine[..colonIdx].Trim().ToLowerInvariant();
                    string val = rawLine[(colonIdx + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(val)) continue;

                    if (string.IsNullOrWhiteSpace(make) && (key == "make" || key.EndsWith(".make")))
                        make = val;
                    else if (string.IsNullOrWhiteSpace(model) && (key == "model" || key.EndsWith(".model")))
                        model = val;
                    else if ((key == "creation_time" || key == "date") && DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                        dateTaken = dt.ToString("yyyy.MM.dd HH:mm");
                }

                return new ExifPhotoInfo
                {
                    Make = make,
                    Model = model,
                    Lens = current.Lens,
                    FocalLength = current.FocalLength,
                    Aperture = current.Aperture,
                    ShutterSpeed = current.ShutterSpeed,
                    Iso = current.Iso,
                    DateTaken = dateTaken
                };
            }
            catch
            {
                return current;
            }
        }
    }
}
