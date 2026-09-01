# CMD BOX GUI

Bộ công cụ quản trị hệ thống, tối ưu hóa Windows và xử lý đa phương tiện hiệu năng cao trên nền tảng WPF (.NET 10).

---

## 1. Tổng quan kiến trúc

- Nền tảng: C# 13, .NET 10.0-windows, WPF (Windows Presentation Foundation).
- Giao diện: Flat Modern Dark/Light theme, hỗ trợ Dynamic Resource switching thời gian thực.
- Kiến trúc xử lý: Asynchronous Task-based, P/Invoke Win32 API, CPU Multi-threading SIMD, FFmpeg Integration.

---

## 2. Các phân hệ chức năng

### Optimizer (Tối ưu hóa hệ thống)
- Master Make Win: Tinh chỉnh 1-Click tự động (Taskbar Win 11, tắt Telemetry, gỡ Bloatware, tối ưu độ trễ hệ thống, quản lý BitLocker).
- Disk & Cache Clean: Quick Clean, Deep Clean PRO (WinSxS, Prefetch, GPU Shaders, Event Logs), dọn dẹp Developer Cache (NPM, Pip, NuGet, Cargo, Gradle) và Browser Cache.
- Performance & Diagnostics: Quản lý Startup applications, sửa lỗi Windows Update, cá nhân hóa Startup Greeting (thông điệp khởi động).

### Media Processing (Xử lý đa phương tiện)
- Làm nét ảnh Non-AI: Phân tách không gian màu YCbCr (chỉ xử lý kênh Luminance Y), nội suy Super-Sampling Catmull-Rom Bicubic, thuật toán Contrast Adaptive Sharpening (CAS) kết hợp Anti-Halo và bảo toàn Metadata/Color Profile gốc.
- Quản lý định dạng: Tối ưu chuẩn nén JPEG (Quality 92%), WebP, PNG, MP4, GIF.
- Xử lý hàng loạt: Nén video chuẩn điện ảnh (CRF/Bitrate Target), chuyển đổi định dạng, trích xuất âm thanh, tắt tiếng (Stream copy siêu tốc).

### Network & Security (Mạng và bảo mật)
- Quét và trích xuất hồ sơ/mật khẩu Wi-Fi đã lưu trên thiết bị.
- Dò quét thiết bị LAN (ARP Table Scan) và kiểm tra mở cổng dịch vụ (Port Scanner).
- Chẩn đoán mạng (Ping, DNS Flush, Adapter Stats) và kiểm soát Windows Firewall.

### Utilities (Tiện ích tự động hóa)
- Auto Clicker & Auto Paste (Spam Text): Điều khiển phần cứng ở tầng Win32 API với cơ chế ngắt khẩn cấp (Emergency Stop).
- Chẩn đoán pin laptop: Kiểm tra dung lượng thiết kế, dung lượng thực tế, độ chai pin và chu kỳ sạc.

---

## 3. Cấu trúc thư mục mã nguồn

```text
CMD_BOX_GUI/
├── Core/               # Tầng lõi: Logger đa luồng, Win32 P/Invoke, ProcessRunner, NativeImageEnhancer
├── Models/             # Mô hình dữ liệu và các đối tượng Observable
├── Services/           # Tầng nghiệp vụ: OptimizerService, MediaService, NetworkService, UtilityService
├── UI/
│   ├── Styles/         # Resource dictionaries: Colors.xaml, Controls.xaml
│   └── Views/          # UserControls giao diện chính (Optimizer, Media, Network, Utilities, Guide)
├── Properties/         # Cấu hình đóng gói xuất bản (PublishProfiles)
├── MainWindow.xaml     # Shell điều hướng và Console Log Terminal
└── App.xaml            # Điểm khởi chạy ứng dụng
```

---

## 4. Yêu cầu hệ thống & Biên dịch

- Hệ điều hành: Windows 10 / Windows 11 (x64).
- Môi trường phát triển: .NET SDK 10.0 trở lên, Visual Studio 2022 / VS Code.
- Công cụ phụ trợ: FFmpeg (tùy chọn, tự động nhận diện nếu có trong PATH hoặc thư mục ứng dụng).

### Lệnh biên dịch:
```bash
# Khôi phục gói và biên dịch bản Debug
dotnet build

# Xuất bản file thực thi đơn lẻ (Single-file Release)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
