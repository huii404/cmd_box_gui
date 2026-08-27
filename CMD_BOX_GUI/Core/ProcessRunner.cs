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
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (runAsAdmin && !SystemCore.IsAdministrator())
            {
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.RedirectStandardOutput = false;
                psi.RedirectStandardError = false;
                psi.CreateNoWindow = false;
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
