# 🛠️ CMD BOX GUI (WPF Edition) & Phân Tích Hệ Thống Gốc (C++ CMD)

> **Tài liệu phân tích kiến trúc ứng dụng C++ gốc (`G:\Code\C++\project\CMD`) và kế hoạch nâng cấp giao diện đồ họa hiện đại trên C# WPF (`CMD_BOX_GUI`).**

---

## 📑 Mục lục
1. [Giới thiệu tổng quan](#1-giới-thiệu-tổng-quan)
2. [Phân tích chi tiết các Module của ứng dụng C++ gốc](#2-phân-tích-chi-tiết-các-module-của-ứng-dụng-c-gốc)
   - [2.1. SystemCore (Nhân lõi hệ thống & Win32 API)](#21-systemcore-nhân-lõi-hệ-thống--win32-api)
   - [2.2. SystemOptimizer (Bảo trì & Tối ưu hóa Windows)](#22-systemoptimizer-bảo-trì--tối-ưu-hóa-windows)
   - [2.3. Internet & Security (Mạng & Bảo mật chuyên sâu)](#23-internet--security-mạng--bảo-mật-chuyên-sâu)
   - [2.4. UtilityTools (Công cụ Tự động hóa & Tiện ích)](#24-utilitytools-công-cụ-tự-động-hóa--tiện-ích)
   - [2.5. MediaProcessor (Xử lý Đa phương tiện qua FFmpeg & GPU)](#25-mediaprocessor-xử-lý-đa-phương-tiện-qua-ffmpeg--gpu)
   - [2.6. AI Virtual Assistant (Trợ lý chẩn đoán & Điều hướng)](#26-ai-virtual-assistant-trợ-lý-chẩn-đoán--điều-hướng)
3. [Chiến lược tích hợp: Kết hợp C# UI & Logic Native/C++](#3-chiến-lược-tích-hợp-kết-hợp-c-ui--logic-nativec)
   - [3.1. Nhóm 1: Xử lý hoàn toàn bằng C# thuần (.NET Native APIs)](#31-nhóm-1-xử-lý-hoàn-toàn-bằng-c-thuần-net-native-apis)
   - [3.2. Nhóm 2: Kết hợp Win32 P/Invoke & Native C++/FFmpeg](#32-nhóm-2-kết-hợp-win32-pinvoke--native-cffmpeg)
4. [Kiến trúc Đề xuất cho Ứng dụng C# WPF (`CMD_BOX_GUI`)](#4-kiến-trúc-đề-xuất-cho-ứng-dụng-c-wpf-cmd_box_gui)
5. [Lộ trình triển khai nâng cấp](#5-lộ-trình-triển-khai-nâng-cấp)

---

## 1. Giới thiệu tổng quan

Ứng dụng gốc **CMD BOX** (viết bằng C++17 tại `G:\Code\C++\project\CMD`) là một bộ công cụ dòng lệnh (CLI) mạnh mẽ dành cho Windows, cung cấp đầy đủ các tính năng dọn dẹp hệ thống, tinh chỉnh Registry/Services, tối ưu mạng, tự động hóa tác vụ (Auto Click/Spam/Auto Paste), kiểm tra pin và xử lý media tăng tốc phần cứng qua FFmpeg.

Mục tiêu của dự án **`CMD_BOX_GUI` (C# WPF)**:
- **Hiện đại hóa trải nghiệm người dùng (UX/UI):** Thay thế giao diện Console đen trắng bằng giao diện đồ họa WPF hiện đại (Fluent Design/Dark Mode), có thanh tiến trình (ProgressBar), bảng log trực quan (Realtime Terminal Log), nút bấm điều khiển trực tiếp và trạng thái hệ thống cập nhật liên tục.
- **Tối ưu hóa kiến trúc:** Sử dụng sức mạnh và sự tiện lợi của **C# .NET** cho toàn bộ logic cơ bản, giao diện, luồng bất đồng bộ (`async/await`) và xử lý dữ liệu. Đồng thời kết hợp với **Win32 P/Invoke / Native Command / FFmpeg Process** cho các lệnh can thiệp hệ thống sâu.

---

## 2. Phân tích chi tiết các Module của ứng dụng C++ gốc

### 2.1. SystemCore (Nhân lõi hệ thống & Win32 API)
*Tập tin: `include/SystemCore.h`, `src/SystemCore.cpp`*

- **Quyền Quản trị viên (UAC Elevation):** Kiểm tra `isElevated()` qua Windows Token (`GetTokenInformation`), thực thi lệnh Admin bằng PowerShell / Batch Script tạm thời (`runBatchAsAdmin`).
- **Quản lý Tiến trình bằng Job Object:** Sử dụng Win32 `CreateJobObjectA` và `SetInformationJobObject` với cờ `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` để đảm bảo khi đóng ứng dụng thì toàn bộ tiến trình con (FFmpeg, PowerShell, CMD) bị dọn dẹp sạch sẽ, không bị treo ngầm.
- **Tự động hóa Chuột & Bàn phím:** Mô phỏng click chuột trái (`mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP)`), dán dữ liệu qua Clipboard (`OpenClipboard`, `CF_UNICODETEXT`, `SetClipboardData`) và mô phỏng tổ hợp phím `Ctrl + V`, `Enter`.
- **Cơ chế Dừng khẩn cấp (Emergency Stop):** Sử dụng `GetAsyncKeyState(VK_ESCAPE)` và `GetAsyncKeyState(VK_F6)` để ngắt vòng lặp auto-click hoặc spam ngay lập tức.
- **Giám sát thiết bị:** Kiểm tra trạng thái Pin (`GetSystemPowerStatus`), phần trăm pin, trạng thái sạc AC/DC.

---

### 2.2. SystemOptimizer (Bảo trì & Tối ưu hóa Windows)
*Tập tin: `include/SystemOptimizer.h`, `src/SystemOptimizer.cpp`*

- **Dọn rác nhanh (Quick Clean - Đa luồng 1-3s):**
  - Quét & xóa sạch các thư mục: User Temp (`%temp%`), Windows Temp (`C:\Windows\Temp`), Recent Files, DirectX Shader Cache (`%LocalAppData%\D3DSCache`), CryptnetUrlCache, Windows Error Reporting Temp (`WER\Temp`).
  - Xóa sạch Thùng rác (`Clear-RecycleBin`) và xóa cache DNS (`ipconfig /flushdns`).
  - Đo lường dung lượng ổ `C:\` trước và sau khi dọn (`fs::space`) để hiển thị dung lượng đã giải phóng.
- **Dọn rác chuyên sâu (Disk Pro Clean):**
  - Dọn dẹp cache sâu: `Prefetch`, Windows Update Download Cache (`SoftwareDistribution\Download`), CBS Logs, DISM Component Store, Delivery Optimization Files, Thumbnails Cache.
  - Sử dụng kỹ thuật `Robocopy /MIR` với thư mục rỗng để xóa hàng triệu file rác tạm thời siêu tốc mà không làm đơ ứng dụng.
  - Gọi công cụ dọn dẹp gốc của Windows: `cleanmgr /sagerun:1`.
- **Quản lý ứng dụng khởi động (Startup Apps Management):**
  - Quét Registry Run Keys: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` và `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`.
  - Có cơ chế **Whitelist thông minh** bảo vệ các ứng dụng thiết yếu: Driver âm thanh (Realtek, Waves), Driver GPU (NVIDIA, AMD, Intel), OEM Tools (Dell, Asus, HP, Lenovo), Touchpad (Synaptics, Alps), Cloud Sync (OneDrive).
- **Quản lý Dịch vụ Windows (Win32 Service Control Manager):**
  - Can thiệp trực tiếp qua API: `OpenSCManagerA`, `OpenServiceA`, `ChangeServiceConfigA`, `ControlService(SERVICE_CONTROL_STOP)`.
  - Tắt/Chuyển Manual các dịch vụ không cần thiết: Telemetry (DiagTrack, dmwappushservice), MapsBroker, Xbox Services (XblAuthManager, XboxGipSvc...), Windows Error Reporting (WerSvc), Windows Search (WSearch).
- **Sửa lỗi Windows Update:**
  - Tự động dừng các service: `wuauserv`, `cryptSvc`, `bits`, `msiserver`.
  - Đổi tên/Xóa thư mục cache bị kẹt: `C:\Windows\SoftwareDistribution` và `C:\Windows\System32\catroot2`.
  - Khởi động lại các service và kích hoạt dò tìm cập nhật mới (`wuauclt /detectnow`).
- **Dọn Cache Môi trường Lập trình (Dev Clean):**
  - Quét và xóa cache các công cụ: `npm cache clean --force`, `pip cache purge`, `.gradle/caches`, `.cargo/cache`, `nuget locals all -clear`, `__pycache__`, các thư mục `node_modules` tạm.
- **Tinh chỉnh Taskbar Windows 11 & Hệ thống PRO:**
  - Chỉnh Registry ẩn SearchBox, Widgets, Copilot, Chat/Teams, TaskView.
  - Tinh chỉnh `MenuShowDelay = 0`, `WaitToKillAppTimeout = 2000`, tắt Hibernate (`powercfg -h off`) để giải phóng hàng chục GB ổ `C:\`.

---

### 2.3. Internet & Security (Mạng & Bảo mật chuyên sâu)
*Tập tin: `include/Internet.h`, `src/Internet.cpp`*

- **Xem thông tin mạng & Wi-Fi:**
  - Lấy IP nội bộ, Subnet Mask, Gateway, DNS Servers qua thư viện `iphlpapi.h` (`GetAdaptersAddresses` / `GetAdaptersInfo`).
  - Truy vấn Public IP ngoại mạng qua HTTP endpoint.
  - Trích xuất toàn bộ Profile Wi-Fi đã lưu kèm mật khẩu văn bản rõ (cleartext password) qua lệnh `netsh wlan show profile name="..." key=clear`.
- **Sửa lỗi & Khôi phục mạng (Network Repair PRO - 8 bước):**
  1. Xóa cache phân giải DNS (`ipconfig /flushdns`).
  2. Reset Winsock Catalog (`netsh winsock reset`).
  3. Reset toàn bộ TCP/IP Stack (`netsh int ip reset`).
  4. Xóa bảng ARP Cache (`arp -d *`).
  5. Giải phóng và xin cấp lại IP qua DHCP (`ipconfig /release` & `ipconfig /renew`).
  6. Khởi động lại dịch vụ WinNAT & HNS (sửa triệt để lỗi xung đột Socket 10013 do Docker/WSL2/Hyper-V).
  7. Mở cổng Firewall cho kết nối mạng nội bộ (LAN HTTP & LocalSend cổng 53317 TCP/UDP).
  8. Chuyển cấu hình mạng sang Private Network để tối ưu chia sẻ tài nguyên.
- **Kích hoạt Lá chắn Bảo mật (Full Security Shield):**
  - Bật Windows Defender & Cập nhật chữ ký virus mới nhất.
  - Kích hoạt tính năng chống Ransomware: *Controlled Folder Access*.
  - Đóng các cổng mạng nguy hiểm dễ bị khai thác tấn công LAN/WannaCry: Cổng `445` (SMB), `139`, `135`, `137`, `138`.
  - Cấu hình DNS-over-HTTPS (DoH) bảo mật với Cloudflare (`1.1.1.1` & `1.0.0.1`).
  - Quét & kiểm tra tính toàn vẹn của tệp `C:\Windows\System32\drivers\etc\hosts`.

---

### 2.4. UtilityTools (Công cụ Tự động hóa & Tiện ích)
*Tập tin: `include/UtilityTools.h`, `src/UtilityTools.cpp`*

- **Auto Clicker:** Cho phép nhập tọa độ X/Y hoặc lấy tọa độ chuột hiện tại, thiết lập số lần click, khoảng cách nghỉ (interval ms) và thời gian đếm ngược. Hỗ trợ phím tắt ngắt khẩn cấp (`ESC`/`F6`).
- **Spam Text & Auto Paste:** Tự động gửi văn bản lặp lại nhiều lần hoặc dán danh sách nhiều dòng từ file/clipboard vào cửa sổ mục tiêu (hỗ trợ Unicode tiếng Việt chuẩn).
- **Trình tải & Cài đặt phần mềm tự động (Download Manager):**
  - Tải tự động các phần mềm thiết yếu: Trình duyệt (Chrome, Brave, Cốc Cốc), Bộ gõ tiếng Việt (EVKey, OpenKey), Chat (Zalo, Discord, Telegram), Nén file (7-Zip, WinRAR), Lập trình (VS Code, Notepad++, Git), VPN (WARP 1.1.1.1).
- **Gỡ bỏ Bloatware Windows:** Dùng PowerShell gỡ bỏ các ứng dụng rác cài sẵn của Windows (Cortana, Xbox App, Solitaire, News, Weather, Tips, v.v.).
- **Chẩn đoán Sức khỏe Pin Laptop (Battery Diagnostics):**
  - Đọc thông số ACPI qua `GetSystemPowerStatus`.
  - Gọi `powercfg /batteryreport /xml` để phân tích: Tên nhà sản xuất pin, Hóa tính (Li-ion), Dung lượng thiết kế ban đầu (`DesignCapacity`), Dung lượng khi sạc đầy thực tế (`FullChargeCapacity`), Số chu kỳ sạc (`CycleCount`), tính tỷ lệ độ chai pin (% Wear Level) và xuất file báo cáo HTML trực quan.

---

### 2.5. MediaProcessor (Xử lý Đa phương tiện qua FFmpeg & GPU)
*Tập tin: `include/MediaProcessor.h`, `src/MediaProcessor.cpp`*

- **Tự động nhận diện Phần cứng GPU:** Kiểm tra card đồ họa để kích hoạt encoder phần cứng tương ứng:
  - NVIDIA: `h264_nvenc`
  - Intel: `h264_qsv`
  - AMD: `h264_amf`
  - Fallback CPU: `libx264` (Preset `faster`)
- **Nén Video & Ảnh (Batch Compress):** Nén giảm dung lượng nhưng bảo toàn metadata (EXIF/GPS) và chất lượng hình ảnh.
- **Làm nét & Phục chế (Enhancement):** Sử dụng các bộ lọc chuyên sâu của FFmpeg: `unsharp`, `hqdn3d` (khử nhiễu không gian và thời gian).
- **Trích xuất âm thanh:** Chuyển đổi video MP4/MKV sang MP3 chất lượng cao (`libmp3lame -q:a 2`).
- **Thay đổi tốc độ phát:** Hỗ trợ từ 0.5x (Slow-motion) đến 2.0x (Tua nhanh) kết hợp bộ lọc âm thanh `atempo` chống méo giọng.
- **Chuẩn hóa tên file:** Đổi tên hàng loạt tệp trong thư mục theo mẫu chuẩn (loại bỏ ký tự đặc biệt, chuẩn hóa khoảng trắng).
- **Giấu file vào Media (Steganography / File-in-File):** Nhúng tệp bí mật (dưới dạng khối ZIP / Binary payload) vào đuôi tệp Ảnh (JPG/PNG) hoặc Video (MP4) và trích xuất lại nguyên vẹn.

---

### 2.6. AI Virtual Assistant (Trợ lý chẩn đoán & Điều hướng)
*Tập tin: `scripts/assistant.py`, `ai_engine.py`, `dataset.py`, `skills.py`*

- Phân tích câu lệnh ngôn ngữ tự nhiên tiếng Việt từ người dùng (ví dụ: *"máy lag quá"*, *"xóa rác"*, *"xem pass wifi"*, *"nén video"*).
- Tự động map ý định (Intent) tới tính năng tương ứng hoặc mở trực tiếp các công cụ Windows (Task Manager, Recycle Bin, Network Settings, Services).

---

## 3. Chiến lược tích hợp: Kết hợp C# UI & Logic Native/C++

Để ứng dụng vừa đạt hiệu năng tối đa, vừa có giao diện WPF mượt mà, chúng ta phân chia các nhóm chức năng như sau:

### 3.1. Nhóm 1: Xử lý hoàn toàn bằng C# thuần (.NET Native APIs)
*Các chức năng này trong C# .NET có thư viện chuẩn rất mạnh, không cần phụ thuộc vào mã C++ bên ngoài:*

| Chức năng | Cơ chế C# .NET tương ứng | Lợi thế khi dùng C# |
| :--- | :--- | :--- |
| **Dọn rác nhanh / Dev Cache** | `System.IO.DirectoryInfo`, `FileInfo`, `Directory.Delete` chạy trên `Task.Run` | Xử lý đa luồng an toàn, dễ bắt lỗi `UnauthorizedAccessException`, cập nhật ProgressBar mượt mà |
| **Kiểm tra thông tin mạng** | `System.Net.NetworkInformation.NetworkInterface`, `IPGlobalProperties`, `HttpClient` | Đọc IP, Gateway, DNS, MAC Address cực nhanh không cần parse chuỗi CMD |
| **Đo dung lượng ổ đĩa** | `System.IO.DriveInfo.GetDrives()` | Lấy thông tin TotalSize, FreeSpace chính xác |
| **Chỉnh sửa Registry (Taskbar, Tweaks)** | `Microsoft.Win32.Registry`, `RegistryKey.SetValue` | Thao tác an toàn, không cần mở process `reg.exe` |
| **Quản lý Dịch vụ Windows (Services)** | `System.ServiceProcess.ServiceController` | Đọc trạng thái, Start/Stop service với sự kiện bất đồng bộ chuẩn .NET |
| **Tải phần mềm (Download Manager)** | `System.Net.Http.HttpClient` + `Progress<float>` | Hiển thị tiến trình tải (%) và tốc độ tải trực tiếp lên UI |
| **Chuẩn hóa tên file Media** | `System.IO.Path`, `Directory.GetFiles`, `File.Move` | Xử lý chuỗi Regex & Unicode tiếng Việt rất nhanh |
| **Đọc báo cáo Pin Laptop** | Chạy ngầm `powercfg /batteryreport /xml` và parse bằng `System.Xml.Linq.XDocument` | Dễ dàng trích xuất thông số kỹ thuật và vẽ biểu đồ pin trực tiếp lên UI |

---

### 3.2. Nhóm 2: Kết hợp Win32 P/Invoke & Native C++/FFmpeg
*Các tính năng can thiệp sâu cấp hệ điều hành hoặc xử lý luồng media nặng:*

| Chức năng | Phương pháp tích hợp | Chi tiết kỹ thuật |
| :--- | :--- | :--- |
| **Auto Clicker & Dừng khẩn cấp** | **Win32 P/Invoke** (`user32.dll`) | Sử dụng `SendInput` hoặc `mouse_event`, `SetCursorPos`, `GetCursorPos` và `GetAsyncKeyState(VK_ESCAPE / VK_F6)` với Global Hook / Background Polling |
| **Spam Text & Clipboard** | **Win32 P/Invoke** + `System.Windows.Clipboard` | `SetDataObject` với text Unicode và gửi phím `Ctrl + V` qua `keybd_event` / `SendInput` |
| **Xử lý Media (Nén, Tăng tốc, Cắt ghép)** | **FFmpeg Process Wrapper (C#)** | Gọi `ffmpeg.exe` ngầm qua `ProcessStartInfo` (Redirect StandardOutput/Error) để đọc dòng `time=...` và tính tiến trình % render hiển thị lên ProgressBar của WPF |
| **Khôi phục mạng & Sửa lỗi Winsock/WinNAT** | **Process Admin Execution (C#)** | Chạy tập lệnh netsh, ipconfig, powershell dưới quyền Administrator thông qua UAC |
| **Giấu file vào Ảnh / Video (Steganography)** | **C# Binary Stream Logic** | Mở luồng `FileStream`, ghi nhúng dữ liệu container Zip/Payload vào cuối file ảnh/video và đọc lại bằng Header/Signature Detection |
| **Job Object quản lý tiến trình con** | **Win32 P/Invoke** (`kernel32.dll`) | Gọi `CreateJobObject`, `AssignProcessToJobObject` để đảm bảo app tắt thì mọi tiến trình con (FFmpeg, PowerShell) đều tắt sạch |

---

## 4. Kiến trúc Đề xuất cho Ứng dụng C# WPF (`CMD_BOX_GUI`)

```text
CMD_BOX_GUI/
├── App.xaml / App.xaml.cs                # Entry Point & Global Resource Theme
├── MainWindow.xaml / MainWindow.xaml.cs  # Giao diện chính (Navigation Sidebar & Tab Content)
├── Core/                                 # Lớp xử lý hệ thống & Native P/Invoke
│   ├── NativeMethods.cs                  # P/Invoke Win32 API (User32, Kernel32, IPHLPAPI, JobObjects)
│   ├── ProcessRunner.cs                  # Trình thực thi lệnh CMD/PowerShell/FFmpeg bất đồng bộ & bắt Stream Log
│   └── AdminHelper.cs                    # Kiểm tra và yêu cầu nâng quyền Administrator
├── Services/                             # Các dịch vụ xử lý logic theo module
│   ├── SystemOptimizerService.cs         # Dọn rác nhanh, dọn chuyên sâu, Dev Cache, Tweaks
│   ├── NetworkService.cs                 # Thông tin mạng, Wi-Fi Audit, Network Repair, Firewall & Security
│   ├── UtilityService.cs                 # Auto Clicker, Spam Text, Auto Paste, Cài app, Pin Laptop
│   ├── MediaService.cs                   # Tự phát hiện GPU, Wrapper FFmpeg, Nén/Làm nét/Đổi đuôi, Giấu file
│   └── WindowsServiceManager.cs          # Quản lý & Tắt/Bật Services Windows, Startup Apps
├── ViewModels/                           # MVVM ViewModels (Nếu sử dụng mô hình MVVM)
│   ├── MainViewModel.cs                  # Trạng thái tổng quát, CPU/RAM/Disk Info, Theme Switcher
│   ├── OptimizerViewModel.cs             # Điều khiển tiến trình dọn dẹp & hiển thị dung lượng giải phóng
│   ├── NetworkViewModel.cs               # Trực quan hóa danh sách Wi-Fi & trạng thái bảo mật
│   ├── ToolsViewModel.cs                 # Tùy chỉnh tham số Auto Click / Battery Gauge
│   └── MediaViewModel.cs                 # Kéo thả file, chọn độ phân giải, thanh tiến trình FFmpeg
└── UI/                                   # Các UserControl / Views tương ứng với từng Tab
    ├── Views/
    │   ├── DashboardView.xaml            # Tổng quan máy tính, CPU/RAM Gauge, 1-Click Clean
    │   ├── OptimizerView.xaml            # Quản lý dọn rác, Registry, Services, Taskbar
    │   ├── NetworkView.xaml              # Quản lý mạng, Wi-Fi Password List, Sửa lỗi mạng
    │   ├── UtilityView.xaml              # Auto Clicker, Spam Text, Báo cáo Pin Laptop
    │   └── MediaView.xaml                # Xử lý Video/Audio/Ảnh (Hỗ trợ Kéo & Thả Drag & Drop)
    └── Styles/                           # Bộ màu, Dark/Light Mode, Card Style, Button Animation
```

---

## 5. Lộ trình triển khai nâng cấp

1. **Giai đoạn 1 (Thiết kế Khung giao diện WPF Dashboard):**
   - Thiết kế giao diện hiện đại với thanh điều hướng (Sidebar Navigation) gồm các mục: *Tổng quan (Dashboard)*, *Tối ưu hệ thống (Optimizer)*, *Mạng & Bảo mật (Network)*, *Tiện ích & Tự động (Utilities)*, *Xử lý Media (Media Center)* và *Terminal Log Box*.
   - Tích hợp ô hiển thị trạng thái hệ thống theo thời gian thực (Dung lượng ổ C, Tình trạng sạc Pin, Trạng thái quyền Admin).

2. **Giai đoạn 2 (Hiện thực hóa Module Tối ưu & Mạng bằng C#):**
   - Chuyển toàn bộ logic Dọn rác, Quản lý Startup, Quản lý Services sang C# `SystemOptimizerService`.
   - Viết tính năng đọc thông tin mạng và audit mật khẩu Wi-Fi hiển thị lên DataGrid có nút Copy mật khẩu nhanh.

3. **Giai đoạn 3 (Hiện thực hóa Tiện ích Auto Click & Pin Laptop):**
   - Tích hợp Native Win32 `SendInput` cho Auto Click / Spam Text với giao diện nhập liệu trực quan và phím nóng hủy tác vụ.
   - Thiết kế biểu đồ % Sức khỏe pin (Battery Health & Wear Level) và thông số chu kỳ sạc từ `powercfg /batteryreport`.

4. **Giai đoạn 4 (Tích hợp Bộ xử lý Media FFmpeg & Steganography):**
   - Hỗ trợ Kéo - Thả file video/ảnh vào ứng dụng.
   - Nhận diện GPU NVIDIA/Intel/AMD tự động và gọi FFmpeg ngầm với thanh tiến trình mượt mà.
   - Hoàn thiện tính năng giấu/trích xuất file bí mật trong ảnh/video.

---
*Tài liệu được khởi tạo tự động cho dự án **CMD_BOX_GUI**.*
