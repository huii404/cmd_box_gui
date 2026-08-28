# 🛠️ CMD BOX GUI — HỆ THỐNG QUẢN TRỊ & TỐI ƯU HÓA WINDOWS (WPF .NET 8)

> **Tài liệu hướng dẫn kiến trúc, quy chuẩn phát triển và quy tắc dành cho AI / Lập trình viên kế thừa.**

---

## 📌 1. TỔNG QUAN DỰ ÁN
* **Công nghệ:** C# 12, .NET 8.0, WPF (Windows Presentation Foundation), Win32 P/Invoke, FFmpeg Engine.
* **Phong cách giao diện:** Flat Modern Slate, hỗ trợ chuyển đổi linh hoạt **Dark Mode / Light Mode** thời gian thực (Realtime Dynamic Theming).
* **Mục tiêu:** Cung cấp bộ công cụ tất-cả-trong-một:
  1. Giám sát phần cứng và thông số hệ thống thời gian thực (Dashboard).
  2. Tối ưu, dọn rác, tinh chỉnh dịch vụ và sửa lỗi Windows (Optimizer).
  3. Quản trị kết nối mạng, trích xuất mật khẩu Wi-Fi, lá chắn bảo mật (Network).
  4. Công cụ tự động hóa Auto Clicker, Auto Paste/Spam, chẩn đoán pin laptop (Utilities).
  5. Trung tâm biên tập Media hàng loạt bằng FFmpeg & Giấu file bí mật (Steganography).
  6. Cẩm nang tra cứu và hướng dẫn sử dụng tích hợp (Guide View).

---

## 📂 2. CẤU TRÚC THƯ MỤC & MÃ NGUỒN

```text
CMD_BOX_GUI/
├── Core/                           # Tầng lõi hệ thống & Native API
│   ├── Logger.cs                   # Engine ghi log đa luồng thread-safe (ConcurrentQueue)
│   ├── NativeMethods.cs            # P/Invoke Win32 API (SendInput, RAM, Battery, DNS...)
│   ├── ProcessRunner.cs            # Engine thực thi tiến trình CMD/Powershell async
│   └── SystemCore.cs               # Quyền Admin, phím ngắt khẩn cấp (ESC/F6), Byte formatting
├── Models/                         # Data Models & Observable Objects
│   └── AppModels.cs                # DriveStorageInfo, WifiInfo, BatteryInfo, MediaBatchItem...
├── Services/                       # Tầng Business Logic & Xử lý nghiệp vụ
│   ├── ChatbotService.cs           # Local AI Engine hỏi đáp thông minh và giải quyết sự cố
│   ├── MediaService.cs             # Engine FFmpeg biên tập media & Steganography
│   ├── NetworkService.cs           # Quản trị mạng, song song hóa quét Wi-Fi, Firewall
│   ├── OptimizerService.cs         # Dọn rác nhanh, dọn rác PRO, dọn dev cache, sửa Windows Update
│   ├── SettingsService.cs          # Quản lý lưu trữ & nạp cấu hình JSON (config.json)
│   ├── ThemeService.cs             # Engine quản lý bảng màu Dark/Light Mode đồng bộ
│   └── UtilityService.cs           # Auto Click, Auto Paste, Pin Laptop, Bloatware
├── UI/
│   ├── Styles/                     # Design System & Resource Dictionaries
│   │   ├── Colors.xaml             # Định nghĩa các SolidColorBrush màu sắc cốt lõi
│   │   └── Controls.xaml           # Style phẳng cho Button, TextBox, DataGrid, ComboBox, Expander
│   └── Views/                      # Các trang giao diện (UserControls)
│       ├── DashboardView.xaml(.cs) # Màn hình Trợ Lý AI Local (Hỏi đáp & Trợ giúp thông minh 100% Offline)
│       ├── OptimizerView.xaml(.cs) # Màn hình dọn dẹp & tối ưu hệ thống
│       ├── NetworkView.xaml(.cs)   # Màn hình mạng & bảo mật
│       ├── UtilitiesView.xaml(.cs) # Màn hình tiện ích tự động & chẩn đoán
│       ├── MediaView.xaml(.cs)     # Màn hình bảng biên tập video hàng loạt & Stego
│       └── GuideView.xaml(.cs)     # Màn hình cẩm nang tra cứu & tìm kiếm kiến thức
├── MainWindow.xaml(.cs)            # Cửa sổ chính, Navigation Sidebar, Console Log Terminal
└── App.xaml(.cs)                   # Entry point, nạp Merged Dictionaries & ThemeService
```

---

## ⚡ 3. QUY TẮC VÀNG CHO AI & DEVELOPER KẾ THỪA

