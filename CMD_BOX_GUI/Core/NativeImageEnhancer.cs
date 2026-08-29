using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CMD_BOX_GUI.Core
{
    public static class NativeImageEnhancer
    {
        public class EnhanceOptions
        {
            public float Amount { get; set; } = 0.95f;
            public int Radius { get; set; } = 2;
            public float Threshold { get; set; } = 3.0f;
            public float EdgeSensitivity { get; set; } = 1.0f;
            public float Contrast { get; set; } = 1.04f;
            public float Vibrance { get; set; } = 0.05f;
            public int ScalePercent { get; set; } = 100;
        }

        public static EnhanceOptions GetPreset(int level)
        {
            return level switch
            {
                0 => new EnhanceOptions { Amount = 0.55f, Radius = 1, Threshold = 4.0f, EdgeSensitivity = 0.8f, Contrast = 1.02f, Vibrance = 0.03f, ScalePercent = 100 },
                2 => new EnhanceOptions { Amount = 1.40f, Radius = 2, Threshold = 2.5f, EdgeSensitivity = 1.2f, Contrast = 1.07f, Vibrance = 0.08f, ScalePercent = 100 },
                3 => new EnhanceOptions { Amount = 1.90f, Radius = 3, Threshold = 2.0f, EdgeSensitivity = 1.4f, Contrast = 1.10f, Vibrance = 0.10f, ScalePercent = 100 },
                _ => new EnhanceOptions { Amount = 0.95f, Radius = 2, Threshold = 3.0f, EdgeSensitivity = 1.0f, Contrast = 1.04f, Vibrance = 0.05f, ScalePercent = 100 }
            };
        }

        public static async Task<bool> EnhanceImageAsync(string inputPath, string outputPath, int enhanceLevel = 1)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var options = GetPreset(enhanceLevel);
                    BitmapSource sourceBitmap = LoadBitmap(inputPath);

                    if (options.ScalePercent > 100)
                    {
                        double scale = options.ScalePercent / 100.0;
                        sourceBitmap = new TransformedBitmap(sourceBitmap, new ScaleTransform(scale, scale));
                    }

                    int width = sourceBitmap.PixelWidth;
                    int height = sourceBitmap.PixelHeight;
                    int stride = width * 4;

                    byte[] srcPixels = new byte[height * stride];
                    sourceBitmap.CopyPixels(srcPixels, stride, 0);

                    byte[] dstPixels = new byte[height * stride];
                    ProcessSharpenAndEnhance(srcPixels, dstPixels, width, height, stride, options);

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
            byte[] blurred = FastGaussianBlur3Pass(src, width, height, stride, opts.Radius);

            float amount = opts.Amount;
            float thresholdSq = opts.Threshold * opts.Threshold;
            float contrast = opts.Contrast;
            float vibrance = opts.Vibrance;
            float edgeSens = opts.EdgeSensitivity;

            Parallel.For(0, height, y =>
            {
                int rowOffset = y * stride;
                int prevRow = Math.Max(0, y - 1) * stride;
                int nextRow = Math.Min(height - 1, y + 1) * stride;

                for (int x = 0; x < width; x++)
                {
                    int px = rowOffset + (x * 4);
                    int prevX = Math.Max(0, x - 1) * 4;
                    int nextX = Math.Min(width - 1, x + 1) * 4;

                    int b = src[px];
                    int g = src[px + 1];
                    int r = src[px + 2];
                    byte a = src[px + 3];

                    int blurB = blurred[px];
                    int blurG = blurred[px + 1];
                    int blurR = blurred[px + 2];

                    int minB = b, maxB = b;
                    int minG = g, maxG = g;
                    int minR = r, maxR = r;

                    int bLeft = src[rowOffset + prevX], gLeft = src[rowOffset + prevX + 1], rLeft = src[rowOffset + prevX + 2];
                    int bRight = src[rowOffset + nextX], gRight = src[rowOffset + nextX + 1], rRight = src[rowOffset + nextX + 2];
                    int bTop = src[prevRow + (x * 4)], gTop = src[prevRow + (x * 4) + 1], rTop = src[prevRow + (x * 4) + 2];
                    int bBottom = src[nextRow + (x * 4)], gBottom = src[nextRow + (x * 4) + 1], rBottom = src[nextRow + (x * 4) + 2];

                    UpdateMinMax(ref minB, ref maxB, bLeft);
                    UpdateMinMax(ref minB, ref maxB, bRight);
                    UpdateMinMax(ref minB, ref maxB, bTop);
                    UpdateMinMax(ref minB, ref maxB, bBottom);

                    UpdateMinMax(ref minG, ref maxG, gLeft);
                    UpdateMinMax(ref minG, ref maxG, gRight);
                    UpdateMinMax(ref minG, ref maxG, gTop);
                    UpdateMinMax(ref minG, ref maxG, gBottom);

                    UpdateMinMax(ref minR, ref maxR, rLeft);
                    UpdateMinMax(ref minR, ref maxR, rRight);
                    UpdateMinMax(ref minR, ref maxR, rTop);
                    UpdateMinMax(ref minR, ref maxR, rBottom);

                    float lumaLeft = 0.299f * rLeft + 0.587f * gLeft + 0.114f * bLeft;
                    float lumaRight = 0.299f * rRight + 0.587f * gRight + 0.114f * bRight;
                    float lumaTop = 0.299f * rTop + 0.587f * gTop + 0.114f * bTop;
                    float lumaBottom = 0.299f * rBottom + 0.587f * gBottom + 0.114f * bBottom;

                    float grad = Math.Abs(lumaRight - lumaLeft) + Math.Abs(lumaBottom - lumaTop);
                    float edgeWeight = Math.Clamp((grad / 18.0f) * edgeSens, 0.25f, 1.25f);

                    float diffB = b - blurB;
                    float diffG = g - blurG;
                    float diffR = r - blurR;

                    float wB = (diffB * diffB) / (diffB * diffB + thresholdSq);
                    float wG = (diffG * diffG) / (diffG * diffG + thresholdSq);
                    float wR = (diffR * diffR) / (diffR * diffR + thresholdSq);

                    float sharpB = b + diffB * amount * wB * edgeWeight;
                    float sharpG = g + diffG * amount * wG * edgeWeight;
                    float sharpR = r + diffR * amount * wR * edgeWeight;

                    float overshootB = (maxB - minB) * 0.18f + 2.0f;
                    float overshootG = (maxG - minG) * 0.18f + 2.0f;
                    float overshootR = (maxR - minR) * 0.18f + 2.0f;

                    sharpB = Math.Clamp(sharpB, minB - overshootB, maxB + overshootB);
                    sharpG = Math.Clamp(sharpG, minG - overshootG, maxG + overshootG);
                    sharpR = Math.Clamp(sharpR, minR - overshootR, maxR + overshootR);

                    sharpB = ApplySmoothSCurve(sharpB, contrast);
                    sharpG = ApplySmoothSCurve(sharpG, contrast);
                    sharpR = ApplySmoothSCurve(sharpR, contrast);

                    if (vibrance > 0.001f)
                    {
                        float maxVal = Math.Max(sharpR, Math.Max(sharpG, sharpB));
                        float minVal = Math.Min(sharpR, Math.Min(sharpG, sharpB));
                        float sat = (maxVal - minVal) / (maxVal + 0.001f);
                        float boost = (1.0f - sat) * vibrance;

                        float gray = 0.299f * sharpR + 0.587f * sharpG + 0.114f * sharpB;
                        sharpR += (sharpR - gray) * boost;
                        sharpG += (sharpG - gray) * boost;
                        sharpB += (sharpB - gray) * boost;
                    }

                    dst[px] = (byte)Math.Clamp((int)Math.Round(sharpB), 0, 255);
                    dst[px + 1] = (byte)Math.Clamp((int)Math.Round(sharpG), 0, 255);
                    dst[px + 2] = (byte)Math.Clamp((int)Math.Round(sharpR), 0, 255);
                    dst[px + 3] = a;
                }
            });
        }

        private static void UpdateMinMax(ref int min, ref int max, int val)
        {
            if (val < min) min = val;
            if (val > max) max = val;
        }

        private static float ApplySmoothSCurve(float val, float contrast)
        {
            if (Math.Abs(contrast - 1.0f) < 0.001f) return val;
            float norm = Math.Clamp(val / 255.0f, 0.0f, 1.0f);
            float s = norm + (contrast - 1.0f) * 1.8f * norm * (1.0f - norm) * (norm - 0.5f);
            return Math.Clamp(s * 255.0f, 0.0f, 255.0f);
        }

        private static byte[] FastGaussianBlur3Pass(byte[] src, int width, int height, int stride, int radius)
        {
            if (radius < 1) radius = 1;
            byte[] pass1 = FastBoxBlur(src, width, height, stride, radius);
            return radius <= 1 ? pass1 : FastBoxBlur(pass1, width, height, stride, radius);
        }

        private static byte[] FastBoxBlur(byte[] src, int width, int height, int stride, int radius)
        {
            if (radius < 1) radius = 1;
            byte[] temp = new byte[src.Length];
            byte[] result = new byte[src.Length];
            int div = radius * 2 + 1;

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
            return frame.Format == PixelFormats.Bgra32 ? frame : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        }

        private static void SaveBitmap(BitmapSource bitmap, string outputPath)
        {
            string ext = Path.GetExtension(outputPath).ToLowerInvariant();
            BitmapEncoder encoder = ext switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 98 },
                ".bmp" => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };

            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(fileStream);
        }
    }
}
