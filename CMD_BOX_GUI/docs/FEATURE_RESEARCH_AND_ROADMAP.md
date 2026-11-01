# 📋 TỔNG HỢP & NGHIÊN CỨU CÁC TÍNH NĂNG MỞ RỘNG (CMD BOX GUI)

Tài liệu này tổng hợp và phân tích các tính năng tiềm năng, hữu chiến và độc đáo nhất được chắt lọc từ các dự án tối ưu mã nguồn mở nổi tiếng (như **optimizerDuck**, **hellzerg/optimizer**, **Chris Titus Tech WinUtil**, **Sophia Script**, **PowerToys**), kèm theo vị trí tích hợp phù hợp vào hệ thống Sub-Tabs 3-Tier hiện tại của **CMD BOX GUI**.

---

## 🎯 TIÊU CHÍ CHỌN LỌC TÍNH NĂNG

1. **Tính Thực Tế Cao (High Utility):** Giải quyết đúng các điểm nghẽn khó chịu thường gặp nhất trên Windows 10 & 11 (lag giật chơi game, RAM đầy ảo, menu chuột phải rườm rà, mạng chập chờn).
2. **Độ An Toàn & Kiểm Soát (Safety First):** Ưu tiên các can thiệp có thể hoàn tác (`Reversible`), không làm phá vỡ cấu trúc cốt lõi của Windows, không gây lỗi màn hình xanh (BSOD).
3. **Phù Hợp Kiến Trúc UI Sub-Tab:** Mỗi tính năng mới đều có một không gian hiển thị tự nhiên trong 1 trong các Sub-Tab đã phân chia, không làm phá vỡ sự gọn gàng của giao diện.

---

## 🚀 DANH MỤC TÍNH NĂNG ĐỀ XUẤT THEO MODULE

### 1. Module SYSTEM OPTIMIZER (Tối Ưu & Tinh Chỉnh Hệ Thống)

| Tên Tính Năng | Mô Tả Kỹ Thuật & Tác Động | Vị Trí Tích Hợp Đề Xuất | Độ Ưu Tiên |
| :--- | :--- | :--- | :--- |
| **🧹 Dọn RAM Chờ (Standby List Cleaner)** | Gọi trực tiếp Windows API `NtSetSystemInformation` / `EmptyWorkingSet` để thu hồi ngay 2GB - 8GB bộ nhớ đệm Standby đang bị kẹt, giúp giảm hiện tượng khựng khung hình (stutter) khi chơi game nặng mà không cần khởi động lại máy. | `Tab 1: ⚡ Dọn Dẹp & Tinh Chỉnh` | ⭐⭐⭐⭐⭐ (Cực cao) |
| **⚡ Mở Khóa Gói Điện "Ultimate Performance"** | Kích hoạt profile cấp điện hiệu năng tối đa ẩn của Windows Workstation (`powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61`), loại bỏ hoàn toàn hiện tượng hạ xung nhịp CPU khi render hoặc chơi game. | `Tab 1: ⚡ Dọn Dẹp & Tinh Chỉnh` | ⭐⭐⭐⭐⭐ (Cực cao) |
| **🎯 Tắt Gia Tốc Chuột (Raw Input Gaming)** | Tắt tính năng *Enhanced Pointer Precision* và đặt đường cong chuyển động tuyến tính (`SmoothMouseXCurve`), giúp cảm giác rê chuột trong game bắn súng (CS2, Valorant, PUBG) đạt chuẩn 1:1 tuyệt đối. | `Tab 1: ⚡ Dọn Dẹp & Tinh Chỉnh` | ⭐⭐⭐⭐ (Cao) |
| **🪟 Khôi Phục Menu Chuột Phải Win 10 Cho Win 11** | Chèn khóa Registry `CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}` để hiển thị trực tiếp Menu chuột phải đầy đủ cổ điển, không còn phải bấm nút "Show more options" phiền phức. | `Tab 1: ⚡ Dọn Dẹp & Tinh Chỉnh` | ⭐⭐⭐⭐⭐ (Cực cao) |
| **💾 Tối Ưu Tuổi Thọ Ổ Cứng SSD NVMe** | Tự động tắt dịch vụ tạo chỉ mục tìm kiếm ngầm `WSearch` (Search Indexing) và ghi đệm file hệ thống thừa trên ổ SSD, giảm 100% tình trạng Disk Spike và kéo dài tuổi thọ chip nhớ NAND. | `Tab 2: 🛡️ Dịch Vụ Rác (Services)` | ⭐⭐⭐⭐ (Cao) |
| **🛡️ Bật / Tắt Defender Tạm Thời Khi Cài Game / Dev** | Cung cấp công tắc vô hiệu hóa tạm thời Windows Defender / SmartScreen trong phiên làm việc để không bị xóa nhầm file crack game, script test hoặc tool lập trình, có thể bật lại bình thường ngay sau đó. | `Tab 4: 🩺 Cứu Hộ & Sửa Lỗi` | ⭐⭐⭐⭐ (Cao) |

