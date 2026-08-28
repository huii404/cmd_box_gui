using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMD_BOX_GUI.Services
{
    // =========================================================================
    // 🧠 LOCAL AI KNOWLEDGE & LOGIC ENGINE (1 FILE DUY NHẤT TRAIN DATA & LOGIC)
    // =========================================================================
    public class ChatbotService
    {
        // ---------------------------------------------------------------------
        // 1. DATA MODEL CỦA TỪNG MỤC KIẾN THỨC HUẤN LUYỆN
        // ---------------------------------------------------------------------
        public class AiKnowledge
        {
            public string Category { get; set; } = string.Empty;
            public string[] Keywords { get; set; } = Array.Empty<string>();
            public string Answer { get; set; } = string.Empty;

            public AiKnowledge(string category, string[] keywords, string answer)
            {
                Category = category;
                Keywords = keywords;
                Answer = answer;
            }
        }

        // ---------------------------------------------------------------------
        // 2. BỘ DỮ LIỆU HUẤN LUYỆN (TRAINING DATASET)
        //    (Dễ dàng thêm/sửa/xóa các mục hỏi đáp tại đây)
        // ---------------------------------------------------------------------
        private static readonly List<AiKnowledge> Dataset = new()
        {
            // === [A] CHÀO HỎI & GIỚI THIỆU ===
            new AiKnowledge(
                "Chung",
                new[] { "chao", "hello", "hi", "hey", "alo", "ban la ai", "tro ly", "gioi thieu", "ai tao ra", "cmd box" },
                "👋 **Xin chào! Tôi là Trợ Lý AI Local của CMD BOX GUI.**\n\n" +
                "Tôi hoạt động hoàn toàn **Offline nội bộ** trên máy tính của bạn (không cần Internet / không tốn API key).\n\n" +
                "Tôi được huấn luyện để giải đáp và hướng dẫn bạn mọi tác vụ trong hệ thống:\n" +
                "• 🧹 **Tối ưu Windows:** Dọn rác nhanh/PRO, tắt app khởi động, tắt service ngầm, sửa Win Update.\n" +
                "• 🌐 **Mạng & Wi-Fi:** Xem lại mật khẩu Wi-Fi đã lưu, khôi phục mạng 8 bước, bật khiên bảo mật.\n" +
                "• 🎬 **Xử lý Media:** Nén video CRF H.264, xuất MP3, làm ảnh GIF, giấu file bí mật (Stego).\n" +
                "• ⚡ **Tiện ích:** Auto Clicker, Auto Paste/Spam, chẩn đoán độ chai pin laptop.\n" +
                "• ⌨️ **Phím tắt:** Sử dụng phím `ESC` / `F6` để ngắt khẩn cấp mọi tiến trình.\n\n" +
                "💬 Bạn cần hỗ trợ câu hỏi hay vấn đề gì hôm nay?"
            ),

            // === [B] TỐI ƯU HÓA & DỌN RÁC WINDOWS ===
            new AiKnowledge(
                "Tối Ưu",
                new[] { "don rac", "xoa rac", "clean", "temp", "prefetch", "wer", "directx", "o c day", "thung rac" },
                "🧹 **HƯỚNG DẪN DỌN RÁC WINDOWS HIỆU QUẢ:**\n\n" +
                "1. **Dọn rác nhanh (Quick Clean):** Chuyển sang tab **`🧹 Tối Ưu & Dọn Dẹp`** ➔ Chọn *Dọn rác nhanh*. Hệ thống tự xóa sạch User Temp, Windows Temp, Recent Files, DirectX Cache và dọn sạch Thùng rác.\n" +
                "2. **Dọn rác chuyên sâu PRO:** Chạy *Dọn rác PRO* để xóa sâu Prefetch, CBS Logs, Delivery Optimization và dọn kho lưu trữ WinSxS qua DISM Component Cleanup.\n" +
                "3. **Tắt Hibernate (Ngủ đông):** Bấm *Tắt Hibernate* để giải phóng ngay 8GB - 32GB dung lượng tệp `hiberfil.sys` trên ổ C!\n" +
                "4. **Dọn Cache Dev:** Dành cho lập trình viên để dọn dẹp các thư mục cache nặng của NPM, Pip, NuGet, Cargo."
            ),

            new AiKnowledge(
                "Tối Ưu",
                new[] { "may cham", "lag", "giat", "full disk", "tang toc", "toi uu he thong", "tre chuot" },
                "⚡ **HƯỚNG DẪN TĂNG TỐC & KHẮC PHỤC MÁY CHẬM:**\n\n" +
                "1. **Tắt ứng dụng khởi động:** Vào tab *🧹 Tối Ưu* ➔ Chọn *Tắt App Khởi Động*. Hệ thống sẽ lọc tắt các app chiếm tài nguyên nhưng **tự động bảo vệ Driver GPU/Âm thanh**.\n" +
                "2. **Vô hiệu hóa Service thừa:** Chạy *Tắt Dịch Vụ Thừa* để ngắt Telemetry theo dõi ngầm, MapsBroker, Xbox Live nếu bạn không dùng.\n" +
                "3. **Tinh chỉnh Desktop PRO:** Chạy *Tinh Chỉnh Hệ Thống PRO* để đưa độ trễ phản hồi menu về `0ms` (`MenuShowDelay = 0`) và xóa bỏ giới hạn Network Throttling."
            ),

            new AiKnowledge(
                "Tối Ưu",
                new[] { "sua update", "loi update", "windows update", "update bi loi", "khong update duoc", "wuauserv" },
                "🛠️ **CÁCH SỬA LỖI WINDOWS UPDATE:**\n\n" +
                "1. Chuyển sang tab **`🧹 Tối Ưu & Dọn Dẹp`** ➔ Bấm nút **`🛠️ Sửa Lỗi Windows Update`**.\n" +
                "2. Hệ thống sẽ tự động dừng an toàn các dịch vụ `wuauserv`, `cryptSvc`, `bits`, `msiserver`.\n" +
                "3. Tự động đổi tên và làm sạch bộ nhớ đệm `SoftwareDistribution` và `catroot2` bị lỗi.\n" +
                "4. Tái khởi động lại các dịch vụ cập nhật để Windows Update tải và cài đặt bản vá bình thường."
            ),

            new AiKnowledge(
                "Tối Ưu",
                new[] { "taskbar win 11", "an icon", "copilot", "widgets", "teams", "thanh taskbar" },
                "🖥️ **TINH CHỈNH THANH TASKBAR WINDOWS 11:**\n\n" +
                "Vào tab **`🧹 Tối Ưu & Dọn Dẹp`** ➔ Bấm **`Ẩn Icon Thừa Taskbar Win 11`**.\n" +
                "Hệ thống sẽ dọn sạch giao diện thanh tác vụ bằng cách ẩn ô Search rườm rà, nút Widgets tin tức ngầm, Microsoft Teams và tắt trợ lý ảo Copilot."
            ),

            // === [C] MẠNG & BẢO MẬT WI-FI ===
            new AiKnowledge(
                "Mạng",
                new[] { "pass wifi", "mat khau wifi", "xem pass", "quen pass", "trich xuat wifi", "wifi da luu" },
                "📶 **CÁCH XEM LẠI MẬT KHẨU WI-FI ĐÃ LƯU:**\n\n" +
                "1. Vào tab **`🌐 Mạng & Bảo Mật`**.\n" +
                "2. Bấm nút **`📶 Quét & Trích Xuất Wi-Fi Đã Lưu`**.\n" +
                "3. Trợ lý sử dụng thuật toán quét song song đa luồng qua lệnh native `netsh wlan`, liệt kê ngay danh sách toàn bộ Tên Wi-Fi (SSID) cùng Mật khẩu rõ ràng (Cleartext).\n" +
                "4. Bạn có thể bấm nút **Copy** bên cạnh bất kỳ mạng nào để sao chép mật khẩu."
            ),

            new KnowledgeRuleStub(
                "Mạng",
                new[] { "mat mang", "khoi phuc mang", "rot mang", "ping cao", "lag mang", "sua mang", "dns", "winsock", "ipconfig" },
                "🌐 **QUY TRÌNH KHÔI PHỤC MẠNG PRO (8 BƯỚC):**\n\n" +
                "Vào tab **`🌐 Mạng & Bảo Mật`** ➔ Bấm **`⚡ Khôi Phục Mạng PRO`**.\n" +
                "Quy trình tự động thực thi:\n" +
                "1. `ipconfig /flushdns`: Xóa sạch bộ đệm tên miền DNS bị nghẽn.\n" +
                "2. `netsh winsock reset`: Đặt lại toàn bộ socket mạng Windows.\n" +
                "3. `netsh int ip reset`: Khởi tạo lại ngăn xếp TCP/IP.\n" +
                "4. `arp -d *`: Xóa bảng cache ARP tránh xung đột IP nội bộ.\n" +
                "5. `Release / Renew DHCP`: Yêu cầu Router cấp dải IP mới.\n" +
                "6. Khởi động lại dịch vụ `WinNAT` và mở Port 53317 cho LocalSend."
            ),

            new AiKnowledge(
                "Mạng",
                new[] { "bao mat", "la chan", "firewall", "chan port", "smb", "smbv1", "chong virus", "defender" },
                "🛡️ **KÍCH HOẠT LÁ CHẮN BẢO MẬT HỆ THỐNG:**\n\n" +
                "Vào tab **`🌐 Mạng & Bảo Mật`** ➔ Bấm **`🛡️ Kích Hoạt Lá Chắn Bảo Mật`**.\n" +
                "Hệ thống sẽ:\n" +
                "• Bật chế độ bảo vệ chống Ransomware (Controlled Folder Access).\n" +
                "• Tạo luật tường lửa đóng các cổng nguy hiểm dễ bị quét mã độc: **Port 445, 139, 135**.\n" +
                "• Bật đầy đủ hồ sơ Windows Defender Firewall cho Domain, Public và Private.\n" +
                "• Vô hiệu hóa giao thức cổ lỗ `SMBv1` (nguồn gốc lây lan của virus WannaCry)."
            ),

            // === [D] BIÊN TẬP MEDIA & STEGANOGRAPHY ===
            new AiKnowledge(
                "Media",
                new[] { "nen video", "giam dung luong", "crf", "h264", "mp4 nang", "video dung luong lon" },
                "🎬 **HƯỚNG DẪN NÉN VIDEO GIẢM DUNG LƯỢNG HÀNG LOẠT:**\n\n" +
                "1. Chuyển sang tab **`🎬 Xử Lý Media`** ➔ Thêm tệp hoặc thư mục video vào bảng.\n" +
                "2. Tại ô Tác vụ, chọn **`Nén Video (CRF H.264)`**.\n" +
                "3. Chọn hệ số nén:\n" +
                "   • **CRF 22:** Giảm ~40% dung lượng, giữ chất lượng cực nét.\n" +
                "   • **CRF 26 (Khuyên dùng):** Giảm ~60-70% dung lượng, xem mượt, hình ảnh đẹp.\n" +
                "   • **CRF 30:** Giảm tới ~85% dung lượng, thích hợp gửi qua mạng xã hội.\n" +
                "4. Bấm **`🚀 BẮT ĐẦU XỬ LÝ HÀNG LOẠT`**. (Có thể bấm Dừng lại bất kỳ lúc nào)."
            ),

            new AiKnowledge(
                "Media",
                new[] { "mp3", "tach nhac", "xuat mp3", "mp4 sang mp3", "chuyen am thanh", "audio" },
                "🎵 **HƯỚNG DẪN TÁCH ÂM THANH MP3 TỪ VIDEO:**\n\n" +
                "1. Vào tab **`🎬 Xử Lý Media`** ➔ Thêm các tệp Video vào bảng danh sách.\n" +
                "2. Chọn Tác vụ **`Trích xuất MP3 từ Video`**.\n" +
                "3. Chọn Bitrate mong muốn (128kbps, 192kbps tiêu chuẩn hoặc 320kbps Lossless HQ).\n" +
                "4. Bấm *Bắt đầu xử lý*, engine `libmp3lame` sẽ chuyển đổi tốc độ cao mà không làm giảm chất lượng âm thanh."
            ),

            new AiKnowledge(
                "Media",
                new[] { "giau file", "steganography", "an file", "giau du lieu vao anh", "ma hoa file", "stego" },
                "🕵️‍♂️ **HƯỚNG DẪN GIẤU FILE BÍ MẬT (STEGANOGRAPHY):**\n\n" +
                "1. Vào tab **`🎬 Xử Lý Media`** ➔ Chọn sub-tab **`🕵️‍♂️ Giấu File Ẩn (Steganography)`**.\n" +
                "2. **Tệp vỏ bọc:** Chọn 1 file Ảnh (.jpg, .png) hoặc Video (.mp4, .mkv).\n" +
                "3. **Tệp bí mật:** Chọn file bạn muốn giấu (file zip, tài liệu word, ảnh riêng tư...).\n" +
                "4. Bấm **`🔒 Giấu Tệp Ngay`**.\n" +
                "📌 *Cơ chế:* File xuất ra vẫn mở xem/nghe như bình thường. Người nhận chỉ cần mở CMD BOX GUI và chọn *Quét & Trích Xuất Tệp Ẩn* là lấy lại được dữ liệu gốc nguyên vẹn 100%!"
            ),

            new AiKnowledge(
                "Media",
                new[] { "lam net", "khu nhieu", "video to gif", "tao gif", "doi toc do", "cat video", "trim" },
                "✨ **CÁC CÔNG CỤ BIÊN TẬP VIDEO HÀNG LOẠT KHÁC:**\n\n" +
                "Tại tab **`🎬 Xử Lý Media`**, bạn có thể chọn thêm nhiều tác vụ mạnh mẽ:\n" +
                "• **Làm nét & Khử nhiễu:** Sử dụng bộ lọc ma trận `unsharp` và thuật toán `hqdn3d`.\n" +
                "• **Tạo ảnh GIF động:** Chuyển đoạn video sang file `.gif` mượt mà (tùy chọn FPS & độ rộng).\n" +
                "• **Đổi tốc độ phát:** Tăng tốc (1.25x, 1.5x, 2.0x) hoặc Slow-motion (0.5x, 0.75x).\n" +
                "• **Cắt video (Trim):** Cắt nhanh theo mốc thời gian Start - End mà không cần re-encode (cực nhanh).\n" +
                "• **Đổi độ phân giải:** Chuyển đổi nhanh giữa 1080p, 720p, 480p, 4K."
            ),

            // === [E] TIỆN ÍCH & TỰ ĐỘNG HÓA ===
            new AiKnowledge(
                "Tiện Ích",
                new[] { "auto click", "click chuot", "tu dong click", "spam chuot", "autoclick" },
                "🖱️ **HƯỚNG DẪN DÙNG AUTO CLICKER:**\n\n" +
                "1. Vào tab **`⚡ Tiện Ích & Tự Động`** ➔ Khu vực **Auto Clicker**.\n" +
                "2. Nhập tọa độ X, Y (hoặc giữ nguyên để click tại vị trí hiện tại), số lần click và khoảng cách mili-giây (ví dụ: 100ms = 10 clicks/giây).\n" +
                "3. Bấm **`Bắt đầu Auto Click`**.\n" +
                "⚠️ **Lưu ý:** Nhấn phím **`ESC`** hoặc **`F6`** bất kỳ lúc nào để ngắt khẩn cấp ngay lập tức!"
            ),

            new AiKnowledge(
                "Tiện Ích",
                new[] { "spam text", "auto paste", "dan tu dong", "gui tin nhan lien tuc", "spam ban phim" },
                "⌨️ **TỰ ĐỘNG DÁN & SPAM TEXT:**\n\n" +
                "1. Vào tab **`⚡ Tiện Ích & Tự Động`** ➔ Chọn **Auto Paste / Spam Text**.\n" +
                "2. Nhập nội dung văn bản hoặc danh sách nhiều dòng cần dán tự động.\n" +
                "3. Đặt khoảng delay giữa mỗi lần gửi.\n" +
                "4. Bấm Bắt đầu: Ứng dụng sẽ đếm lùi 2 giây để bạn chuyển sang cửa sổ cần dán.\n" +
                "⚠️ Nhấn **`ESC / F6`** để dừng lại."
            ),

            new AiKnowledge(
                "Tiện Ích",
                new[] { "pin", "chai pin", "battery", "laptop", "suc khoe pin", "bao cao pin" },
                "🔋 **CHẨN ĐOÁN PIN LAPTOP & ĐỘ CHAI PIN:**\n\n" +
                "1. Vào tab **`⚡ Tiện Ích & Tự Động`** ➔ Chọn **Chẩn Đoán Pin Laptop**.\n" +
                "2. Bấm **`🔍 Chẩn Đoán Chi Tiết`** để xem ngay:\n" +
                "   • **Design Capacity:** Dung lượng pin chuẩn khi xuất xưởng.\n" +
                "   • **Full Charge Capacity:** Dung lượng tối đa còn nạp được hiện tại.\n" +
                "   • **Tỷ lệ chai pin (% Wear):** Mức độ suy hao theo thời gian.\n" +
                "   • **Cycle Count:** Số chu kỳ sạc xả.\n" +
                "3. Bấm **`📄 Mở Báo Cáo HTML`** để xem báo cáo đồ họa chuyên sâu của Windows."
            ),

            new AiKnowledge(
                "Tiện Ích",
                new[] { "go bloatware", "xoa app rac", "bloatware", "xoa ung dung mac dinh", "bing news" },
                "🗑️ **GỠ BỎ BLOATWARE & ỨNG DỤNG RÁC WINDOWS:**\n\n" +
                "Vào tab **`⚡ Tiện Ích & Tự Động`** ➔ Bấm **`🗑️ Gỡ Bỏ Bloatware Windows`**.\n" +
                "Hệ thống sẽ tự động gỡ sạch các ứng dụng chạy ngầm không cần thiết cài sẵn trên Windows như Bing News, Bing Weather, Solitaire, Xbox App, Zune Music, Clipchamp..."
            ),

            // === [F] HƯỚNG DẪN CÀI ĐẶT & PHÍM TẮT ===
            new AiKnowledge(
                "Cài Đặt",
                new[] { "phim tat", "hotkey", "esc", "f6", "ngat", "dung lai", "emergency" },
                "⌨️ **DANH SÁCH PHÍM TẮT TIỆN ÍCH:**\n\n" +
                "• **`Phím ESC` hoặc `F6`:** Phím ngắt khẩn cấp toàn cục — Lập tức dừng Auto Clicker, Spam Text, xử lý Media Batch, quét ngầm.\n" +
                "• **`Phím Enter` trong ô Chat:** Gửi câu hỏi cho Trợ Lý AI Local ngay lập tức.\n" +
                "• **`Icon 🌙 / ☀️` (Góc dưới Sidebar):** Chuyển đổi qua lại giữa Giao diện Sáng (Light) và Tối (Dark).\n" +
                "• **`Tệp config.json`:** Mọi cài đặt được lưu tự động và phục hồi sau khi mở lại ứng dụng."
            ),

            new AiKnowledge(
                "Cài Đặt",
                new[] { "sang toi", "dark mode", "light mode", "doi mau", "theme", "giao dien" },
                "🌓 **CHẾ ĐỘ SÁNG / TỐI (THEME SWITCHER):**\n\n" +
                "• Bạn chỉ cần bấm vào nút **Giao diện Tối / Sáng (icon 🌙 / ☀️)** ở góc dưới bên trái thanh Sidebar.\n" +
                "• Toàn bộ 6 giao diện và Console Terminal sẽ đổi màu mượt mà theo thời gian thực (DynamicResource Palette).\n" +
                "• Cài đặt theme sẽ được ghi nhớ vĩnh viễn trong tệp `config.json`."
            )
        };

        // ---------------------------------------------------------------------
        // 3. THUẬT TOÁN XỬ LÝ NGÔN NGỮ & MATCHING LOGIC
        // ---------------------------------------------------------------------
        public async Task<string> AskAssistantAsync(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
                return "Bạn hãy nhập câu hỏi vào ô bên dưới nhé!";

            // Giả lập độ trễ tự nhiên (100ms) để giao diện mượt mà
            await Task.Delay(100);

            string cleanInput = NormalizeText(userInput);

            AiKnowledge? bestMatch = null;
            int maxScore = 0;

            foreach (var item in Dataset)
            {
                int score = 0;
                foreach (var kw in item.Keywords)
                {
                    string cleanKw = NormalizeText(kw);

                    // 1. Khớp chính xác từ khóa
                    if (cleanInput.Contains(cleanKw))
                    {
                        score += cleanKw.Length * 2;
                    }
                    else
                    {
                        // 2. Khớp từng từ đơn lẻ trong cụm từ khóa
                        var kwWords = cleanKw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        int wordMatches = kwWords.Count(w => cleanInput.Contains(w));
                        if (wordMatches == kwWords.Length && kwWords.Length > 1)
                        {
                            score += cleanKw.Length;
                        }
                    }
                }

                if (score > maxScore)
                {
                    maxScore = score;
                    bestMatch = item;
                }
            }

            if (bestMatch != null && maxScore > 0)
            {
                return bestMatch.Answer;
            }

            // Fallback khi không tìm thấy câu hỏi phù hợp
            return "🤖 **Trợ Lý AI chưa tìm thấy câu trả lời trực tiếp cho câu hỏi này.**\n\n" +
                   "💡 **Bạn có thể thử các câu hỏi mẫu gợi ý sau:**\n" +
                   "• *\"Cách dọn rác Win?\"*\n" +
                   "• *\"Xem mật khẩu Wi-Fi đã lưu?\"*\n" +
                   "• *\"Cách nén video giảm dung lượng?\"*\n" +
                   "• *\"Kiểm tra độ chai pin laptop?\"*\n" +
                   "• *\"Khôi phục mạng khi bị mất kết nối?\"*\n" +
                   "• *\"Cách giấu file bí mật vào video/ảnh?\"*\n" +
                   "• *\"Phím tắt ngắt khẩn cấp là gì?\"*";
        }

        // Chuẩn hóa văn bản: Chuyển chữ thường và loại bỏ dấu tiếng Việt
        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string normalized = text.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC)
                     .ToLowerInvariant()
                     .Replace('đ', 'd')
                     .Replace('Đ', 'D');
        }

        // Helper subclass
        private class KnowledgeRuleStub : AiKnowledge
        {
            public KnowledgeRuleStub(string category, string[] keywords, string answer)
                : base(category, keywords, answer) { }
        }
    }
}