### 🎨 Quy tắc 1: Quản lý Màu sắc & Dynamic Theming
* **BẮT BUỘC:** Toàn bộ thuộc tính màu sắc trong XAML (`Background`, `Foreground`, `BorderBrush`, `Fill`...) **PHẢI** sử dụng `{DynamicResource ColorKey}` thay vì `{StaticResource ColorKey}`.
* **Lý do:** `StaticResource` chỉ gán màu 1 lần duy nhất khi khởi tạo. Chỉ có `DynamicResource` mới phản hồi tức thì với sự kiện thay đổi bảng màu tại `ThemeService` mà không cần reload lại View.
* **Bảng màu chuẩn (`ThemeService.cs`):**
  * `BgPrimary`, `BgSecondary`, `BgCard`, `BgCardHover`, `BgInput`
  * `BorderSubtle`, `BorderActive`
  * `TextPrimary`, `TextSecondary`, `TextMuted`
  * `AccentPrimary`, `AccentSuccess`, `AccentWarning`, `AccentDanger`, `AccentCyan`, `AccentPurple`
  * `TerminalBg`, `TerminalText`

### 🧵 Quy tắc 2: Đa Luồng & Bất Đồng Bộ (Threading & Async)
* **Tuyệt đối KHÔNG block UI Thread:** Tất cả tác vụ I/O, quét registry, gọi CLI, quét file, nén video phải bọc trong `await Task.Run(...)` hoặc `ProcessRunner.RunProcessAsync(...)`.
* **Hỗ trợ CancellationToken & Phím Ngắt Khẩn Cấp:**
  * Mọi tác vụ chạy lặp hoặc tốn thời gian (Auto Clicker, Spam Text, Batch FFmpeg, Clean PRO) **bắt buộc** truyền `CancellationToken` và kiểm tra `SystemCore.CheckEmergencyStop()` (phím `ESC` hoặc `F6`).
* **Cập nhật giao diện từ Background Thread:** Luôn sử dụng `Dispatcher.Invoke(() => { ... })` khi cập nhật UI/ObservableCollection từ Task ngầm.
* **Xử lý tác vụ hàng loạt:** Sử dụng `Parallel.ForEachAsync` với `MaxDegreeOfParallelism` hợp lý (ví dụ: quét Wi-Fi đa luồng) để tăng tốc độ gấp nhiều lần.

### 🚀 Quy tắc 3: Tối Ưu Hiệu Năng (Performance & Low Resource)
* **Cache thông số tĩnh:** Các thông số phần cứng cố định (Tên CPU, kiến trúc OS, Tên máy, Current User, .NET Version) chỉ đọc **1 lần duy nhất** khi khởi động tại `DashboardView`, không query lặp lại trong timer.
* **Sử dụng Win32 Native API trực tiếp:**
  * Đo RAM: Dùng `NativeMethods.GlobalMemoryStatusEx(...)` (thời gian tính bằng micro-giây, CPU 0%).
  * Đo Pin: Dùng `NativeMethods.GetSystemPowerStatus(...)`.
  * *Tránh dùng WMI (`ManagementObjectSearcher`)* vì WMI tốn nhiều CPU và gây trễ giao diện.
* **Chống tràn bộ nhớ Console Log:** Trong `MainWindow.xaml.cs`, duy trì giới hạn tối đa 500 dòng log (`MaxLogLines = 500`) và batching timer 50ms để ngăn lag TextBox khi có luồng log lớn.

### 🛡️ Quy tắc 4: An Toàn Tiến Trình & Quyền Administrator
* **Kiểm tra quyền:** Sử dụng `SystemCore.IsAdministrator()` trước khi chạy các lệnh can thiệp sâu (DISM, Sửa Windows Update, Gỡ Bloatware, Chặn Firewall).
* **Xử lý Clipboard an toàn:** Khi thao tác `Clipboard.SetDataObject(...)`, luôn dùng hàm bọc `SafeSetClipboardText(...)` có cơ chế thử lại (retry) để tránh ngoại lệ `COMException (0x800401D0)` khi phần mềm khác đang khóa clipboard.

### 🎬 Quy tắc 5: Xử lý Media & Steganography
* **FFmpeg Resolution:** Không hardcode đường dẫn tuyệt đối. Luôn tìm qua `MediaService.FindFFmpegPath()`.
* **Steganography Magic Marker:** Sử dụng chữ ký độc quyền `---CMD_BOX_SECRET_PAYLOAD---` để định vị vùng dữ liệu giấu ở đuôi tệp media mà không làm hỏng cấu trúc phát video/ảnh.

---

## 📝 4. LỊCH SỬ CẢI TIẾN & PHIÊN BẢN

* **v2.0 (Phiên bản hiện tại):**
  * Tích hợp toàn diện giao diện phẳng WPF Flat Dark / Light Slate.
  * Thêm nút chuyển đổi Theme ở góc dưới bên trái Sidebar với icon 🌙 / ☀️ và cập nhật thời gian thực cho cả 6 Views và Terminal Console.
  * Bổ sung trang `GuideView` với thanh tìm kiếm thông minh và cơ chế co giãn Accordion Expander.
  * Tối ưu hóa hiệu năng CPU về mức ~0% khi chạy nền Dashboard, cache thông số tĩnh.
  * Tăng cường Threading: Song song hóa quét Wi-Fi với `Parallel.ForEachAsync`, hỗ trợ `CancellationToken` và phím ngắt khẩn cấp `ESC / F6` trên mọi tác vụ.