---

### 2. Module NETWORK & CONNECTIVITY (Mạng & Kết Nối)

| Tên Tính Năng | Mô Tả Kỹ Thuật & Tác Động | Vị Trí Tích Hợp Đề Xuất | Độ Ưu Tiên |
| :--- | :--- | :--- | :--- |
| **📱 Tạo Mã QR Chia Sẻ Wi-Fi** | Đọc mật khẩu Wi-Fi đang lưu trên máy và tự động tạo mã QR chuẩn `WIFI:T:WPA;S:...;P:...;;` hiển thị lên màn hình để điện thoại / máy tính bảng quét vào mạng ngay trong 1 giây không cần đọc mật khẩu dài dòng. | `Tab 4: 🔑 Mật Khẩu Wi-Fi` | ⭐⭐⭐⭐⭐ (Cực hot) |
| **🚀 Đo Tốc Độ Mạng Nhanh (Speedtest Mini CLI)** | Tích hợp engine đo tốc độ tải xuống (Download), tải lên (Upload) và độ trễ Ping/Jitter trực tiếp trên giao diện bằng máy chủ Cloudflare / Ookla mà không cần mở trình duyệt xem quảng cáo. | `Tab 1: 📊 Tổng Quan & Đổi DNS` | ⭐⭐⭐⭐ (Cao) |
| **🚫 Chặn Quảng Cáo Toàn Máy Bằng HOSTS File** | Tích hợp bộ lọc tên miền độc hại & quảng cáo (StevenBlack Hosts Filter) trực tiếp vào file `C:\Windows\System32\drivers\etc\hosts`. Giúp chặn quảng cáo, mã độc và theo dõi ngầm ở mọi app desktop. | `Tab 2: 🛠️ Sửa Lỗi & Cứu Hộ` | ⭐⭐⭐⭐ (Cao) |
| **🔍 Quét Cổng & Kết Nối Đang Mở (Port / Socket Viewer)** | Liệt kê danh sách các cổng mạng (TCP/UDP) đang mở và ứng dụng nào đang truyền dữ liệu ra Internet (tương tự `netstat -bano` nhưng có bảng lọc và hiển thị tên tiến trình kèm icon). | `Tab 3: 📡 Thiết Bị Mạng LAN` | ⭐⭐⭐ (Trung bình) |

---

### 3. Module MEDIA PROCESSING (Xử Lý Ảnh, Âm Thanh & Video)

| Tên Tính Năng | Mô Tả Kỹ Thuật & Tác Động | Vị Trí Tích Hợp Đề Xuất | Độ Ưu Tiên |
| :--- | :--- | :--- | :--- |
| **🌐 Tải Video Trực Tuyến Tự Động (yt-dlp GUI)** | Nhúng script / binary `yt-dlp` siêu nhẹ: Người dùng dán link YouTube, TikTok, Facebook, Douyin vào là tự động tải video chuẩn 1080p/4K hoặc xuất thẳng ra file nhạc MP3 320k không dính logo watermark. | `Tab 4: 🛠️ Hộp Công Cụ Media` | ⭐⭐⭐⭐⭐ (Cực hữu dụng) |
| **🏷️ Chèn Watermark / Bản Quyền Lên Hàng Loạt Video** | Cho phép chọn logo trong suốt (.png) hoặc nhập text thương hiệu, chọn góc hiển thị (trên-phải, dưới-phải, chính giữa mờ) và áp dụng hàng loạt lên danh sách video trước khi up mạng xã hội. | `Tab 4: 🛠️ Hộp Công Cụ Media` | ⭐⭐⭐⭐ (Cao) |
| **🔊 Chuẩn Hóa Âm Lượng Nhạc Hàng Loạt (EBU R128 Normalizer)** | Quét danh sách file nhạc/video và tự động cân bằng độ lớn âm thanh về mức chuẩn `-14 LUFS` (chuẩn YouTube/Spotify), chấm dứt tình trạng bài này quá to, bài kia quá nhỏ khi nghe playlist. | `Tab 2: 🎵 Tách Âm Thanh` | ⭐⭐⭐⭐ (Cao) |
| **📝 Tách / Ghép Phụ Đề Video (SRT Subtitle Tool)** | Tách phụ đề rời từ video MKV/MP4 hoặc nhúng cứng (hardsub) / nhúng mềm (softsub) phụ đề `.srt` vào video cực nhanh. | `Tab 4: 🛠️ Hộp Công Cụ Media` | ⭐⭐⭐ (Trung bình) |

---

### 4. Module UTILITIES (Tiện Ích Thực Chiến & Tự Động Hóa)

