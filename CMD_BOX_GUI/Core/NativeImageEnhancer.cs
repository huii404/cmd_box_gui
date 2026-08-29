using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CMD_BOX_GUI.Core
{
    /// <summary>
    /// Bộ xử lý làm nét & tối ưu ảnh thuần C# (.NET) siêu tốc độ cao:
    /// - Không cần công cụ CLI ngoài (không cần Vulkan / tệp .bin cồng kềnh)
    /// - Xử lý trực tiếp trên RAM bằng đa luồng Parallel.For trên toàn bộ lõi CPU Ryzen
    /// - Tích hợp Unsharp Masking (USM), Tương phản nổi khối 3D (S-Curve), Khử mờ và Tăng rực rỡ màu sắc (Vibrance).
    /// </summary>
    public static class NativeImageEnhancer
    {
        public class EnhanceOptions
        {
            public float Amount { get; set; } = 1.0f;       // Cường độ làm nét (0.3 - 2.5)
            public int Radius { get; set; } = 2;            // Bán kính làm nét (1 - 5 px)
            public int Threshold { get; set; } = 3;         // Ngưỡng lọc nhiễu (chống tăng hạt)
            public float Contrast { get; set; } = 1.06f;    // Tương phản nổi khối (1.0 - 1.2)
            public float Vibrance { get; set; } = 0.08f;    // Tăng rực rỡ thông minh (0.0 - 0.2)
            public int ScalePercent { get; set; } = 100;    // Tỉ lệ phóng to (100% hoặc 200%)
        }

        public static EnhanceOptions GetPreset(int level)
        {
            return level switch
            {
                0 => new EnhanceOptions { Amount = 0.6f, Radius = 1, Threshold = 4, Contrast = 1.03f, Vibrance = 0.04f, ScalePercent = 100 }, // Nhẹ
                2 => new EnhanceOptions { Amount = 1.5f, Radius = 2, Threshold = 2, Contrast = 1.10f, Vibrance = 0.10f, ScalePercent = 100 }, // Cao
                3 => new EnhanceOptions { Amount = 2.2f, Radius = 3, Threshold = 1, Contrast = 1.15f, Vibrance = 0.14f, ScalePercent = 100 }, // Siêu nét
                _ => new EnhanceOptions { Amount = 1.0f, Radius = 2, Threshold = 3, Contrast = 1.06f, Vibrance = 0.07f, ScalePercent = 100 }  // Tiêu chuẩn (Mức 2)
            };
        }

        /// <summary>
        /// Thực thi làm nét ảnh bằng thuần C#
        /// </summary>
        public static async Task<bool> EnhanceImageAsync(string inputPath, string outputPath, int enhanceLevel = 1)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var options = GetPreset(enhanceLevel);

                    // 1. Đọc ảnh gốc vào BitmapSource Bgra32
                    BitmapSource sourceBitmap = LoadBitmap(inputPath);

                    // Nếu có phóng to (ví dụ 2x)
                    if (options.ScalePercent > 100)
                    {
                        double scale = options.ScalePercent / 100.0;
                        var transform = new ScaleTransform(scale, scale);
                        sourceBitmap = new TransformedBitmap(sourceBitmap, transform);
                    }

                    int width = sourceBitmap.PixelWidth;
                    int height = sourceBitmap.PixelHeight;
                    int stride = width * 4;

                    byte[] srcPixels = new byte[height * stride];
                    sourceBitmap.CopyPixels(srcPixels, stride, 0);

                    byte[] dstPixels = new byte[height * stride];

                    // 2. Xử lý thuật toán làm nét & nổi khối đa luồng siêu tốc
                    ProcessSharpenAndEnhance(srcPixels, dstPixels, width, height, stride, options);

                    // 3. Tạo BitmapSource kết quả và lưu ra file
                    var resultBitmap = BitmapSource.Create(
                        width, height,
                        sourceBitmap.DpiX, sourceBitmap.DpiY,
                        PixelFormats.Bgra32, null,
                        dstPixels, stride);

                    SaveBitmap(resultBitmap, outputPath);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Native C#] Lỗi làm nét ảnh: {ex.Message}");
                    return false;
                }
            });
        }

        private static void ProcessSharpenAndEnhance(
            byte[] src, byte[] dst, int width, int height, int stride, EnhanceOptions opts)
        {
            // Tạo bản làm mờ nhanh (Fast Box Blur 2-pass) để làm mốc tần số thấp
            byte[] blurred = FastBlur(src, width, height, stride, opts.Radius);

            float amount = opts.Amount;
            int threshold = opts.Threshold;
            float contrast = opts.Contrast;
            float vibrance = opts.Vibrance;

            Parallel.For(0, height, y =>
            {
                int rowOffset = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int px = rowOffset + (x * 4);

                    // Đọc B, G, R, A
                    int b = src[px];
                    int g = src[px + 1];
                    int r = src[px + 2];
                    byte a = src[px + 3];

                    int blurB = blurred[px];
                    int blurG = blurred[px + 1];
                    int blurR = blurred[px + 2];

                    // Tính chênh lệch biên cạnh vi mô (High-frequency details)
                    int diffB = b - blurB;
                    int diffG = g - blurG;
                    int diffR = r - blurR;

                    // Áp dụng Unsharp Mask với ngưỡng chống nhiễu (Threshold)
                    float sharpB = (Math.Abs(diffB) > threshold) ? b + diffB * amount : b;
                    float sharpG = (Math.Abs(diffG) > threshold) ? g + diffG * amount : g;
                    float sharpR = (Math.Abs(diffR) > threshold) ? r + diffR * amount : r;

                    // Tăng tương phản nổi khối 3D (S-Curve stretching)
                    sharpB = 128f + (sharpB - 128f) * contrast;
                    sharpG = 128f + (sharpG - 128f) * contrast;
                    sharpR = 128f + (sharpR - 128f) * contrast;

                    // Tăng độ rực rỡ thông minh (Vibrance boost - ưu tiên vùng màu nhạt, tránh bão hòa da người)
                    if (vibrance > 0.001f)
                    {
                        float max = Math.Max(sharpR, Math.Max(sharpG, sharpB));
                        float min = Math.Min(sharpR, Math.Min(sharpG, sharpB));
                        float sat = (max - min) / (max + 0.001f);
                        float boost = (1.0f - sat) * vibrance;

                        float gray = 0.299f * sharpR + 0.587f * sharpG + 0.114f * sharpB;
                        sharpR += (sharpR - gray) * boost;
                        sharpG += (sharpG - gray) * boost;
                        sharpB += (sharpB - gray) * boost;
                    }

                    // Giới hạn giá trị trong [0, 255]
                    dst[px] = (byte)Math.Clamp((int)Math.Round(sharpB), 0, 255);
                    dst[px + 1] = (byte)Math.Clamp((int)Math.Round(sharpG), 0, 255);
                    dst[px + 2] = (byte)Math.Clamp((int)Math.Round(sharpR), 0, 255);
                    dst[px + 3] = a;
                }
            });
        }

        /// <summary>
        /// Thuật toán Fast Separable Box Blur O(1) đa luồng cực nhanh
        /// </summary>
        private static byte[] FastBlur(byte[] src, int width, int height, int stride, int radius)
        {
            if (radius < 1) radius = 1;
            byte[] temp = new byte[src.Length];
            byte[] result = new byte[src.Length];

            int div = radius * 2 + 1;

            // Quét ngang (Horizontal Pass)
            Parallel.For(0, height, y =>
            {
                int rowOffset = y * stride;
                int sumB = 0, sumG = 0, sumR = 0;

                for (int i = -radius; i <= radius; i++)
                {
                    int cx = Math.Clamp(i, 0, width - 1) * 4;
                    sumB += src[rowOffset + cx];
                    sumG += src[rowOffset + cx + 1];
                    sumR += src[rowOffset + cx + 2];
                }

                for (int x = 0; x < width; x++)
                {
                    int outIdx = rowOffset + (x * 4);
                    temp[outIdx] = (byte)(sumB / div);
                    temp[outIdx + 1] = (byte)(sumG / div);
                    temp[outIdx + 2] = (byte)(sumR / div);
                    temp[outIdx + 3] = src[outIdx + 3];

                    int leftX = Math.Clamp(x - radius, 0, width - 1) * 4;
                    int rightX = Math.Clamp(x + radius + 1, 0, width - 1) * 4;

                    sumB += src[rowOffset + rightX] - src[rowOffset + leftX];
                    sumG += src[rowOffset + rightX + 1] - src[rowOffset + leftX + 1];
                    sumR += src[rowOffset + rightX + 2] - src[rowOffset + leftX + 2];
                }
            });

            // Quét dọc (Vertical Pass)
            Parallel.For(0, width, x =>
            {
                int px = x * 4;
                int sumB = 0, sumG = 0, sumR = 0;

                for (int i = -radius; i <= radius; i++)
                {
                    int cy = Math.Clamp(i, 0, height - 1);
                    int offset = (cy * stride) + px;
                    sumB += temp[offset];
                    sumG += temp[offset + 1];
                    sumR += temp[offset + 2];
                }

                for (int y = 0; y < height; y++)
                {
                    int outIdx = (y * stride) + px;
                    result[outIdx] = (byte)(sumB / div);
                    result[outIdx + 1] = (byte)(sumG / div);
                    result[outIdx + 2] = (byte)(sumR / div);
                    result[outIdx + 3] = temp[outIdx + 3];

                    int topY = Math.Clamp(y - radius, 0, height - 1);
                    int botY = Math.Clamp(y + radius + 1, 0, height - 1);

                    int topOffset = (topY * stride) + px;
                    int botOffset = (botY * stride) + px;

                    sumB += temp[botOffset] - temp[topOffset];
                    sumG += temp[botOffset + 1] - temp[topOffset + 1];
                    sumR += temp[botOffset + 2] - temp[topOffset + 2];
                }
            });

            return result;
        }

        private static BitmapSource LoadBitmap(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            if (frame.Format == PixelFormats.Bgra32)
            {
                return frame;
            }

            return new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        }

        private static void SaveBitmap(BitmapSource bitmap, string outputPath)
        {
            string ext = Path.GetExtension(outputPath).ToLowerInvariant();
            BitmapEncoder encoder = ext switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 98 },
                ".bmp" => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder() // Mặc định PNG lossless
            };

            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(fileStream);
        }
    }
}
