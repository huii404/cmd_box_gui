using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

            string cppProjectFfmpeg = @"G:\Code\C++\project\CMD\bin\ffmpeg.exe";
            if (File.Exists(cppProjectFfmpeg)) return cppProjectFfmpeg;

            return "ffmpeg.exe";
        }

        // 1. TRÍCH XUẤT ÂM THANH MP3
        public async Task<bool> ExtractAudioMp3Async(string inputPath, string outputPath)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Trích xuất MP3 từ [{Path.GetFileName(inputPath)}]...");

            string args = $"-y -i \"{inputPath}\" -vn -c:a libmp3lame -q:a 2 \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã tạo file MP3: {outputPath}");
                return true;
            }
            Logger.Error("Trích xuất MP3 thất bại.");
            return false;
        }

        // 2. NÉN VIDEO
        public async Task<bool> CompressVideoAsync(string inputPath, string outputPath, int crf = 26)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Nén Video [{Path.GetFileName(inputPath)}] (CRF {crf})...");

            string args = $"-y -i \"{inputPath}\" -vcodec libx264 -crf {crf} -preset faster -c:a aac -b:a 128k \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args);

            if (code == 0 && File.Exists(outputPath))
            {
                long oldSize = new FileInfo(inputPath).Length;
                long newSize = new FileInfo(outputPath).Length;
                Logger.Success($"Nén xong: {SystemCore.FormatBytes(oldSize)} ➔ {SystemCore.FormatBytes(newSize)}");
                return true;
            }
            Logger.Error("Nén Video thất bại.");
            return false;
        }

        // 3. LÀM NÉT & KHỬ NHIỄU VIDEO (MEDIA ENHANCEMENT)
        public async Task<bool> EnhanceMediaAsync(string inputPath, string outputPath)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Làm nét & khử nhiễu video [{Path.GetFileName(inputPath)}]...");

            string args = $"-y -i \"{inputPath}\" -vf \"unsharp=5:5:1.0:5:5:0.0,hqdn3d=2:1.5:3:2.5\" -c:a copy \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã làm nét & khử nhiễu video thành công: {outputPath}");
                return true;
            }
            Logger.Error("Làm nét video thất bại.");
            return false;
        }

        // 4. ĐỔI TỐC ĐỘ PHÁT VIDEO
        public async Task<bool> ChangeVideoSpeedAsync(string inputPath, string outputPath, double speed)
        {
            string ffmpeg = FindFFmpegPath();
            Logger.Info($"Đổi tốc độ video sang {speed}x...");

            double setpts = 1.0 / speed;
            string args = $"-y -i \"{inputPath}\" -filter_complex \"[0:v]setpts={setpts:0.00}*PTS[v];[0:a]atempo={speed:0.00}[a]\" -map \"[v]\" -map \"[a]\" \"{outputPath}\"";
            int code = await ProcessRunner.RunProcessAsync(ffmpeg, args);

            if (code == 0 && File.Exists(outputPath))
            {
                Logger.Success($"Đã đổi tốc độ Video ({speed}x): {outputPath}");
                return true;
            }
            Logger.Error("Đổi tốc độ thất bại.");
            return false;
        }

        // 5. CHUẨN HÓA TÊN FILE TRONG THƯ MỤC
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

        // 6. GIẤU FILE TRONG FILE (STEGANOGRAPHY)
        public async Task<bool> HideFileInMediaAsync(string containerPath, string secretFilePath, string outputPath)
        {
            Logger.Info($"Giấu tệp [{Path.GetFileName(secretFilePath)}] vào [{Path.GetFileName(containerPath)}]...");
            try
            {
                await Task.Run(() =>
                {
                    using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                    using var containerStream = new FileStream(containerPath, FileMode.Open, FileAccess.Read);
                    containerStream.CopyTo(outStream);

                    outStream.Write(MagicMarker, 0, MagicMarker.Length);

                    string secretFileName = Path.GetFileName(secretFilePath);
                    byte[] nameBytes = Encoding.UTF8.GetBytes(secretFileName);
                    byte[] nameLenBytes = BitConverter.GetBytes(nameBytes.Length);
                    outStream.Write(nameLenBytes, 0, nameLenBytes.Length);
                    outStream.Write(nameBytes, 0, nameBytes.Length);

                    using var secretStream = new FileStream(secretFilePath, FileMode.Open, FileAccess.Read);
                    secretStream.CopyTo(outStream);
                });

                Logger.Success($"Đã giấu tệp thành công: {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Lỗi giấu tệp: {ex.Message}");
                return false;
            }
        }

        // 7. TRÍCH XUẤT FILE ẨN
        public async Task<bool> ExtractHiddenFileAsync(string containerPath, string outputDirectory)
        {
            Logger.Info($"Quét tìm tệp ẩn trong [{Path.GetFileName(containerPath)}]...");
            try
            {
                return await Task.Run(() =>
                {
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
                });
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
