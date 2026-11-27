using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace RyanDPI.Services
{
    /// <summary>
    /// Lightweight Win32 Service Control Manager (SCM) host allowing RyanDPI.exe --service-usque
    /// to run usque.exe (Cloudflare WARP MASQUE SOCKS5 TCP+UDP tunnel) cleanly as the RyanDpiEngine
    /// Windows service with:
    ///   1. An RFC-1928 compliant local SOCKS5 UDP-Fix Bridge on 127.0.0.1:1082 -> usque on 127.0.0.1:1083.
    ///      ProxiFyre sends UDP ASSOCIATE with 0.0.0.0:0 (RFC 1928 wildcard), which usque v4.2.1's
    ///      strict associated-UDP check drops ("udp address ... is not associated with tcp"), breaking
    ///      Discord's DNS/QUIC/Voice/Go Live! The bridge binds a real ephemeral UDP port per association
    ///      and registers that exact 127.0.0.1:port with usque so 100% of ProxiFyre TCP & UDP works.
    ///   2. Live MASQUE connection health monitoring with automatic HTTP/3 (UDP/443) -> HTTP/2 (TCP/443)
    ///      fallback when an ISP drops QUIC UDP/443.
    ///   3. Rotating usque.log diagnostics next to usque.exe.
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
        private static int _internalUsquePort = 1083;
        private static Process? _childProcess;
        private static readonly object ChildLock = new();
        private static readonly object LogLock = new();
        private static string _usqueLogPath = string.Empty;
        private static UsqueSocks5Bridge? _bridge;
        private static long _lastNetChangeTick = 0;
        private static volatile bool _networkRebindRequested = false;

        public static int RunUsqueService(string usqueExePath, string configPath, int port)
        {
            _usqueExePath = usqueExePath;
            _usqueConfigPath = configPath;
            _usquePort = port > 0 ? port : 1082;
            _internalUsquePort = _usquePort + 1;
            var workDir = Path.GetDirectoryName(_usqueConfigPath) ?? Path.GetDirectoryName(_usqueExePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            _usqueLogPath = Path.Combine(workDir, "usque.log");

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

            try
            {
                _bridge = new UsqueSocks5Bridge(_usquePort, _internalUsquePort, WriteUsqueLog);
                _bridge.Start();
            }
            catch (Exception ex)
            {
                WriteUsqueLog($"[bridge] Failed to start local SOCKS5 UDP-fix bridge on 127.0.0.1:{_usquePort}: {ex.Message}");
            }

            var workerThread = new Thread(SuperviseUsqueLoop) { IsBackground = true };
            workerThread.Start();

            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

            _status.dwCurrentState = SERVICE_RUNNING;
            _status.dwControlsAccepted = SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN;
            _status.dwWaitHint = 0;
            SetServiceStatus(_statusHandle, ref _status);

            StopEvent.Wait();

            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;

            _status.dwCurrentState = SERVICE_STOP_PENDING;
            _status.dwControlsAccepted = 0;
            _status.dwWaitHint = 4000;
            SetServiceStatus(_statusHandle, ref _status);

            try { _bridge?.Stop(); } catch { }
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
                    try { _bridge?.Stop(); } catch { }
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

        private static void WriteUsqueLog(string line)
        {
            if (string.IsNullOrWhiteSpace(_usqueLogPath) || string.IsNullOrWhiteSpace(line)) return;
            try
            {
                lock (LogLock)
                {
                    var dir = Path.GetDirectoryName(_usqueLogPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    // Cap log size at ~128 KB so disk usage stays negligible
                    if (File.Exists(_usqueLogPath) && new FileInfo(_usqueLogPath).Length > 131072)
                    {
                        var lines = File.ReadAllLines(_usqueLogPath).TakeLast(120);
                        File.WriteAllLines(_usqueLogPath, lines);
                    }
                    File.AppendAllText(_usqueLogPath, $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
                }
            }
            catch
            {
                // Non-fatal
            }
        }

        private static void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            if (StopEvent.IsSet) return;
            var now = Environment.TickCount64;
            var prev = Interlocked.Exchange(ref _lastNetChangeTick, now);
            if (now - prev >= 2500)
            {
                _networkRebindRequested = true;
            }
        }

        private static void SuperviseUsqueLoop()
        {
            var workDir = Path.GetDirectoryName(_usqueConfigPath) ?? Path.GetDirectoryName(_usqueExePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            Directory.CreateDirectory(workDir);
            _usqueLogPath = Path.Combine(workDir, "usque.log");

            try
            {
                if (File.Exists(_usqueLogPath) && new FileInfo(_usqueLogPath).Length > 65536)
                {
                    File.Delete(_usqueLogPath);
                }
            }
            catch { }

            WriteUsqueLog($"=== RyanDpiEngine WARP MASQUE host started (bridge=127.0.0.1:{_usquePort} -> usque=127.0.0.1:{_internalUsquePort}) ===");

            // Auto-register free Cloudflare WARP MASQUE identity if config.json is missing or corrupted
            if (!IsValidUsqueConfig(_usqueConfigPath) && File.Exists(_usqueExePath))
            {
                RunUsqueRegistration(workDir);
            }

            int consecutiveFailures = 0;
            bool preferHttp2 = false;

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
                        RunUsqueRegistration(workDir);
                    }

                    // Alternate HTTP/3 MASQUE (fast UDP/443) and HTTP/2 TCP/443 fallback if QUIC is blocked
                    string extraFlags = preferHttp2 ? " --http2" : "";
                    WriteUsqueLog($"Launching usque socks (mode={(preferHttp2 ? "HTTP/2 TCP-443" : "HTTP/3 QUIC-443")}, port=127.0.0.1:{_internalUsquePort})...");

                    var psi = new ProcessStartInfo
                    {
                        FileName = _usqueExePath,
                        Arguments = $"socks -c \"{_usqueConfigPath}\" -b 127.0.0.1 -p {_internalUsquePort} --always-reconnect -k 10s --udp-timeout 180s{extraFlags}",
                        WorkingDirectory = workDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    int connectedFlag = 0;
                    Process? proc;
                    lock (ChildLock)
                    {
                        if (StopEvent.IsSet) break;
                        proc = new Process { StartInfo = psi };
                        proc.OutputDataReceived += (_, e) =>
                        {
                            if (e.Data == null) return;
                            WriteUsqueLog($"[usque] {e.Data}");
                            if (e.Data.Contains("Connected to MASQUE", StringComparison.OrdinalIgnoreCase))
                            {
                                Interlocked.Exchange(ref connectedFlag, 1);
                            }
                        };
                        proc.ErrorDataReceived += (_, e) =>
                        {
                            if (e.Data == null) return;
                            WriteUsqueLog($"[usque] {e.Data}");
                            if (e.Data.Contains("Connected to MASQUE", StringComparison.OrdinalIgnoreCase))
                            {
                                Interlocked.Exchange(ref connectedFlag, 1);
                            }
                        };
                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        _childProcess = proc;
                    }

                    if (proc != null)
                    {
                        var startTick = Environment.TickCount64;
                        bool timedOutWaitingForMasque = false;
                        bool restartedForNetworkChange = false;
                        _networkRebindRequested = false;

                        while (!StopEvent.IsSet && !proc.HasExited)
                        {
                            StopEvent.Wait(500);

                            // If VPN (e.g. Proton VPN) or Wi-Fi/Ethernet toggled, rebind usque cleanly onto the new default route!
                            if (_networkRebindRequested &&
                                Environment.TickCount64 - Volatile.Read(ref _lastNetChangeTick) >= 2000 &&
                                Environment.TickCount64 - startTick >= 4000)
                            {
                                _networkRebindRequested = false;
                                restartedForNetworkChange = true;
                                WriteUsqueLog("Network / VPN route change detected — rebinding usque WARP MASQUE cleanly...");
                                KillChildProcess();
                                break;
                            }

                            // If ISP or VPN blocks QUIC UDP/443, usque with --always-reconnect stays alive forever without connecting.
                            // Detect 12s without "Connected to MASQUE" and immediately flip to HTTP/2 TCP/443!
                            if (Volatile.Read(ref connectedFlag) == 0 &&
                                Environment.TickCount64 - startTick >= 12000)
                            {
                                timedOutWaitingForMasque = true;
                                WriteUsqueLog($"usque did not establish MASQUE tunnel within 12s in {(preferHttp2 ? "HTTP/2" : "HTTP/3")} mode — switching transport and restarting...");
                                KillChildProcess();
                                break;
                            }
                        }

                        if (!StopEvent.IsSet)
                        {
                            var ranMs = Environment.TickCount64 - startTick;
                            if (restartedForNetworkChange)
                            {
                                consecutiveFailures = 0;
                            }
                            else if (timedOutWaitingForMasque || Volatile.Read(ref connectedFlag) == 0)
                            {
                                consecutiveFailures++;
                                preferHttp2 = !preferHttp2;
                                // If both HTTP/3 and HTTP/2 failed back-to-back, refresh device key in case config.json went stale
                                if (consecutiveFailures >= 2 && consecutiveFailures % 2 == 0)
                                {
                                    WriteUsqueLog("Both HTTP/3 and HTTP/2 failed to connect — refreshing WARP MASQUE device registration...");
                                    RunUsqueRegistration(workDir);
                                }
                            }
                            else
                            {
                                consecutiveFailures = ranMs > 15000 ? 0 : consecutiveFailures + 1;
                                if (consecutiveFailures >= 2) preferHttp2 = !preferHttp2;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteUsqueLog($"Supervisor error: {ex.Message}");
                    consecutiveFailures++;
                    preferHttp2 = !preferHttp2;
                }
                finally
                {
                    KillChildProcess();
                }

                if (!StopEvent.IsSet)
                {
                    StopEvent.Wait(Math.Min(10000, 1200 + consecutiveFailures * 1000));
                }
            }
        }

        private static void RunUsqueRegistration(string workDir)
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
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var regProc = new Process { StartInfo = regPsi };
                regProc.OutputDataReceived += (_, e) => { if (e.Data != null) WriteUsqueLog($"[register] {e.Data}"); };
                regProc.ErrorDataReceived += (_, e) => { if (e.Data != null) WriteUsqueLog($"[register] {e.Data}"); };
                regProc.Start();
                regProc.BeginOutputReadLine();
                regProc.BeginErrorReadLine();
                if (!regProc.WaitForExit(20000))
                {
                    try { regProc.Kill(entireProcessTree: true); } catch { }
                }
            }
            catch (Exception ex)
            {
                WriteUsqueLog($"[register] exception: {ex.Message}");
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

    /// <summary>
    /// High-performance local SOCKS5 bridge (127.0.0.1:listenPort -> 127.0.0.1:upstreamPort) that
    /// fixes RFC-1928 wildcard UDP ASSOCIATE (0.0.0.0:0) sent by ProxiFyre so usque v4.2.1 accepts
    /// and relays 100% of UDP datagrams (DNS, QUIC/HTTP3, Discord Voice & Go Live Screen Share).
    /// </summary>
    public sealed class UsqueSocks5Bridge : IDisposable
    {
        private const int SIO_UDP_CONNRESET = -1744830452; // IOC_IN | IOC_VENDOR | 12
        private readonly int _listenPort;
        private readonly int _upstreamPort;
        private readonly Action<string>? _log;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;

        public UsqueSocks5Bridge(int listenPort, int upstreamPort, Action<string>? log = null)
        {
            _listenPort = listenPort;
            _upstreamPort = upstreamPort;
            _log = log;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, _listenPort);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start(256);
            _ = AcceptLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
        }

        public void Dispose()
        {
            Stop();
            try { _cts?.Dispose(); } catch { }
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(ct);
                    client.NoDelay = true;
                    _ = Task.Run(() => HandleClientAsync(client, ct), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    if (!ct.IsCancellationRequested)
                    {
                        await Task.Delay(50, ct).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                try
                {
                    var clientStream = client.GetStream();

                    // 1. SOCKS5 client greeting: VER, NMETHODS, METHODS[NMETHODS]
                    var hdr = new byte[2];
                    if (!await ReadExactAsync(clientStream, hdr, 0, 2, ct)) return;
                    if (hdr[0] != 0x05) return;

                    int nMethods = hdr[1];
                    if (nMethods > 0)
                    {
                        var methods = new byte[nMethods];
                        if (!await ReadExactAsync(clientStream, methods, 0, nMethods, ct)) return;
                    }

                    // Select NO AUTHENTICATION REQUIRED (0x00)
                    await clientStream.WriteAsync(new byte[] { 0x05, 0x00 }, ct);

                    // 2. SOCKS5 request: VER, CMD, RSV, ATYP, DST.ADDR, DST.PORT
                    var reqHead = new byte[4];
                    if (!await ReadExactAsync(clientStream, reqHead, 0, 4, ct)) return;
                    if (reqHead[0] != 0x05) return;

                    byte cmd = reqHead[1];
                    byte atyp = reqHead[3];

                    byte[] addrPart;
                    if (atyp == 0x01) // IPv4
                    {
                        addrPart = new byte[4];
                        if (!await ReadExactAsync(clientStream, addrPart, 0, 4, ct)) return;
                    }
                    else if (atyp == 0x04) // IPv6
                    {
                        addrPart = new byte[16];
                        if (!await ReadExactAsync(clientStream, addrPart, 0, 16, ct)) return;
                    }
                    else if (atyp == 0x03) // Domain
                    {
                        var lenBuf = new byte[1];
                        if (!await ReadExactAsync(clientStream, lenBuf, 0, 1, ct)) return;
                        int dlen = lenBuf[0];
                        addrPart = new byte[1 + dlen];
                        addrPart[0] = lenBuf[0];
                        if (dlen > 0 && !await ReadExactAsync(clientStream, addrPart, 1, dlen, ct)) return;
                    }
                    else
                    {
                        await SendErrorReplyAsync(clientStream, 0x08, ct); // Address type not supported
                        return;
                    }

                    var portPart = new byte[2];
                    if (!await ReadExactAsync(clientStream, portPart, 0, 2, ct)) return;

                    if (cmd == 0x01) // TCP CONNECT
                    {
                        await HandleTcpConnectAsync(clientStream, reqHead, addrPart, portPart, ct);
                    }
                    else if (cmd == 0x03) // UDP ASSOCIATE (rewrite 0.0.0.0:0 -> exact bound 127.0.0.1:<port> for usque!)
                    {
                        await HandleUdpAssociateAsync(clientStream, ct);
                    }
                    else
                    {
                        await SendErrorReplyAsync(clientStream, 0x07, ct); // Command not supported
                    }
                }
                catch
                {
                    // Connection closed or reset
                }
            }
        }

        private async Task HandleTcpConnectAsync(
            NetworkStream clientStream, byte[] reqHead, byte[] addrPart, byte[] portPart, CancellationToken ct)
        {
            using var upstream = new TcpClient { NoDelay = true };
            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(8000);
                await upstream.ConnectAsync(IPAddress.Loopback, _upstreamPort, connectCts.Token);
            }
            catch
            {
                await SendErrorReplyAsync(clientStream, 0x05, ct); // Connection refused
                return;
            }

            var upstreamStream = upstream.GetStream();

            // Authenticate with internal usque (NO AUTH)
            await upstreamStream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, ct);
            var upGreet = new byte[2];
            if (!await ReadExactAsync(upstreamStream, upGreet, 0, 2, ct) || upGreet[0] != 0x05 || upGreet[1] != 0x00)
            {
                await SendErrorReplyAsync(clientStream, 0x01, ct);
                return;
            }

            // Forward exact original CONNECT request bytes to usque
            var rawReq = new byte[4 + addrPart.Length + 2];
            Buffer.BlockCopy(reqHead, 0, rawReq, 0, 4);
            Buffer.BlockCopy(addrPart, 0, rawReq, 4, addrPart.Length);
            Buffer.BlockCopy(portPart, 0, rawReq, 4 + addrPart.Length, 2);
            await upstreamStream.WriteAsync(rawReq, ct);

            // Pump upstream reply + full-duplex TCP stream transparently
            using var relayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var t1 = PumpTcpAsync(clientStream, upstreamStream, relayCts.Token);
            var t2 = PumpTcpAsync(upstreamStream, clientStream, relayCts.Token);
            await Task.WhenAny(t1, t2);
            relayCts.Cancel();
            try { await Task.WhenAll(t1, t2); } catch { }
        }

        private async Task HandleUdpAssociateAsync(NetworkStream clientStream, CancellationToken ct)
        {
            using var clientSideUdp = CreateTunedLoopbackUdp();
            using var usqueSideUdp = CreateTunedLoopbackUdp();

            var clientRelayPort = ((IPEndPoint)clientSideUdp.Client.LocalEndPoint!).Port;
            var usqueBoundPort = ((IPEndPoint)usqueSideUdp.Client.LocalEndPoint!).Port;

            using var upstream = new TcpClient { NoDelay = true };
            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(6000);
                await upstream.ConnectAsync(IPAddress.Loopback, _upstreamPort, connectCts.Token);
            }
            catch
            {
                await SendErrorReplyAsync(clientStream, 0x05, ct);
                return;
            }

            var upstreamStream = upstream.GetStream();

            // 1. Greet internal usque
            await upstreamStream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, ct);
            var upGreet = new byte[2];
            if (!await ReadExactAsync(upstreamStream, upGreet, 0, 2, ct) || upGreet[0] != 0x05 || upGreet[1] != 0x00)
            {
                await SendErrorReplyAsync(clientStream, 0x01, ct);
                return;
            }

            // 2. Send UDP ASSOCIATE to usque with our EXACT bound 127.0.0.1:usqueBoundPort!
            //    This satisfies usque v4.2.1's strict srv.AssociatedUDP check cleanly.
            var assocReq = new byte[10];
            assocReq[0] = 0x05;
            assocReq[1] = 0x03; // UDP ASSOCIATE
            assocReq[2] = 0x00;
            assocReq[3] = 0x01; // IPv4
            assocReq[4] = 127;
            assocReq[5] = 0;
            assocReq[6] = 0;
            assocReq[7] = 1;
            assocReq[8] = (byte)(usqueBoundPort >> 8);
            assocReq[9] = (byte)(usqueBoundPort & 0xFF);
            await upstreamStream.WriteAsync(assocReq, ct);

            // 3. Parse usque's UDP ASSOCIATE reply
            var repHead = new byte[4];
            if (!await ReadExactAsync(upstreamStream, repHead, 0, 4, ct) || repHead[0] != 0x05 || repHead[1] != 0x00)
            {
                await SendErrorReplyAsync(clientStream, repHead[1] != 0 ? repHead[1] : (byte)0x01, ct);
                return;
            }

            IPAddress usqueRelayIp;
            if (repHead[3] == 0x01)
            {
                var ipBytes = new byte[4];
                if (!await ReadExactAsync(upstreamStream, ipBytes, 0, 4, ct)) return;
                usqueRelayIp = new IPAddress(ipBytes);
                if (usqueRelayIp.Equals(IPAddress.Any)) usqueRelayIp = IPAddress.Loopback;
            }
            else if (repHead[3] == 0x04)
            {
                var ipBytes = new byte[16];
                if (!await ReadExactAsync(upstreamStream, ipBytes, 0, 16, ct)) return;
                usqueRelayIp = new IPAddress(ipBytes);
                if (usqueRelayIp.Equals(IPAddress.IPv6Any)) usqueRelayIp = IPAddress.IPv6Loopback;
            }
            else if (repHead[3] == 0x03)
            {
                var dLen = new byte[1];
                if (!await ReadExactAsync(upstreamStream, dLen, 0, 1, ct)) return;
                var dBytes = new byte[dLen[0]];
                if (dLen[0] > 0 && !await ReadExactAsync(upstreamStream, dBytes, 0, dLen[0], ct)) return;
                usqueRelayIp = IPAddress.Loopback;
            }
            else
            {
                await SendErrorReplyAsync(clientStream, 0x08, ct);
                return;
            }

            var upPortBytes = new byte[2];
            if (!await ReadExactAsync(upstreamStream, upPortBytes, 0, 2, ct)) return;
            int usqueRelayPort = (upPortBytes[0] << 8) | upPortBytes[1];
            var usqueRelayEp = new IPEndPoint(usqueRelayIp, usqueRelayPort);

            // 4. Reply to ProxiFyre with our bridge's client-facing UDP relay port (127.0.0.1:clientRelayPort)
            var clientRep = new byte[10];
            clientRep[0] = 0x05;
            clientRep[1] = 0x00;
            clientRep[2] = 0x00;
            clientRep[3] = 0x01;
            clientRep[4] = 127;
            clientRep[5] = 0;
            clientRep[6] = 0;
            clientRep[7] = 1;
            clientRep[8] = (byte)(clientRelayPort >> 8);
            clientRep[9] = (byte)(clientRelayPort & 0xFF);
            await clientStream.WriteAsync(clientRep, ct);

            // 5. Relay UDP datagrams in both directions until either TCP control socket closes
            using var assocCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            IPEndPoint? lastClientUdpEp = null;

            var c2u = Task.Run(async () =>
            {
                try
                {
                    while (!assocCts.IsCancellationRequested)
                    {
                        var pkt = await clientSideUdp.ReceiveAsync(assocCts.Token);
                        Volatile.Write(ref lastClientUdpEp, pkt.RemoteEndPoint);
                        await usqueSideUdp.SendAsync(pkt.Buffer, pkt.Buffer.Length, usqueRelayEp);
                    }
                }
                catch { }
            }, assocCts.Token);

            var u2c = Task.Run(async () =>
            {
                try
                {
                    while (!assocCts.IsCancellationRequested)
                    {
                        var pkt = await usqueSideUdp.ReceiveAsync(assocCts.Token);
                        var target = Volatile.Read(ref lastClientUdpEp);
                        if (target != null)
                        {
                            await clientSideUdp.SendAsync(pkt.Buffer, pkt.Buffer.Length, target);
                        }
                    }
                }
                catch { }
            }, assocCts.Token);

            var watchClientTcp = WaitForTcpCloseAsync(clientStream, assocCts.Token);
            var watchUpstreamTcp = WaitForTcpCloseAsync(upstreamStream, assocCts.Token);

            await Task.WhenAny(watchClientTcp, watchUpstreamTcp);
            assocCts.Cancel();
            try { await Task.WhenAll(c2u, u2c); } catch { }
        }

        private static UdpClient CreateTunedLoopbackUdp()
        {
            var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            try
            {
                udp.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            catch { }
            try
            {
                udp.Client.ReceiveBufferSize = 2 * 1024 * 1024;
                udp.Client.SendBufferSize = 2 * 1024 * 1024;
            }
            catch { }
            return udp;
        }

        private static async Task WaitForTcpCloseAsync(NetworkStream stream, CancellationToken ct)
        {
            var buf = new byte[64];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int n = await stream.ReadAsync(buf, ct);
                    if (n <= 0) break;
                }
            }
            catch { }
        }

        private static async Task PumpTcpAsync(NetworkStream src, NetworkStream dst, CancellationToken ct)
        {
            var buf = new byte[32768];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int n = await src.ReadAsync(buf, ct);
                    if (n <= 0) break;
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                }
            }
            catch { }
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(offset + read, count - read), ct);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }

        private static async Task SendErrorReplyAsync(NetworkStream stream, byte rep, CancellationToken ct)
        {
            try
            {
                await stream.WriteAsync(new byte[] { 0x05, rep, 0x00, 0x01, 0, 0, 0, 0, 0, 0 }, ct);
            }
            catch { }
        }
    }
}
