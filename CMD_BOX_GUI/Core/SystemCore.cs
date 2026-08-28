using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace CMD_BOX_GUI.Core
{
    public static class SystemCore
    {
        public static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static void RestartAsAdmin()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error($"Không thể khởi động lại với quyền Administrator: {ex.Message}");
            }
        }

        public static bool CheckEmergencyStop()
        {
            // Kiểm tra phím ESC hoặc F6 có đang được nhấn
            return (NativeMethods.GetAsyncKeyState(NativeMethods.VK_ESCAPE) & 0x8000) != 0 ||
                   (NativeMethods.GetAsyncKeyState(NativeMethods.VK_F6) & 0x8000) != 0;
        }

        /// <summary>
        /// Thực thi trực tiếp lệnh CMD với quyền Administrator và truyền log vào View Console trong thời gian thực (không cần hộp thoại xác nhận).
        /// Tự động kiểm tra cơ chế chống lạm dụng (công tắc AllowAdminCmd).
        /// </summary>
        public static async System.Threading.Tasks.Task<int> RunAdminCmdAsync(string cmdCommand, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cmdCommand)) return 0;

            // Kiểm tra cơ chế bảo vệ chống lạm dụng
            if (!Services.SettingsService.Current.AllowAdminCmd)
            {
                Logger.Warning($"🔒 [BẢO VỆ CHỐNG LẠM DỤNG] Lệnh Admin CMD đã bị chặn: '{cmdCommand}'.");
                Logger.Warning("💡 Hãy bật công tắc 'Lệnh Admin' ở góc dưới bên trái nếu muốn cho phép thực thi.");
                return -1;
            }

            Logger.Info($"⚡ [ADMIN CMD] Đang thực thi: {cmdCommand}");

            try
            {
                int exitCode = await ProcessRunner.RunProcessAsync(
                    "cmd.exe",
                    $"/c {cmdCommand}",
                    onOutputLine: line =>
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            Logger.Info($"[CMD] {line}");
                    },
                    onErrorLine: err =>
                    {
                        if (!string.IsNullOrWhiteSpace(err))
                            Logger.Warning($"[CMD] {err}");
                    },
                    cancellationToken: cancellationToken,
                    runAsAdmin: true
                );

                if (exitCode == 0)
                {
                    Logger.Success($"✅ [ADMIN CMD] Hoàn tất thành công (Exit Code: {exitCode}).");
                }
                else
                {
                    Logger.Warning($"⚠️ [ADMIN CMD] Kết thúc với mã: {exitCode}.");
                }

                return exitCode;
            }
            catch (System.OperationCanceledException)
            {
                Logger.Warning($"🛑 [ADMIN CMD] Đã bị hủy bởi người dùng: {cmdCommand}");
                return -2;
            }
            catch (Exception ex)
            {
                Logger.Error($"❌ [ADMIN CMD] Lỗi thực thi: {ex.Message}");
                return -3;
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {suffixes[order]}";
        }

        public static void SimulateLeftClick()
        {
            var inputs = new NativeMethods.INPUT[2];
            inputs[0].type = NativeMethods.INPUT_MOUSE;
            inputs[0].mi.dwFlags = NativeMethods.MOUSEEVENTF_LEFTDOWN;

            inputs[1].type = NativeMethods.INPUT_MOUSE;
            inputs[1].mi.dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP;

            NativeMethods.SendInput((uint)inputs.Length, inputs, MarshalSize());
        }

        public static void SimulateCtrlV()
        {
            var inputs = new NativeMethods.INPUT[4];
            
            // Ctrl Down
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].ki.wVk = NativeMethods.VK_CONTROL;
            inputs[0].ki.dwFlags = NativeMethods.KEYEVENTF_KEYDOWN;

            // V Down
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].ki.wVk = NativeMethods.VK_V;
            inputs[1].ki.dwFlags = NativeMethods.KEYEVENTF_KEYDOWN;

            // V Up
            inputs[2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[2].ki.wVk = NativeMethods.VK_V;
            inputs[2].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            // Ctrl Up
            inputs[3].type = NativeMethods.INPUT_KEYBOARD;
            inputs[3].ki.wVk = NativeMethods.VK_CONTROL;
            inputs[3].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            NativeMethods.SendInput((uint)inputs.Length, inputs, MarshalSize());
        }

        public static void SimulateEnter()
        {
            var inputs = new NativeMethods.INPUT[2];

            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].ki.wVk = NativeMethods.VK_RETURN;
            inputs[0].ki.dwFlags = NativeMethods.KEYEVENTF_KEYDOWN;

            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].ki.wVk = NativeMethods.VK_RETURN;
            inputs[1].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            NativeMethods.SendInput((uint)inputs.Length, inputs, MarshalSize());
        }

        private static int MarshalSize()
        {
            return System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>();
        }
    }
}
