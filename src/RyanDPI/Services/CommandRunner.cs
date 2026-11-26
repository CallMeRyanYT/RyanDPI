using System.Diagnostics;
using System.Text;

namespace RyanDPI.Services
{
    public readonly record struct CommandResult(int ExitCode, string StdOut, string StdErr)
    {
        public string CombinedOutput => string.IsNullOrEmpty(StdErr)
            ? StdOut
            : string.IsNullOrEmpty(StdOut) ? StdErr : $"{StdOut}\n{StdErr}";
    }

    public static class CommandRunner
    {
        public static CommandResult Run(string fileName, string arguments, int timeoutMs = 45000, string? workingDirectory = null)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    startInfo.WorkingDirectory = workingDirectory;
                }

                using var process = new Process { StartInfo = startInfo };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) stderr.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return new CommandResult(-1, stdout.ToString().Trim(), "Command timed out.");
                }

                // Ensure async output buffers drain completely
                process.WaitForExit();
                return new CommandResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
            }
            catch (Exception ex)
            {
                return new CommandResult(-1, string.Empty, ex.Message);
            }
        }

        public static CommandResult RunPowerShell(string script, int timeoutMs = 45000)
        {
            var bytes = Encoding.Unicode.GetBytes(script);
            var encoded = Convert.ToBase64String(bytes);
            return Run("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", timeoutMs);
        }
    }
}
