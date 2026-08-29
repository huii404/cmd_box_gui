using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CMD_BOX_GUI.Core
{
    public static class ProcessRunner
    {
        public static async Task<int> RunProcessAsync(
            string fileName,
            string arguments,
            Action<string>? onOutputLine = null,
            Action<string>? onErrorLine = null,
            CancellationToken cancellationToken = default,
            bool runAsAdmin = false)
        {
            bool elevate = runAsAdmin && !SystemCore.IsAdministrator();

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments
            };

            if (elevate)
            {
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else
            {
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
            }

            using var process = new Process { StartInfo = psi };

            if (!psi.UseShellExecute)
            {
                if (onOutputLine != null)
                {
                    process.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data != null) onOutputLine(e.Data);
                    };
                }

                if (onErrorLine != null)
                {
                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data != null) onErrorLine(e.Data);
                    };
                }
            }

            try
            {
                process.Start();

                if (!psi.UseShellExecute)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                await process.WaitForExitAsync(cancellationToken);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch { }
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Lỗi thực thi lệnh [{fileName}]: {ex.Message}");
                return -1;
            }
        }

        public static async Task<string> RunCommandAndGetOutputAsync(string command, string arguments = "")
        {
            var sb = new StringBuilder();
            await RunProcessAsync(
                command,
                arguments,
                line => sb.AppendLine(line),
                line => sb.AppendLine(line));
            return sb.ToString().Trim();
        }
    }
}
