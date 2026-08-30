using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
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
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
                    Verb = "runas"
                });
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error($"Không thể khởi động với quyền Admin: {ex.Message}");
            }
        }

        public static bool CheckEmergencyStop()
        {
            return (NativeMethods.GetAsyncKeyState(NativeMethods.VK_ESCAPE) & 0x8000) != 0 ||
                   (NativeMethods.GetAsyncKeyState(NativeMethods.VK_F6) & 0x8000) != 0;
        }

        public static async Task<int> RunAdminCmdAsync(string cmdCommand, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cmdCommand)) return 0;
            return await ProcessRunner.RunProcessAsync(
                "cmd.exe",
                $"/c {cmdCommand}",
                onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[CMD] {line}"); },
                onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[CMD] {err}"); },
                cancellationToken: cancellationToken,
                runAsAdmin: true
            );
        }

        /// <summary>
        /// Đóng gói toàn bộ chuỗi lệnh vào 1 file kịch bản .bat tạm và thực thi 1 lần duy nhất với quyền Admin
        /// </summary>
        public static async Task<int> RunBatchScriptAsync(IEnumerable<string> commands, string scriptName = "script", CancellationToken cancellationToken = default)
        {
            string tempBat = Path.Combine(Path.GetTempPath(), $"cmd_box_{scriptName}_{Guid.NewGuid():N}.bat");
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("chcp 65001 >nul");
                foreach (var cmd in commands)
                {
                    if (!string.IsNullOrWhiteSpace(cmd))
                    {
                        sb.AppendLine(cmd);
                    }
                }
                await File.WriteAllTextAsync(tempBat, sb.ToString(), new UTF8Encoding(false), cancellationToken);

                return await ProcessRunner.RunProcessAsync(
                    "cmd.exe",
                    $"/c \"{tempBat}\"",
                    onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[{scriptName}] {line}"); },
                    onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[{scriptName}] {err}"); },
                    cancellationToken: cancellationToken,
                    runAsAdmin: true
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"Lỗi khi chạy batch script [{scriptName}]: {ex.Message}");
                return -1;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempBat)) File.Delete(tempBat);
                }
                catch { }
            }
        }

        /// <summary>
        /// Đóng gói toàn bộ chuỗi lệnh vào 1 file kịch bản .ps1 tạm và thực thi 1 lần duy nhất với quyền Admin
        /// </summary>
        public static async Task<int> RunPowerShellScriptAsync(string psScript, string scriptName = "ps_script", CancellationToken cancellationToken = default)
        {
            string tempPs1 = Path.Combine(Path.GetTempPath(), $"cmd_box_{scriptName}_{Guid.NewGuid():N}.ps1");
            try
            {
                await File.WriteAllTextAsync(tempPs1, psScript, new UTF8Encoding(false), cancellationToken);

                return await ProcessRunner.RunProcessAsync(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{tempPs1}\"",
                    onOutputLine: line => { if (!string.IsNullOrWhiteSpace(line)) Logger.Info($"[{scriptName}] {line}"); },
                    onErrorLine: err => { if (!string.IsNullOrWhiteSpace(err)) Logger.Warning($"[{scriptName}] {err}"); },
                    cancellationToken: cancellationToken,
                    runAsAdmin: true
                );
            }
            catch (Exception ex)
            {
                Logger.Error($"Lỗi khi chạy powershell script [{scriptName}]: {ex.Message}");
                return -1;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPs1)) File.Delete(tempPs1);
                }
                catch { }
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
            var inputs = new NativeMethods.INPUT[]
            {
                new() { type = NativeMethods.INPUT_MOUSE, mi = new() { dwFlags = NativeMethods.MOUSEEVENTF_LEFTDOWN } },
                new() { type = NativeMethods.INPUT_MOUSE, mi = new() { dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP } }
            };
            NativeMethods.SendInput((uint)inputs.Length, inputs, MarshalSize());
        }

        public static void SimulateCtrlV()
        {
            var inputs = new NativeMethods.INPUT[]
            {
                new() { type = NativeMethods.INPUT_KEYBOARD, ki = new() { wVk = NativeMethods.VK_CONTROL, dwFlags = NativeMethods.KEYEVENTF_KEYDOWN } },
                new() { type = NativeMethods.INPUT_KEYBOARD, ki = new() { wVk = NativeMethods.VK_V, dwFlags = NativeMethods.KEYEVENTF_KEYDOWN } },
                new() { type = NativeMethods.INPUT_KEYBOARD, ki = new() { wVk = NativeMethods.VK_V, dwFlags = NativeMethods.KEYEVENTF_KEYUP } },
                new() { type = NativeMethods.INPUT_KEYBOARD, ki = new() { wVk = NativeMethods.VK_CONTROL, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            };
            NativeMethods.SendInput((uint)inputs.Length, inputs, MarshalSize());
        }

        public static void SimulateEnter()
        {
            var inputs = new NativeMethods.INPUT[]
            {
                new() { type = NativeMethods.INPUT_KEYBOARD, ki = new() { wVk = NativeMethods.VK_RETURN, dwFlags = NativeMethods.KEYEVENTF_KEYDOWN } },
                new() { type = NativeMethods.INPUT_KEYBOARD, ki = new() { wVk = NativeMethods.VK_RETURN, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            };
            NativeMethods.SendInput((uint)inputs.Length, inputs, MarshalSize());
        }

        public static async Task RestartExplorerAsync()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("explorer"))
                {
                    try { p.Kill(); await p.WaitForExitAsync(); } catch { }
                }
            }
            catch { }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = true });
            }
            catch { }
        }

        private static int MarshalSize() => Marshal.SizeOf<NativeMethods.INPUT>();
    }
}