| Tên Tính Năng | Mô Tả Kỹ Thuật & Tác Động | Vị Trí Tích Hợp Đề Xuất | Độ Ưu Tiên |
| :--- | :--- | :--- | :--- |
| **🛡️ Chiếm Quyền File / Folder (Take Ownership)** | Thêm lệnh "Take Ownership" vào menu chuột phải hoặc kéo thả thư mục cứng đầu vào app để cấp toàn quyền `Administrators / Full Control` bằng lệnh `takeown` và `icacls`, xóa ngay các file "Access Denied" không thể xóa. | `Tab 1: 📦 Cài App Nhanh & Tinh Chỉnh` | ⭐⭐⭐⭐⭐ (Rất tiện) |
| **🔍 Kiểm Tra Mã Băm Tệp Tin (File Hasher Checksum)** | Kéo thả file vào để tính toán tức thì các mã băm **MD5, SHA-1, SHA-256** và so sánh với mã gốc để kiểm tra xem file tải trên mạng về có bị sửa đổi, dính virus hoặc tải thiếu dung lượng hay không. | `Tab 3: 🔋 Báo Cáo & Công Cụ Hệ Thống` | ⭐⭐⭐⭐ (Cao) |
| **👑 Lối Tắt "God Mode" Windows** | 1-Click tạo biểu tượng thư mục *God Mode* trên Desktop (`All Tasks.{ED7BA470-8E54-465E-825C-99712043E01C}`), gom toàn bộ hơn 200+ cài đặt ẩn sâu của Control Panel vào một màn hình duy nhất. | `Tab 3: 🔋 Báo Cáo & Công Cụ Hệ Thống` | ⭐⭐⭐⭐ (Cao) |
| **📊 Thông Số Phần Cứng & Khe RAM Chi Tiết** | Đọc chi tiết thông số RAM qua WMI: Loại RAM (DDR4 hay DDR5), Tốc độ Bus (3200MHz, 4800MHz, 6000MHz), Số khe cắm còn trống để người dùng biết có nâng cấp thêm RAM được hay không mà không cần tháo nắp máy. | `Tab 3: 🔋 Báo Cáo & Công Cụ Hệ Thống` | ⭐⭐⭐⭐⭐ (Rất hữu ích) |

---

## 📊 MA TRẬN ĐÁNH GIÁ TÍNH KHẢ THI (FEASIBILITY & PRIORITY MATRIX)

```
        MỨC ĐỘ HỮU ÍCH (IMPACT)
             ▲
             │  [Dọn RAM Chờ]          [QR Code Wi-Fi]
   CAO       │  [Menu Win 10 cho 11]   [yt-dlp Tải Video]
             │  [Ultimate Power Plan]  [Take Ownership]
             │  [Thông số Khe RAM]     [File Hasher MD5]
             │──────────────────────────────────────────
             │  [Tắt Gia Tốc Chuột]    [Chặn Ads HOSTS]
   TRUNG BÌNH│  [Tối Ưu SSD NVMe]      [Speedtest CLI]
             │  [Watermark Video]      [God Mode Shortcut]
             │
             └──────────────────────────────────────────►
                ĐỘ KHÓ TRIỂN KHAI (IMPLEMENTATION EFFORT)
                 THẤP (1-2 Ngày)         TRUNG BÌNH (3-5 Ngày)
```

---

## 🗓️ GỢI Ý LỘ TRÌNH TRIỂN KHAI THEO TỪNG GIAI ĐOẠN

### Giai Đoạn 1: "Quick Wins" (Dễ làm, hiệu ứng cực mạnh, rủi ro bằng 0)
1. **Khôi phục Menu chuột phải Win 10 cho Win 11**: Can thiệp 1 khóa Registry, người dùng thấy sướng ngay lập tức.
2. **Tạo mã QR Wi-Fi từ mật khẩu đã lưu**: Người dùng máy tính kết nối điện thoại cực kỳ tiện lợi.
3. **Mở khóa Ultimate Performance Power Plan**: 1 lệnh CMD `powercfg` kích hoạt gói điện game thủ.
4. **Kiểm tra mã băm file (MD5 / SHA256 Hasher)**: Kéo thả file tính mã băm cực nhanh bằng thư viện chuẩn của C# `System.Security.Cryptography`.

### Giai Đoạn 2: "Advanced Performance & Media" (Nâng tầm ứng dụng)
1. **Dọn RAM Standby List Cleaner**: Viết Service gọi Win32 API giải phóng bộ nhớ đệm chống giật lag khi chơi game.
2. **Tích hợp trình tải video đa nền tảng yt-dlp**: Dán link tải video YouTube / TikTok không dính watermark.
3. **Đọc chi tiết số khe RAM và Bus RAM**: Dùng WMI `Win32_PhysicalMemory` để người dùng kiểm tra khả năng nâng cấp máy tính.
4. **Đo tốc độ mạng Speedtest CLI**: Tích hợp kiểm tra Ping / Download / Upload trực tiếp trong tab Network.

---

*Tài liệu này được biên soạn độc quyền cho dự án **CMD BOX GUI** nhằm làm kim chỉ nam phát triển các phiên bản kế tiếp.*
