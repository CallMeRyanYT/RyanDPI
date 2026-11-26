using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace RyanDPI.Services
{
    /// <summary>
    /// Lightweight Win32 Service Control Manager (SCM) host allowing RyanDPI.exe --service-usque
    /// to run usque.exe (Cloudflare WARP MASQUE SOCKS5 TCP+UDP tunnel) cleanly as the RyanDpiEngine
    /// Windows service with zero third-party service wrappers (no NSSM) and instant stop/restart.
    /// </summary>
    public static class WindowsServiceHost
    {
        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_START_PENDING = 0x00000002;
        private const uint SERVICE_STOP_PENDING = 0x00000003;
        private const uint SERVICE_RUNNING = 0x00000004;
        private const uint SERVICE_STOPPED = 0x00000001;
        private const uint SERVICE_ACCEPT_STOP = 0x00000001;
        private const uint SERVICE_ACCEPT_SHUTDOWN = 0x00000004;
        private const uint SERVICE_CONTROL_STOP = 0x00000001;
        private const uint SERVICE_CONTROL_INTERROGATE = 0x00000004;
        private const uint SERVICE_CONTROL_SHUTDOWN = 0x00000005;

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        private delegate void ServiceMainDelegate(int argc, IntPtr argv);
        private delegate uint ServiceCtrlHandlerExDelegate(uint dwControl, uint dwEventType, IntPtr lpEventData, IntPtr lpContext);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SERVICE_TABLE_ENTRY
        {
            public string? lpServiceName;
            public ServiceMainDelegate? lpServiceProc;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool StartServiceCtrlDispatcher([In] SERVICE_TABLE_ENTRY[] lpServiceTable);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr RegisterServiceCtrlHandlerEx(
            string lpServiceName, ServiceCtrlHandlerExDelegate lpHandlerProc, IntPtr lpContext);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);

        private static readonly ManualResetEventSlim StopEvent = new(false);
        private static IntPtr _statusHandle = IntPtr.Zero;
        private static SERVICE_STATUS _status;
        private static ServiceCtrlHandlerExDelegate? _ctrlHandlerRef;
        private static ServiceMainDelegate? _serviceMainRef;
        private static string _usqueExePath = string.Empty;
        private static string _usqueConfigPath = string.Empty;
        private static int _usquePort = 1082;
        private static Process? _childProcess;
        private static readonly object ChildLock = new();

        public static int RunUsqueService(string usqueExePath, string configPath, int port)
        {
            _usqueExePath = usqueExePath;
            _usqueConfigPath = configPath;
            _usquePort = port > 0 ? port : 1082;

            _serviceMainRef = ServiceMain;
            var table = new[]
            {
                new SERVICE_TABLE_ENTRY
                {
                    lpServiceName = RyanDpiEngineService.EngineServiceName,
                    lpServiceProc = _serviceMainRef
                },
                new SERVICE_TABLE_ENTRY
                {
                    lpServiceName = null,
                    lpServiceProc = null
                }
            };

            return StartServiceCtrlDispatcher(table) ? 0 : Marshal.GetLastWin32Error();
        }

        private static void ServiceMain(int argc, IntPtr argv)
        {
            _ctrlHandlerRef = ServiceCtrlHandler;
            _statusHandle = RegisterServiceCtrlHandlerEx(RyanDpiEngineService.EngineServiceName, _ctrlHandlerRef, IntPtr.Zero);
            if (_statusHandle == IntPtr.Zero) return;

            _status = new SERVICE_STATUS
            {
                dwServiceType = SERVICE_WIN32_OWN_PROCESS,
                dwCurrentState = SERVICE_START_PENDING,
                dwControlsAccepted = 0,
                dwWaitHint = 5000
            };
            SetServiceStatus(_statusHandle, ref _status);

            var workerThread = new Thread(SuperviseUsqueLoop) { IsBackground = true };
            workerThread.Start();

            _status.dwCurrentState = SERVICE_RUNNING;
            _status.dwControlsAccepted = SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN;
            _status.dwWaitHint = 0;
            SetServiceStatus(_statusHandle, ref _status);

            StopEvent.Wait();

            _status.dwCurrentState = SERVICE_STOP_PENDING;
            _status.dwControlsAccepted = 0;
            _status.dwWaitHint = 4000;
            SetServiceStatus(_statusHandle, ref _status);

            KillChildProcess();
            workerThread.Join(3000);

            _status.dwCurrentState = SERVICE_STOPPED;
            _status.dwWaitHint = 0;
            SetServiceStatus(_statusHandle, ref _status);
        }

        private static uint ServiceCtrlHandler(uint dwControl, uint dwEventType, IntPtr lpEventData, IntPtr lpContext)
        {
            switch (dwControl)
            {
                case SERVICE_CONTROL_STOP:
                case SERVICE_CONTROL_SHUTDOWN:
                    StopEvent.Set();
                    KillChildProcess();
                    return 0;
                case SERVICE_CONTROL_INTERROGATE:
                    SetServiceStatus(_statusHandle, ref _status);
                    return 0;
                default:
                    return 0;
            }
        }

        public static bool IsValidUsqueConfig(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var info = new FileInfo(path);
                if (info.Length < 50) return false;
                var txt = File.ReadAllText(path);
                return txt.Contains("\"private_key\"", StringComparison.OrdinalIgnoreCase) ||
                       txt.Contains("\"endpoint", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void SuperviseUsqueLoop()
        {
            var workDir = Path.GetDirectoryName(_usqueConfigPath) ?? Path.GetDirectoryName(_usqueExePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            Directory.CreateDirectory(workDir);

            // Auto-register free Cloudflare WARP MASQUE identity if config.json is missing or corrupted
            if (!IsValidUsqueConfig(_usqueConfigPath) && File.Exists(_usqueExePath))
            {
                try { if (File.Exists(_usqueConfigPath)) File.Delete(_usqueConfigPath); } catch { }
                try
                {
                    var regPsi = new ProcessStartInfo
                    {
                        FileName = _usqueExePath,
                        Arguments = $"register -a -c \"{_usqueConfigPath}\"",
                        WorkingDirectory = workDir,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var regProc = Process.Start(regPsi);
                    regProc?.WaitForExit(20000);
                }
                catch
                {
                    // Will retry in loop below
                }
            }

            int consecutiveFailures = 0;
            while (!StopEvent.IsSet)
            {
                try
                {
                    if (!File.Exists(_usqueExePath))
                    {
                        StopEvent.Wait(5000);
                        continue;
                    }

                    if (!IsValidUsqueConfig(_usqueConfigPath))
                    {
                        try { if (File.Exists(_usqueConfigPath)) File.Delete(_usqueConfigPath); } catch { }
                        var regPsi = new ProcessStartInfo
                        {
                            FileName = _usqueExePath,
                            Arguments = $"register -a -c \"{_usqueConfigPath}\"",
                            WorkingDirectory = workDir,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var regProc = Process.Start(regPsi);
                        regProc?.WaitForExit(20000);
                    }

                    // Alternate HTTP/3 MASQUE (fast UDP/443) and HTTP/2 TCP/443 fallback if QUIC is blocked
                    bool useHttp2Fallback = consecutiveFailures >= 2 && (consecutiveFailures % 2 == 0);
                    string extraFlags = useHttp2Fallback ? " --http2" : "";

                    var psi = new ProcessStartInfo
                    {
                        FileName = _usqueExePath,
                        Arguments = $"socks -c \"{_usqueConfigPath}\" -b 127.0.0.1 -p {_usquePort} --always-reconnect --udp-timeout 180s{extraFlags}",
                        WorkingDirectory = workDir,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Process? proc;
                    lock (ChildLock)
                    {
                        if (StopEvent.IsSet) break;
                        proc = Process.Start(psi);
                        _childProcess = proc;
                    }

                    if (proc != null)
                    {
                        var startTick = Environment.TickCount64;
                        while (!StopEvent.IsSet && !proc.HasExited)
                        {
                            StopEvent.Wait(500);
                        }

                        if (!StopEvent.IsSet)
                        {
                            var ranMs = Environment.TickCount64 - startTick;
                            consecutiveFailures = ranMs > 15000 ? 0 : consecutiveFailures + 1;
                        }
                    }
                }
                catch
                {
                    consecutiveFailures++;
                }
                finally
                {
                    KillChildProcess();
                }

                if (!StopEvent.IsSet)
                {
                    StopEvent.Wait(Math.Min(10000, 1500 + consecutiveFailures * 1000));
                }
            }
        }

        private static void KillChildProcess()
        {
            lock (ChildLock)
            {
                if (_childProcess != null)
                {
                    try
                    {
                        if (!_childProcess.HasExited)
                        {
                            _childProcess.Kill(entireProcessTree: true);
                            _childProcess.WaitForExit(2500);
                        }
                    }
                    catch
                    {
                        // Ignore exit race
                    }
                    finally
                    {
                        try { _childProcess.Dispose(); } catch { }
                        _childProcess = null;
                    }
                }
            }
        }
    }
}
