using System.IO;
using System.Text;

namespace RyanDPI.Services
{
    public static class LogService
    {
        private static readonly object SyncLock = new();
        private static readonly List<string> RecentLines = new();
        private const int MaxBufferedLines = 400;

        public static event Action<string>? LineLogged;

        public static string LogsDirectory
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RyanDPI",
                    "Logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string LogFilePath => Path.Combine(LogsDirectory, "ryandpi.log");

        public static void Initialize()
        {
            try
            {
                var path = LogFilePath;
                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                {
                    var backup = Path.Combine(LogsDirectory, "ryandpi_prev.log");
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }

                Info("RyanDPI v2.0.0 started.");
            }
            catch
            {
                // Ignore logging init failure
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERR ", message);

        private static void Write(string level, string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
            lock (SyncLock)
            {
                RecentLines.Add(line);
                if (RecentLines.Count > MaxBufferedLines)
                {
                    RecentLines.RemoveAt(0);
                }

                try
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Ignore file I/O lock contention
                }
            }

            LineLogged?.Invoke(line);
        }

        public static string GetRecentLogsText()
        {
            lock (SyncLock)
            {
                return string.Join(Environment.NewLine, RecentLines);
            }
        }

        public static void ClearBuffer()
        {
            lock (SyncLock)
            {
                RecentLines.Clear();
            }
        }
    }
}
