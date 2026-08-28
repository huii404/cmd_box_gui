using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CMD_BOX_GUI.Core;

namespace CMD_BOX_GUI.Services
{
    public class MediaService
    {
        private static readonly byte[] MagicMarker = Encoding.UTF8.GetBytes("---CMD_BOX_SECRET_PAYLOAD---");

        public string FindFFmpegPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFfmpeg = Path.Combine(appDir, "ffmpeg.exe");
            if (File.Exists(localFfmpeg)) return localFfmpeg;

            string localBinFfmpeg = Path.Combine(appDir, "bin", "ffmpeg.exe");
            if (File.Exists(localBinFfmpeg)) return localBinFfmpeg;

            return "ffmpeg.exe";
        }

        // 1. TRÍCH XUẤT ÂM THANH MP3 (VỚI TÙY CHỌN BITRATE)
        public async Task<bool> ExtractAudioMp3Async(string inputPath, string outputPath, int bitrateKbps = 192, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Trích xuất MP3 ({bitrateKbps}kbps) từ [{Path.GetFileName(inputPath)}]...");

            string args = $"-y -i \"{inputPath}\" -vn -c:a libmp3lame -b:a {bitrateKbps}k \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã trích xuất MP3: {outputPath}");
                return true;
            }
            Logger.Error($"Trích xuất MP3 thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 2. NÉN VIDEO (VỚI HỆ SỐ CRF)
        public async Task<bool> CompressVideoAsync(string inputPath, string outputPath, int crf = 26, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Nén Video [{Path.GetFileName(inputPath)}] (CRF {crf})...");

            string args = $"-y -i \"{inputPath}\" -vcodec libx264 -crf {crf} -preset faster -c:a aac -b:a 128k \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"Nén xong [{Path.GetFileName(inputPath)}]: {SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)}");
                return true;
            }
            Logger.Error($"Nén Video thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 3. LÀM NÉT & KHỬ NHIỄU VIDEO (MEDIA ENHANCEMENT)
        public async Task<bool> EnhanceMediaAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Làm nét & khử nhiễu video [{Path.GetFileName(inputPath)}]...");

            string args = $"-y -i \"{inputPath}\" -vf \"unsharp=5:5:1.0:5:5:0.0,hqdn3d=2:1.5:3:2.5\" -c:a copy \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã làm nét & khử nhiễu: {outputPath}");
                return true;
            }
            Logger.Error($"Làm nét video thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 4. ĐỔI TỐC ĐỘ PHÁT VIDEO
        public async Task<bool> ChangeVideoSpeedAsync(string inputPath, string outputPath, double speed, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Đổi tốc độ video [{Path.GetFileName(inputPath)}] sang {speed}x...");

            double setpts = 1.0 / speed;
            string args = $"-y -i \"{inputPath}\" -filter_complex \"[0:v]setpts={setpts:0.00}*PTS[v];[0:a]atempo={speed:0.00}[a]\" -map \"[v]\" -map \"[a]\" \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã đổi tốc độ Video ({speed}x): {outputPath}");
                return true;
            }
            Logger.Error($"Đổi tốc độ thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 5. CHUYỂN ĐỔI ĐỊNH DẠNG (CONVERT FORMAT)
        public async Task<bool> ConvertFormatAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Chuyển đổi định dạng [{Path.GetFileName(inputPath)}] ➔ [{Path.GetExtension(outputPath)}]...");

            string args = $"-y -i \"{inputPath}\" \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Chuyển đổi định dạng thành công: {outputPath}");
                return true;
            }
            Logger.Error($"Chuyển đổi định dạng thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 6. TẮT / XÓA ÂM THANH (MUTE VIDEO)
        public async Task<bool> RemoveAudioAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Tách âm thanh khỏi [{Path.GetFileName(inputPath)}]...");

            string args = $"-y -i \"{inputPath}\" -c:v copy -an \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã tách âm thanh thành công: {outputPath}");
                return true;
            }
            Logger.Error($"Tách âm thanh thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 7. CHUYỂN VIDEO SANG ẢNH GIF ĐỘNG
        public async Task<bool> VideoToGifAsync(string inputPath, string outputPath, int fps = 12, int width = 480, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Tạo GIF từ [{Path.GetFileName(inputPath)}] (FPS {fps}, Rộng {width}px)...");

            string args = $"-y -i \"{inputPath}\" -vf \"fps={fps},scale={width}:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã tạo file GIF: {outputPath}");
                return true;
            }
            Logger.Error($"Tạo GIF thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 8. TRÍCH XUẤT ẢNH THUMBNAIL (SNAPSHOT)
        public async Task<bool> ExtractThumbnailAsync(string inputPath, string outputPath, string timestamp = "00:00:01", CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Chụp khung hình tại {timestamp} từ [{Path.GetFileName(inputPath)}]...");

            string args = $"-y -ss {timestamp} -i \"{inputPath}\" -vframes 1 -q:v 2 \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã trích xuất Thumbnail: {outputPath}");
                return true;
            }
            Logger.Error($"Trích xuất Thumbnail thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 9. ĐỔI ĐỘ PHÂN GIẢI (RESIZE / SCALE)
        public async Task<bool> ResizeVideoAsync(string inputPath, string outputPath, int width, int height, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Đổi độ phân giải [{Path.GetFileName(inputPath)}] ➔ {width}x{height}...");

            string args = $"-y -i \"{inputPath}\" -vf \"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2\" -c:a copy \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã đổi độ phân giải thành công: {outputPath}");
                return true;
            }
            Logger.Error($"Đổi độ phân giải thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 10. CẮT VIDEO THEO THỜI GIAN (TRIM / CUT)
        public async Task<bool> TrimVideoAsync(string inputPath, string outputPath, string startTime, string endTime, CancellationToken cancellationToken = default)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Cắt video [{Path.GetFileName(inputPath)}] từ {startTime} đến {endTime}...");

            string args = $"-y -ss {startTime} -to {endTime} -i \"{inputPath}\" -c copy \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args, cancellationToken: cancellationToken);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã cắt video thành công: {outputPath}");
                return true;
            }
            Logger.Error($"Cắt video thất bại cho {Path.GetFileName(inputPath)}");
            return false;
        }

        // 11. CHUẨN HÓA TÊN FILE TRONG THƯ MỤC
        public void NormalizeFilenamesInDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;
            Logger.Info($"Chuẩn hóa tên file trong thư mục: {dirPath}");

            int count = 0;
            var dir = new DirectoryInfo(dirPath);
            foreach (var file in dir.GetFiles())
            {
                string oldName = file.Name;
                string cleanName = Regex.Replace(oldName, @"[^\w\-\.]+", "_");
                cleanName = Regex.Replace(cleanName, @"_+", "_").Trim('_');

                if (oldName != cleanName)
                {
                    try
                    {
                        string newPath = Path.Combine(dirPath, cleanName);
                        if (!File.Exists(newPath))
                        {
                            file.MoveTo(newPath);
                            count++;
                        }
                    }
                    catch { }
                }
            }

            Logger.Success($"Đã chuẩn hóa {count} tên file!");
        }

        // 12. GIẤU FILE TRONG FILE (STEGANOGRAPHY)
        public async Task<bool> HideFileInMediaAsync(string containerPath, string secretFilePath, string outputPath, CancellationToken cancellationToken = default)
        {
            Logger.Info($"Giấu tệp [{Path.GetFileName(secretFilePath)}] vào [{Path.GetFileName(containerPath)}]...");
            try
            {
                await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                    using var containerStream = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                    await containerStream.CopyToAsync(outStream, cancellationToken);

                    await outStream.WriteAsync(MagicMarker.AsMemory(0, MagicMarker.Length), cancellationToken);

                    string secretFileName = Path.GetFileName(secretFilePath);
                    byte[] nameBytes = Encoding.UTF8.GetBytes(secretFileName);
                    byte[] nameLenBytes = BitConverter.GetBytes(nameBytes.Length);
                    await outStream.WriteAsync(nameLenBytes.AsMemory(0, nameLenBytes.Length), cancellationToken);
                    await outStream.WriteAsync(nameBytes.AsMemory(0, nameBytes.Length), cancellationToken);

                    using var secretStream = new FileStream(secretFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                    await secretStream.CopyToAsync(outStream, cancellationToken);
                }, cancellationToken);

                Logger.Success($"Đã giấu tệp thành công: {outputPath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("Đã hủy thao tác giấu file.");
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Lỗi giấu tệp: {ex.Message}");
                return false;
            }
        }

        // 13. TRÍCH XUẤT FILE ẨN
        public async Task<bool> ExtractHiddenFileAsync(string containerPath, string outputDirectory, CancellationToken cancellationToken = default)
        {
            Logger.Info($"Quét tìm tệp ẩn trong [{Path.GetFileName(containerPath)}]...");
            try
            {
                return await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] allBytes = File.ReadAllBytes(containerPath);
                    int markerIndex = FindMarkerIndex(allBytes, MagicMarker);

                    if (markerIndex == -1)
                    {
                        byte[] zipHeader = { 0x50, 0x4B, 0x03, 0x04 };
                        int zipIndex = FindMarkerIndex(allBytes, zipHeader);
                        if (zipIndex != -1 && zipIndex > 100)
                        {
                            string zipOut = Path.Combine(outputDirectory, "extracted_payload.zip");
                            using var fs = new FileStream(zipOut, FileMode.Create, FileAccess.Write);
                            fs.Write(allBytes, zipIndex, allBytes.Length - zipIndex);
                            Logger.Success($"Đã trích xuất payload ZIP: {zipOut}");
                            return true;
                        }

                        Logger.Warning("Không tìm thấy tệp ẩn nào.");
                        return false;
                    }

                    int pos = markerIndex + MagicMarker.Length;
                    int nameLen = BitConverter.ToInt32(allBytes, pos);
                    pos += 4;

                    string secretFileName = Encoding.UTF8.GetString(allBytes, pos, nameLen);
                    pos += nameLen;

                    string outFilePath = Path.Combine(outputDirectory, secretFileName);
                    using (var fs = new FileStream(outFilePath, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(allBytes, pos, allBytes.Length - pos);
                    }

                    Logger.Success($"Đã trích xuất tệp ẩn: {outFilePath}");
                    return true;
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("Đã hủy thao tác trích xuất.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Lỗi trích xuất: {ex.Message}");
                return false;
            }
        }

        private static int FindMarkerIndex(byte[] source, byte[] pattern)
        {
            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
