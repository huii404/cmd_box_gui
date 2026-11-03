using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using CMD_BOX_GUI.Core;
using CMD_BOX_GUI.Models;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CMD_BOX_GUI.Services
{
    public class DocumentService
    {
        private static readonly Lazy<DocumentService> _instance = new(() => new DocumentService());
        public static DocumentService Instance => _instance.Value;

        // =========================================================================
        // 1. OCR (NHẬN DIỆN VĂN BẢN TỪ ẢNH QUA WINDOWS NATIVE OCR ENGINE)
        // =========================================================================

        /// <summary>
        /// Lấy danh sách các ngôn ngữ nhận dạng OCR khả dụng trên Windows của máy
        /// </summary>
        public List<OcrLanguageOption> GetAvailableOcrLanguages()
        {
            var list = new List<OcrLanguageOption>
            {
                new OcrLanguageOption { Code = "Auto", DisplayName = "🌐 Tự Động (Theo Windows User Profile)" }
            };

            try
            {
                var supported = OcrEngine.AvailableRecognizerLanguages;
                foreach (var lang in supported)
                {
                    string name = lang.DisplayName;
                    if (string.IsNullOrWhiteSpace(name)) name = lang.NativeName;
                    list.Add(new OcrLanguageOption
                    {
                        Code = lang.LanguageTag,
                        DisplayName = $"🔤 {name} ({lang.LanguageTag})"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[OCR] Không thể truy vấn danh sách ngôn ngữ: {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// Trích xuất toàn bộ văn bản từ 1 tệp hình ảnh bằng Windows Media OCR Engine
        /// </summary>
        public async Task<string> ExtractTextFromImageAsync(string imagePath, string? languageCode = null)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("Tệp hình ảnh không tồn tại.", imagePath);

            string fullPath = Path.GetFullPath(imagePath);

            // Đọc bytes trực tiếp qua .NET I/O để hỗ trợ 100% đường dẫn tiếng Việt / Telex / khoảng trắng / Unicode
            byte[] imageBytes = await File.ReadAllBytesAsync(fullPath);
            using var memStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(memStream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync();
            }
            memStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(memStream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            OcrEngine? engine = null;
            if (!string.IsNullOrWhiteSpace(languageCode) && !string.Equals(languageCode, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                var targetLang = new Windows.Globalization.Language(languageCode);
                if (OcrEngine.IsLanguageSupported(targetLang))
                {
                    engine = OcrEngine.TryCreateFromLanguage(targetLang);
                }
            }

            engine ??= OcrEngine.TryCreateFromUserProfileLanguages();

            if (engine == null)
            {
                var available = OcrEngine.AvailableRecognizerLanguages;
                if (available.Count > 0)
                {
                    engine = OcrEngine.TryCreateFromLanguage(available[0]);
                }
            }

            if (engine == null)
            {
                throw new InvalidOperationException("Hệ thống Windows chưa cài đặt gói nhận diện ký tự OCR nào. Vui lòng cài gói ngôn ngữ trong Settings > Time & Language.");
            }

            var ocrResult = await engine.RecognizeAsync(softwareBitmap);
            if (ocrResult == null || ocrResult.Lines == null || ocrResult.Lines.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var line in ocrResult.Lines)
            {
                sb.AppendLine(line.Text);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Thực hiện OCR hàng loạt danh sách ảnh, tùy chọn ghi ra file .txt hoặc gộp kết quả
        /// </summary>
        public async Task<List<DocumentBatchItem>> BatchOcrImagesAsync(
            IEnumerable<DocumentBatchItem> items,
            string? languageCode,
            string? outputFolder,
            bool exportTxtFiles,
            IProgress<(int current, int total, string fileName)>? progress = null)
        {
            var list = items.ToList();
            int total = list.Count;
            int count = 0;

            foreach (var item in list)
            {
                count++;
                progress?.Report((count, total, item.FileName));
                item.Status = "Đang xử lý...";

                try
                {
                    string text = await ExtractTextFromImageAsync(item.FilePath, languageCode);
                    item.ResultText = text;

                    if (exportTxtFiles)
                    {
                        string outDir = string.IsNullOrWhiteSpace(outputFolder)
                            ? Path.GetDirectoryName(item.FilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                            : outputFolder;

                        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                        string txtName = Path.GetFileNameWithoutExtension(item.FilePath) + "_ocr.txt";
                        string outPath = Path.Combine(outDir, txtName);
                        await File.WriteAllTextAsync(outPath, text, Encoding.UTF8);
                        item.OutputPath = outPath;
                    }

                    item.Status = "Thành công";
                }
                catch (Exception ex)
                {
                    item.Status = "Lỗi";
                    item.ResultText = $"Lỗi OCR: {ex.Message}";
                    Logger.Error($"[OCR Batch] Lỗi xử lý {item.FileName}: {ex.Message}");
                }
            }

            return list;
        }

        // =========================================================================
        // 2. TRÍCH XUẤT VĂN BẢN TỪ FILE DOCX & GỘP FILE (AI CONTEXT PACKER)
        // =========================================================================

        /// <summary>
        /// Trích xuất toàn bộ text từ tài liệu Word (.docx) thuần C#, không phụ thuộc MS Office
        /// </summary>
        public async Task<string> ExtractTextFromDocxAsync(string docxPath)
        {
            if (!File.Exists(docxPath)) throw new FileNotFoundException("Tệp DOCX không tồn tại.", docxPath);

            return await Task.Run(() =>
            {
                using var fs = new FileStream(docxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null) return string.Empty;

                using var stream = entry.Open();
                var doc = XDocument.Load(stream);

                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var sb = new StringBuilder();

                foreach (var p in doc.Descendants(w + "p"))
                {
                    var texts = p.Descendants(w + "t").Select(t => t.Value);
                    string paragraphText = string.Concat(texts);
                    if (!string.IsNullOrWhiteSpace(paragraphText))
                    {
                        sb.AppendLine(paragraphText);
                    }
                }

                return sb.ToString().TrimEnd();
            });
        }

        /// <summary>
        /// Gộp nội dung nhiều file text/code/tài liệu thành một tệp đóng gói duy nhất cho AI Prompt
        /// </summary>
        public async Task<string> ConsolidateFilesForAiPromptAsync(IEnumerable<string> filePaths)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# CONSOLIDATED PROJECT CONTEXT DUMP");
            sb.AppendLine($"# Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            foreach (var path in filePaths)
            {
                if (!File.Exists(path)) continue;
                string relName = Path.GetFileName(path);
                sb.AppendLine($"// ===================================================================");
                sb.AppendLine($"// FILE: {relName} ({path})");
                sb.AppendLine($"// ===================================================================");

                try
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    string content;
                    if (ext == ".docx")
                    {
                        content = await ExtractTextFromDocxAsync(path);
                    }
                    else
                    {
                        content = await File.ReadAllTextAsync(path);
                    }
                    sb.AppendLine(content);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[Lỗi khi đọc file: {ex.Message}]");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        // =========================================================================
        // 3. CHUYỂN ĐỔI BẢNG MÃ TIẾNG VIỆT (TCVN3 / VNI-WINDOWS -> UNICODE)
        // =========================================================================

        private static readonly Dictionary<char, char> Tcvn3Map = new()
        {
            { 'µ', 'à' }, { '¸', 'á' }, { '¶', 'ả' }, { '·', 'ã' }, { '¹', 'ạ' },
            { '¨', 'ă' }, { '»', 'ằ' }, { '¾', 'ắ' }, { '¼', 'ẳ' }, { '½', 'ẵ' }, { 'Æ', 'ặ' },
            { '©', 'â' }, { 'Ç', 'ầ' }, { 'Ê', 'ấ' }, { 'È', 'ẩ' }, { 'É', 'ẫ' }, { 'Ë', 'ậ' },
            { 'Ì', 'è' }, { 'Ð', 'é' }, { 'Î', 'ẻ' }, { 'Ï', 'ẽ' }, { 'Ñ', 'ẹ' },
            { 'ª', 'ê' }, { 'Ò', 'ề' }, { 'Õ', 'ế' }, { 'Ó', 'ể' }, { 'Ô', 'ễ' }, { 'Ö', 'ệ' },
            { '×', 'ì' }, { 'Ø', 'í' }, { 'Ù', 'ỉ' }, { 'Ü', 'ĩ' }, { 'Þ', 'ị' },
            { 'ß', 'ò' }, { 'á', 'ó' }, { 'à', 'ỏ' }, { 'ã', 'õ' }, { 'ä', 'ọ' },
            { '«', 'ô' }, { 'å', 'ồ' }, { 'è', 'ố' }, { 'æ', 'ổ' }, { 'ç', 'ỗ' }, { 'é', 'ộ' },
            { '¬', 'ơ' }, { 'ê', 'ờ' }, { 'í', 'ớ' }, { 'ë', 'ở' }, { 'ì', 'ỡ' }, { 'î', 'ợ' },
            { 'ï', 'ù' }, { 'ó', 'ú' }, { 'ñ', 'ủ' }, { 'ò', 'ũ' }, { 'ô', 'ụ' },
            { '­', 'ư' }, { 'õ', 'ừ' }, { 'ø', 'ứ' }, { 'ö', 'ử' }, { '÷', 'ữ' }, { 'ù', 'ự' },
            { 'ú', 'ỳ' }, { 'ý', 'ý' }, { 'û', 'ỷ' }, { 'ü', 'ỹ' }, { 'þ', 'ỵ' },
            { '®', 'đ' },
            // Chữ hoa đặc biệt TCVN3
            { '¡', 'Ă' }, { '¢', 'Â' }, { '§', 'Đ' }, { '£', 'Ê' }, { '¤', 'Ô' }, { '¥', 'Ơ' }, { '¦', 'Ư' }
        };

        /// <summary>
        /// Chuyển đổi mã TCVN3 (ABC / font .VnTimes, .VnArial) sang Unicode chuẩn
        /// </summary>
        public string ConvertTcvn3ToUnicode(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (Tcvn3Map.TryGetValue(c, out char mapped))
                    sb.Append(mapped);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static readonly (string From, string To)[] VniRules = new[]
        {
            ("aø", "à"), ("aù", "á"), ("aû", "ả"), ("aõ", "ã"), ("aï", "ạ"),
            ("aê", "ă"), ("aè", "ằ"), ("aé", "ắ"), ("aú", "ẳ"), ("aü", "ẵ"), ("aë", "ặ"),
            ("aâ", "â"), ("aà", "ầ"), ("aá", "ấ"), ("aå", "ẩ"), ("aã", "ẫ"), ("aä", "ậ"),
            ("eè", "è"), ("eù", "é"), ("eû", "ẻ"), ("eõ", "ẽ"), ("eï", "ẹ"),
            ("eâ", "ê"), ("eà", "ề"), ("eá", "ế"), ("eå", "ể"), ("eã", "ễ"), ("eä", "ệ"),
            ("iè", "ì"), ("í", "í"), ("iû", "ỉ"), ("ó", "ĩ"), ("ò", "ị"),
            ("oø", "ò"), ("où", "ó"), ("oû", "ỏ"), ("oõ", "õ"), ("oï", "ọ"),
            ("oâ", "ô"), ("oà", "ồ"), ("oá", "ố"), ("oå", "ổ"), ("oã", "ỗ"), ("oä", "ộ"),
            ("ô", "ơ"), ("ôø", "ờ"), ("ôù", "ớ"), ("ôû", "ở"), ("ôõ", "ỡ"), ("ôï", "ợ"),
            ("uø", "ù"), ("uù", "ú"), ("uû", "ủ"), ("uõ", "ũ"), ("uï", "ụ"),
            ("ö", "ư"), ("öø", "ừ"), ("öù", "ứ"), ("öû", "ử"), ("öõ", "ữ"), ("öï", "ự"),
            ("yø", "ỳ"), ("yù", "ý"), ("yû", "ỷ"), ("yõ", "ỹ"), ("î", "ỵ"),
            ("ñ", "đ"), ("Ñ", "Đ")
        };

        /// <summary>
        /// Chuyển đổi mã VNI-Windows sang Unicode
        /// </summary>
        public string ConvertVniToUnicode(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string result = input;
            foreach (var (from, to) in VniRules)
            {
                result = result.Replace(from, to, StringComparison.OrdinalIgnoreCase);
            }
            return result;
        }

        /// <summary>
        /// Loại bỏ hoàn toàn dấu Tiếng Việt (chuyển sang không dấu)
        /// </summary>
        public string RemoveVietnameseAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC)
                     .Replace('đ', 'd')
                     .Replace('Đ', 'D');
        }

        // =========================================================================
        // 4. TIỆN ÍCH BIẾN ĐỔI CHỮ (CASE CONVERTER)
        // =========================================================================

        public string ConvertCase(string text, string mode)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return mode switch
            {
                "UPPER" => text.ToUpperInvariant(),
                "lower" => text.ToLowerInvariant(),
                "Title" => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower()),
                "camelCase" => ToCamelCase(text),
                "snake_case" => ToSnakeCase(text),
                "kebab-case" => ToKebabCase(text),
                _ => text
            };
        }

        private string ToCamelCase(string s)
        {
            var words = Regex.Split(s, @"[\s_\-]+").Where(w => !string.IsNullOrEmpty(w)).ToArray();
            if (words.Length == 0) return string.Empty;
            var sb = new StringBuilder(words[0].ToLowerInvariant());
            for (int i = 1; i < words.Length; i++)
            {
                string w = words[i];
                sb.Append(char.ToUpperInvariant(w[0])).Append(w.Substring(1).ToLowerInvariant());
            }
            return sb.ToString();
        }

        private string ToSnakeCase(string s)
        {
            var words = Regex.Split(s, @"[\s_\-]+").Where(w => !string.IsNullOrEmpty(w));
            return string.Join("_", words.Select(w => w.ToLowerInvariant()));
        }

        private string ToKebabCase(string s)
        {
            var words = Regex.Split(s, @"[\s_\-]+").Where(w => !string.IsNullOrEmpty(w));
            return string.Join("-", words.Select(w => w.ToLowerInvariant()));
        }

        // =========================================================================
        // 5. LÀM SẠCH VĂN BẢN (DATA CLEANER)
        // =========================================================================

        public string CleanTextData(
            string text,
            bool removeBlankLines,
            bool trimLines,
            bool deduplicate,
            bool sortLines,
            bool sortDescending)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).AsEnumerable();

            if (trimLines)
            {
                lines = lines.Select(l => l.Trim());
            }

            if (removeBlankLines)
            {
                lines = lines.Where(l => !string.IsNullOrWhiteSpace(l));
            }

            if (deduplicate)
            {
                lines = lines.Distinct();
            }

            if (sortLines)
            {
                lines = sortDescending
                    ? lines.OrderByDescending(l => l, StringComparer.CurrentCultureIgnoreCase)
                    : lines.OrderBy(l => l, StringComparer.CurrentCultureIgnoreCase);
            }

            return string.Join(Environment.NewLine, lines);
        }

        // =========================================================================
        // 6. BỘ TRÍCH XUẤT DỮ LIỆU THÔNG MINH (SMART DATA EXTRACTOR)
        // =========================================================================

        public List<string> ExtractEmails(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new();
            var matches = Regex.Matches(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            return matches.Select(m => m.Value).Distinct().OrderBy(x => x).ToList();
        }

        public List<string> ExtractUrls(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new();
            var matches = Regex.Matches(text, @"https?://[^\s/$.?#].[^\s]*");
            return matches.Select(m => m.Value.TrimEnd('.', ',', ')', '\"', '\'')).Distinct().OrderBy(x => x).ToList();
        }

        public List<string> ExtractPhoneNumbers(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new();
            var matches = Regex.Matches(text, @"(?:\+?84|0)(?:[1-9]\d{8}|[2-9]\d{9})");
            return matches.Select(m => m.Value).Distinct().OrderBy(x => x).ToList();
        }

        public List<string> ExtractIpAddresses(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new();
            var matches = Regex.Matches(text, @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b");
            return matches.Select(m => m.Value).Distinct().OrderBy(x => x).ToList();
        }

        // =========================================================================
        // 7. MÃ HÓA, GIẢI MÃ & KIỂM TRA TÍNH TOÀN VẸN (HASH CHECKSUM)
        // =========================================================================

        public string Base64EncodeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }

        public string Base64DecodeText(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return string.Empty;
            byte[] bytes = Convert.FromBase64String(base64.Trim());
            return Encoding.UTF8.GetString(bytes);
        }

        public string UrlEncode(string text) => Uri.EscapeDataString(text ?? string.Empty);
        public string UrlDecode(string text) => Uri.UnescapeDataString(text ?? string.Empty);

        public async Task<string> ComputeFileChecksumAsync(string filePath, string algorithm = "SHA256")
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Tệp không tồn tại.", filePath);

            using var stream = File.OpenRead(filePath);
            using HashAlgorithm hasher = algorithm.ToUpperInvariant() switch
            {
                "MD5" => MD5.Create(),
                "SHA1" => SHA1.Create(),
                "SHA512" => SHA512.Create(),
                _ => SHA256.Create()
            };

            byte[] hashBytes = await hasher.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        // =========================================================================
        // 8. ĐÓNG GÓI DANH SÁCH ẢNH THÀNH FILE PDF CHUẨN (IMAGES TO PDF)
        // =========================================================================

        /// <summary>
        /// Tạo file PDF chứa danh sách các ảnh chuẩn mà không phụ thuộc thư viện ngoài (Pure PDF 1.4)
        /// </summary>
        public async Task<bool> ConvertImagesToPdfAsync(IEnumerable<string> imagePaths, string outputPdfPath)
        {
            var images = imagePaths.Where(File.Exists).ToList();
            if (images.Count == 0) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // Chuyển đổi mọi định dạng ảnh sang chuẩn JPEG stream và đóng gói PDF
                    using var fs = new FileStream(outputPdfPath, FileMode.Create, FileAccess.Write);
                    using var writer = new StreamWriter(fs, Encoding.ASCII);

                    var objectOffsets = new List<long>();
                    long currentOffset = 0;

                    void WriteAscii(string text)
                    {
                        byte[] bytes = Encoding.ASCII.GetBytes(text);
                        fs.Write(bytes, 0, bytes.Length);
                        currentOffset += bytes.Length;
                    }

                    void MarkObject()
                    {
                        objectOffsets.Add(currentOffset);
                    }

                    // 1. PDF Header
                    WriteAscii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

                    int pageCount = images.Count;
                    int catalogObjNum = 1;
                    int pagesObjNum = 2;
                    // Mỗi trang tốn 3 objects: Page, Content Stream, Image XObject
                    // Page: 3 + i*3
                    // Content: 3 + i*3 + 1
                    // Image: 3 + i*3 + 2

                    // 2. Catalog
                    MarkObject();
                    WriteAscii($"{catalogObjNum} 0 obj\n<< /Type /Catalog /Pages {pagesObjNum} 0 R >>\nendobj\n");

                    // 3. Pages Node
                    var pageObjRefs = Enumerable.Range(0, pageCount).Select(i => $"{3 + i * 3} 0 R");
                    MarkObject();
                    WriteAscii($"{pagesObjNum} 0 obj\n<< /Type /Pages /Count {pageCount} /Kids [ {string.Join(" ", pageObjRefs)} ] >>\nendobj\n");

                    for (int i = 0; i < pageCount; i++)
                    {
                        string imgPath = images[i];
                        int pageNum = 3 + i * 3;
                        int contentNum = pageNum + 1;
                        int imageNum = pageNum + 2;

                        // Chuyển ảnh sang JPEG memory stream
                        using var imgStream = new MemoryStream();
                        int imgWidth = 800;
                        int imgHeight = 600;

                        byte[] imgFileBytes = File.ReadAllBytes(imgPath);
                        using (var imgMem = new MemoryStream(imgFileBytes))
                        using (var srcBmp = new System.Drawing.Bitmap(imgMem))
                        {
                            imgWidth = srcBmp.Width;
                            imgHeight = srcBmp.Height;
                            srcBmp.Save(imgStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                        byte[] jpegBytes = imgStream.ToArray();

                        // Trang PDF có kích thước chuẩn theo ảnh (point size: 1 pt = 1/72 inch)
                        double ptWidth = imgWidth * 72.0 / 96.0;
                        double ptHeight = imgHeight * 72.0 / 96.0;

                        // 4. Page Object
                        MarkObject();
                        WriteAscii($"{pageNum} 0 obj\n<< /Type /Page /Parent {pagesObjNum} 0 R /MediaBox [ 0 0 {ptWidth:0.##} {ptHeight:0.##} ] /Contents {contentNum} 0 R /Resources << /XObject << /Im1 {imageNum} 0 R >> >> >>\nendobj\n");

                        // 5. Content Stream
                        string content = $"q {ptWidth:0.##} 0 0 {ptHeight:0.##} 0 0 cm /Im1 Do Q";
                        byte[] contentBytes = Encoding.ASCII.GetBytes(content);
                        MarkObject();
                        WriteAscii($"{contentNum} 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
                        fs.Write(contentBytes, 0, contentBytes.Length);
                        currentOffset += contentBytes.Length;
                        WriteAscii("\nendstream\nendobj\n");

                        // 6. Image XObject
                        MarkObject();
                        WriteAscii($"{imageNum} 0 obj\n<< /Type /XObject /Subtype /Image /Width {imgWidth} /Height {imgHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpegBytes.Length} >>\nstream\n");
                        fs.Write(jpegBytes, 0, jpegBytes.Length);
                        currentOffset += jpegBytes.Length;
                        WriteAscii("\nendstream\nendobj\n");
                    }

                    // 7. XRef Table
                    long xrefOffset = currentOffset;
                    int totalObjects = 2 + pageCount * 3 + 1; // bao gồm obj 0
                    WriteAscii($"xref\n0 {totalObjects}\n0000000000 65535 f \n");
                    foreach (var offset in objectOffsets)
                    {
                        WriteAscii($"{offset:D10} 00000 n \n");
                    }

                    // 8. Trailer
                    WriteAscii($"trailer\n<< /Size {totalObjects} /Root {catalogObjNum} 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ImagesToPdf] Lỗi tạo PDF: {ex.Message}");
                    return false;
                }
            });
        }
    }
}
