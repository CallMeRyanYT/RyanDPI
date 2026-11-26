using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using RyanDPI.Models;

namespace RyanDPI.Services
{
    public sealed class RyanDpiEngineService
    {
        // Distinct from RyanDPI.exe so ProxiFyre's service-tag exclusion ("RyanDpiEngine")
        // excludes ciadpi's Windows service without excluding the RyanDPI.exe GUI self-test!
        public const string EngineServiceName = "RyanDpiEngine";
        public const string RouterServiceName = "ProxiFyreService";
        public const string NdisDriverServiceName = "NDISRD";
        public const string NetworkWatchTaskName = "RyanDpiNetworkWatch";

        public static readonly string InstallRootDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RyanDPI");

        public static readonly string EngineRootDir = Path.Combine(InstallRootDir, "engine");
        public static readonly string EngineByeDpiDir = Path.Combine(EngineRootDir, "byedpi");
        public static readonly string EngineProxiFyreDir = Path.Combine(EngineRootDir, "proxifyre");
        public static readonly string EngineDnsCryptDir = Path.Combine(EngineRootDir, "dnscrypt");
        public static readonly string EngineUsqueDir = Path.Combine(EngineRootDir, "usque");

        public static readonly string EngineCiadpiExe = Path.Combine(EngineByeDpiDir, "ciadpi.exe");
        public static readonly string EngineProxiFyreExe = Path.Combine(EngineProxiFyreDir, "ProxiFyre.exe");
        public static readonly string EngineProxiFyreConfig = Path.Combine(EngineProxiFyreDir, "app-config.json");
        public static readonly string EngineDnsCryptExe = Path.Combine(EngineDnsCryptDir, "dnscrypt-proxy.exe");
        public static readonly string EngineUsqueExe = Path.Combine(EngineUsqueDir, "usque.exe");
        public static readonly string EngineUsqueConfig = Path.Combine(EngineUsqueDir, "config.json");
        public static readonly string EngineServiceHostExe = Path.Combine(EngineUsqueDir, "RyanDPI.exe");
        public static readonly string EngineNetworkWatchScript = Path.Combine(EngineRootDir, "network-watch.ps1");
        public static readonly string EngineLastNetworkStateFile = Path.Combine(EngineRootDir, "last_net.txt");

        private static readonly string[] LegacyServices =
        {
            "RyanDPI", // Previous v2.0.0 service name (migrated cleanly to RyanDpiEngine)
            "ByeDPI",
            "GoodbyeDPI",
            "zapret",
            "winws1",
            "winws2",
            "WinDivert",
            "WinDivert14",
            "wiresock-client-service",
            "WireSockAppService",
            "WireSockConnectService"
        };

        private static readonly string[] ProxiFyreAllowedFiles =
        {
            "ProxiFyre.exe",
            "ProxiFyre.exe.config",
            "ProxiFyre.Configuration.dll",
            "socksify.dll",
            "Newtonsoft.Json.dll",
            "NLog.dll",
            "NLog.config",
            "Topshelf.dll"
        };

        private readonly DnsService _dnsService;

        public RyanDpiEngineService()
        {
            _dnsService = new DnsService(EngineRootDir);
        }

        public DnsService Dns => _dnsService;

        public async Task<RyanDpiStatus> GetStatusAsync()
        {
            return await Task.Run(() =>
            {
                var engineState = ServiceControlHelper.QueryServiceState(EngineServiceName);
                var routerState = ServiceControlHelper.QueryServiceState(RouterServiceName);
                var dnsState = _dnsService.GetDnsCryptState();
                var ndisInstalled = ServiceControlHelper.QueryServiceState(NdisDriverServiceName) != ComponentState.NotInstalled;

                var legacyFound = new List<string>();
                foreach (var svc in LegacyServices)
                {
                    if (ServiceControlHelper.QueryServiceState(svc) != ComponentState.NotInstalled)
                    {
                        legacyFound.Add(svc);
                    }
                }

                return new RyanDpiStatus
                {
                    EngineState = engineState,
                    RouterState = routerState,
                    DnsProxyState = dnsState,
                    NdisDriverInstalled = ndisInstalled,
                    HasLegacyServices = legacyFound.Count > 0,
                    DetectedLegacyServices = legacyFound
                };
            });
        }

        public async Task<OperationResult> InstallAndEnableAsync(
            RyanDpiSettings settings,
            IProgress<InstallProgressUpdate>? progress = null)
        {
            const int totalSteps = 5;

            try
            {
                LogService.Info("=== Starting RyanDPI Full System Installation ===");

                // Step 1: Clean up legacy / conflicting services (and prior RyanDPI services for a fresh install)
                progress?.Report(new InstallProgressUpdate
                {
                    StepIndex = 1,
                    TotalSteps = totalSteps,
                    Title = LocalizationService.T("Step1_Title"),
                    Detail = LocalizationService.T("Step1_Detail")
                });
                await CleanupLegacyServicesAsync(includeCurrentRyanDpi: true);

                // Step 2: Prerequisites (Windows Packet Filter driver + VC++ runtime)
                progress?.Report(new InstallProgressUpdate
                {
                    StepIndex = 2,
                    TotalSteps = totalSteps,
                    Title = LocalizationService.T("Step2_Title"),
                    Detail = LocalizationService.T("Step2_Detail")
                });
                var (prereqOk, ndisNeedsReboot) = await EnsurePrerequisitesAsync();
                if (!prereqOk)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = LocalizationService.T("Msg_NdisFailed")
                    };
                }

                // Step 3: Stage binaries to C:\Program Files\RyanDPI\engine + write all-apps config + ACL harden
                progress?.Report(new InstallProgressUpdate
                {
                    StepIndex = 3,
                    TotalSteps = totalSteps,
                    Title = LocalizationService.T("Step3_Title"),
                    Detail = LocalizationService.T("Step3_Detail")
                });
                var stageOk = await StageEngineAndConfigAsync(settings);
                if (!stageOk)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = LocalizationService.T("Msg_StageFailed")
                    };
                }

                // Step 4: DNS protection + Firewall rules
                progress?.Report(new InstallProgressUpdate
                {
                    StepIndex = 4,
                    TotalSteps = totalSteps,
                    Title = LocalizationService.T("Step4_Title"),
                    Detail = LocalizationService.T("Step4_Detail")
                });
                var bundledDnsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "dnscrypt-proxy");
                var (dnsOk, dnsSummary) = await _dnsService.ApplyDnsModeAsync(settings.DnsMode, bundledDnsDir);
                LogService.Info($"DNS step result: {dnsSummary} (ok={dnsOk})");

                await ConfigureFirewallRulesAsync();

                // Step 5: Register & start RyanDPI + ProxiFyreService
                progress?.Report(new InstallProgressUpdate
                {
                    StepIndex = 5,
                    TotalSteps = totalSteps,
                    Title = LocalizationService.T("Step5_Title"),
                    Detail = LocalizationService.T("Step5_Detail")
                });

                var engineStarted = await InstallAndStartEngineServiceAsync(settings);
                if (!engineStarted)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = LocalizationService.T("Msg_EngineStartFailed")
                    };
                }

                var routerStarted = await InstallAndStartRouterServiceAsync();
                if (!routerStarted)
                {
                    if (ndisNeedsReboot)
                    {
                        await ConfigureNetworkWatchTaskAsync(settings);
                        LogService.Warn("ProxiFyreService is installed and will activate automatically after reboot once NDISRD initializes.");
                        return new OperationResult
                        {
                            Success = true,
                            RequiresReboot = true,
                            Message = LocalizationService.T("Msg_RebootNeeded")
                        };
                    }

                    return new OperationResult
                    {
                        Success = false,
                        Message = LocalizationService.T("Msg_RouterStartFailed")
                    };
                }

                await ConfigureNetworkWatchTaskAsync(settings);

                LogService.Info("=== RyanDPI installed and running smoothly across all applications ===");
                return new OperationResult
                {
                    Success = true,
                    RequiresReboot = false,
                    Message = LocalizationService.Tf("Msg_InstallSuccess", dnsSummary)
                };
            }
            catch (Exception ex)
            {
                LogService.Error($"Installation failed: {ex.Message}");
                return new OperationResult
                {
                    Success = false,
                    Message = LocalizationService.Tf("Msg_InstallError", ex.Message)
                };
            }
        }

        public async Task<OperationResult> StartAsync(RyanDpiSettings settings)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    LogService.Info("Starting RyanDPI services...");

                    if (settings.DnsMode == DnsProtectionMode.EncryptedDnsProxy)
                    {
                        _dnsService.EnsureDnsCryptRunningIfInstalled();
                    }

                    // Ensure NDISRD driver is running
                    if (ServiceControlHelper.QueryServiceState(NdisDriverServiceName) == ComponentState.Stopped)
                    {
                        CommandRunner.Run("sc.exe", $"start {NdisDriverServiceName}", 15000);
                    }

                    // Start RyanDPI engine first
                    if (ServiceControlHelper.QueryServiceState(EngineServiceName) != ComponentState.Running)
                    {
                        CommandRunner.Run("sc.exe", $"start {EngineServiceName}", 15000);
                    }

                    var serviceRunning = await WaitForServiceRunningAsync(EngineServiceName, 8000);
                    var portReady = serviceRunning && await WaitForLocalSocksPortAsync(settings.ActiveLocalEnginePort, settings.UsesBuiltInUsqueService ? 10000 : 5000);
                    if (!portReady)
                    {
                        LogService.Error($"RyanDPI engine failed verification (serviceRunning={serviceRunning}).");
                        return new OperationResult
                        {
                            Success = false,
                            Message = LocalizationService.T("Msg_StartEngineFail")
                        };
                    }

                    // Then start system-wide router
                    if (ServiceControlHelper.QueryServiceState(RouterServiceName) != ComponentState.Running)
                    {
                        CommandRunner.Run("sc.exe", $"start {RouterServiceName}", 15000);
                    }

                    var routerRunning = await WaitForServiceRunningAsync(RouterServiceName, 5000);
                    if (!routerRunning)
                    {
                        return new OperationResult
                        {
                            Success = false,
                            Message = LocalizationService.T("Msg_StartRouterFail")
                        };
                    }

                    LogService.Info("RyanDPI services are now running.");
                    return new OperationResult { Success = true, Message = LocalizationService.T("Msg_Started") };
                }
                catch (Exception ex)
                {
                    LogService.Error($"Start failed: {ex.Message}");
                    return new OperationResult { Success = false, Message = ex.Message };
                }
            });
        }

        public async Task<OperationResult> StopAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    LogService.Info("Pausing RyanDPI services...");
                    // Always stop router BEFORE stopping local SOCKS engine to prevent transient connection errors
                    ServiceControlHelper.StopService(RouterServiceName);
                    ServiceControlHelper.StopService(EngineServiceName);
                    LogService.Info("RyanDPI paused cleanly. Direct internet routing restored.");
                    return new OperationResult { Success = true, Message = LocalizationService.T("Msg_Paused") };
                }
                catch (Exception ex)
                {
                    LogService.Error($"Stop failed: {ex.Message}");
                    return new OperationResult { Success = false, Message = ex.Message };
                }
            });
        }

        public async Task<OperationResult> ApplySettingsAndRestartAsync(RyanDpiSettings settings)
        {
            try
            {
                LogService.Info("Applying updated RyanDPI settings...");
                settings.Save();

                var status = await GetStatusAsync();
                if (status.EngineState == ComponentState.NotInstalled ||
                    status.RouterState == ComponentState.NotInstalled)
                {
                    return new OperationResult
                    {
                        Success = true,
                        Message = LocalizationService.T("Msg_SettingsSavedNotInstalled")
                    };
                }

                // Perform all blocking service & ACL operations off the UI thread
                await Task.Run(() =>
                {
                    ServiceControlHelper.StopService(RouterServiceName);
                    ServiceControlHelper.StopService(EngineServiceName);

                    // Ensure usque binaries are staged if switching to Brazil Built-in WARP MASQUE
                    if (settings.UsesBuiltInUsqueService && (!File.Exists(EngineUsqueExe) || !File.Exists(EngineServiceHostExe)))
                    {
                        var srcUsque = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "usque", "usque.exe");
                        Directory.CreateDirectory(EngineUsqueDir);
                        if (File.Exists(srcUsque))
                        {
                            File.Copy(srcUsque, EngineUsqueExe, overwrite: true);
                        }
                        var appBase = AppDomain.CurrentDomain.BaseDirectory;
                        foreach (var hostFile in new[] { "RyanDPI.exe", "RyanDPI.dll", "RyanDPI.runtimeconfig.json", "RyanDPI.deps.json" })
                        {
                            var srcHost = Path.Combine(appBase, hostFile);
                            if (File.Exists(srcHost))
                            {
                                File.Copy(srcHost, Path.Combine(EngineUsqueDir, hostFile), overwrite: true);
                            }
                        }
                    }

                    if (settings.UsesBuiltInUsqueService && !File.Exists(EngineUsqueConfig) && File.Exists(EngineUsqueExe))
                    {
                        CommandRunner.Run(EngineUsqueExe, $"register -a -c \"{EngineUsqueConfig}\"", 25000, EngineUsqueDir);
                    }

                    WriteProxiFyreConfig(settings);
                    HardenEngineDirectoryAcls();

                    var binPath = settings.UsesBuiltInUsqueService
                        ? RyanDpiSettings.BuildUsqueScBinPathValue(EngineServiceHostExe, EngineUsqueExe, EngineUsqueConfig)
                        : settings.BuildScBinPathValue(EngineCiadpiExe);
                    var cfgRes = CommandRunner.Run("sc.exe", $"config {EngineServiceName} binPath= \"{binPath}\"", 15000);
                    LogService.Info($"Updated RyanDPI service parameters for {settings.Region} (exit code {cfgRes.ExitCode}).");
                });

                // Apply DNS mode change
                var bundledDnsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "dnscrypt-proxy");
                await _dnsService.ApplyDnsModeAsync(settings.DnsMode, bundledDnsDir);

                // Refresh firewall rules & network-switch watcher (e.g. if DNS mode changed)
                await ConfigureFirewallRulesAsync();
                await ConfigureNetworkWatchTaskAsync(settings);

                // Restart engine & router cleanly
                return await StartAsync(settings);
            }
            catch (Exception ex)
            {
                LogService.Error($"Applying settings failed: {ex.Message}");
                return new OperationResult { Success = false, Message = LocalizationService.Tf("Msg_SettingsApplyFailed", ex.Message) };
            }
        }

        /// <summary>
        /// Automatically refreshes staged engine binaries, ProxiFyre config, ACLs, and firewall rules
        /// after an auto-update when RyanDPI services are already installed on the PC.
        /// </summary>
        public async Task<OperationResult> RefreshEngineAfterUpdateAsync(RyanDpiSettings settings)
        {
            try
            {
                var status = await GetStatusAsync();
                bool hasOldV200Service = status.DetectedLegacyServices.Contains("RyanDPI", StringComparer.OrdinalIgnoreCase);
                if (!status.IsInstalled && !hasOldV200Service)
                {
                    return new OperationResult { Success = true, Message = string.Empty };
                }

                bool wasRunning = status.IsFullyActive || status.IsPartiallyRunning || hasOldV200Service;
                LogService.Info($"Refreshing RyanDPI background services after update (wasRunning={wasRunning}, legacyV200={hasOldV200Service})...");

                await Task.Run(() =>
                {
                    ServiceControlHelper.StopService(RouterServiceName);
                    ServiceControlHelper.StopService(EngineServiceName);
                    if (hasOldV200Service)
                    {
                        ServiceControlHelper.StopAndDeleteService("RyanDPI");
                    }
                });

                var stageOk = await StageEngineAndConfigAsync(settings);
                if (!stageOk)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = LocalizationService.T("Update_BgRefreshFail")
                    };
                }

                if (settings.DnsMode == DnsProtectionMode.EncryptedDnsProxy)
                {
                    var bundledDnsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "dnscrypt-proxy");
                    await _dnsService.ApplyDnsModeAsync(settings.DnsMode, bundledDnsDir);
                }

                await ConfigureFirewallRulesAsync();
                await ConfigureNetworkWatchTaskAsync(settings);

                if (hasOldV200Service || status.EngineState == ComponentState.NotInstalled || status.RouterState == ComponentState.NotInstalled)
                {
                    var engineOk = await InstallAndStartEngineServiceAsync(settings);
                    var routerOk = engineOk && await InstallAndStartRouterServiceAsync();
                    if (!wasRunning)
                    {
                        await StopAsync();
                    }
                    return new OperationResult
                    {
                        Success = routerOk,
                        Message = routerOk
                            ? LocalizationService.T("Update_BgRefreshOk")
                            : LocalizationService.T("Update_BgRefreshRestartFail")
                    };
                }

                await Task.Run(() =>
                {
                    var binPath = settings.BuildScBinPathValue(EngineCiadpiExe);
                    CommandRunner.Run("sc.exe", $"config {EngineServiceName} binPath= \"{binPath}\"", 15000);
                    // Use delayed-auto on ProxiFyreService so Wi-Fi/DHCP settles on boot before binding
                    CommandRunner.Run("sc.exe", $"config {RouterServiceName} start= delayed-auto", 10000);
                });

                if (wasRunning)
                {
                    return await StartAsync(settings);
                }

                return new OperationResult { Success = true, Message = LocalizationService.T("Update_BgRefreshPaused") };
            }
            catch (Exception ex)
            {
                LogService.Error($"Post-update engine refresh failed: {ex.Message}");
                return new OperationResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<OperationResult> CleanupLegacyServicesAsync(bool includeCurrentRyanDpi = false)
        {
            return await Task.Run(() =>
            {
                try
                {
                    LogService.Info(includeCurrentRyanDpi
                        ? "Stopping RyanDPI and cleaning up any conflicting legacy DPI / tunneling services..."
                        : "Cleaning up legacy third-party DPI / tunneling services...");

                    if (includeCurrentRyanDpi)
                    {
                        RemoveNetworkWatchTask();
                        ServiceControlHelper.StopAndDeleteService(RouterServiceName);
                        ServiceControlHelper.StopAndDeleteService(EngineServiceName);
                    }

                    int removedCount = 0;
                    foreach (var svc in LegacyServices)
                    {
                        if (ServiceControlHelper.QueryServiceState(svc) != ComponentState.NotInstalled)
                        {
                            LogService.Info($"Removing legacy service: {svc}");
                            ServiceControlHelper.StopAndDeleteService(svc);
                            removedCount++;
                        }
                    }

                    // Clean old WireSockRefresh scheduled task if present
                    CommandRunner.Run("schtasks.exe", "/delete /tn \"WireSockRefresh\" /f", 10000);

                    // Clean legacy Discord drover hijack files if present (only removes version.dll when drover.ini is beside it)
                    CleanupLegacyDiscordDrover();

                    var msg = removedCount > 0
                        ? LocalizationService.Tf("Msg_LegacyRemoved", removedCount)
                        : LocalizationService.T("Msg_LegacyNone");
                    return new OperationResult { Success = true, Message = msg };
                }
                catch (Exception ex)
                {
                    LogService.Warn($"Legacy cleanup warning: {ex.Message}");
                    return new OperationResult { Success = false, Message = ex.Message };
                }
            });
        }

        public async Task<OperationResult> UninstallAllAsync(bool resetDns = true)
        {
            try
            {
                LogService.Info("=== Uninstalling RyanDPI completely ===");

                await CleanupLegacyServicesAsync(includeCurrentRyanDpi: true);
                await Task.Run(RemoveFirewallRules);

                if (resetDns)
                {
                    await _dnsService.ResetDnsToAutomaticAsync(removeDnsCryptService: true);
                }

                // Kill any lingering engine processes before deleting folder
                await Task.Run(() =>
                {
                    foreach (var procName in new[] { "ProxiFyre", "ciadpi", "dnscrypt-proxy", "usque" })
                    {
                        foreach (var p in Process.GetProcessesByName(procName))
                        {
                            try { p.Kill(); p.WaitForExit(3000); } catch { }
                        }
                    }
                });

                await Task.Delay(500);

                if (Directory.Exists(EngineRootDir))
                {
                    try
                    {
                        Directory.Delete(EngineRootDir, recursive: true);
                        LogService.Info($"Removed engine directory: {EngineRootDir}");
                    }
                    catch (Exception ex)
                    {
                        LogService.Warn($"Could not fully remove {EngineRootDir}: {ex.Message}");
                    }
                }

                LogService.Info("=== RyanDPI completely uninstalled ===");
                return new OperationResult
                {
                    Success = true,
                    Message = LocalizationService.T("Msg_UninstallSuccess")
                };
            }
            catch (Exception ex)
            {
                LogService.Error($"Uninstall failed: {ex.Message}");
                return new OperationResult { Success = false, Message = LocalizationService.Tf("Msg_UninstallError", ex.Message) };
            }
        }

        public async Task<List<SelfTestCheckResult>> RunSelfTestAsync(RyanDpiSettings? activeSettings = null)
        {
            var settings = activeSettings ?? RyanDpiSettings.Load();
            var results = new List<SelfTestCheckResult>();

            // 1. Local RyanDPI SOCKS5 Engine socket + service check
            var engineSvcRunning = await Task.Run(() => ServiceControlHelper.QueryServiceState(EngineServiceName) == ComponentState.Running);
            var routerSvcRunning = await Task.Run(() => ServiceControlHelper.QueryServiceState(RouterServiceName) == ComponentState.Running);

            int probePort = settings.ActiveLocalEnginePort;
            string probeHost = "127.0.0.1";
            if (settings.Region == RegionPreset.Brazil &&
                settings.BrazilTunnel == BrazilTunnelMode.CustomSocks5 &&
                TryParseEndpoint(settings.CustomSocksEndpoint, out var cHost, out var cPort))
            {
                probeHost = cHost;
                probePort = cPort;
            }

            var sw = Stopwatch.StartNew();
            var socksAlive = await ProbeTcpPortAsync(probeHost, probePort, 2000);
            sw.Stop();

            bool enginePassed = engineSvcRunning && routerSvcRunning && socksAlive;
            string engineSummary = enginePassed
                ? (settings.Region == RegionPreset.Brazil
                    ? LocalizationService.Tf("SelfTest_BrazilEngineActive", $"{probeHost}:{probePort}")
                    : LocalizationService.Tf("SelfTest_LocalEngineActive", probePort))
                : (!engineSvcRunning && socksAlive)
                    ? LocalizationService.T("SelfTest_LocalEngineLegacy")
                    : !engineSvcRunning
                        ? LocalizationService.T("SelfTest_LocalEngineStopped")
                        : !routerSvcRunning
                            ? LocalizationService.T("SelfTest_LocalRouterStopped")
                            : LocalizationService.Tf("SelfTest_LocalPortUnreachable", probePort);

            results.Add(new SelfTestCheckResult
            {
                Name = LocalizationService.T("SelfTest_LocalEngine"),
                Passed = enginePassed,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = engineSummary
            });

            // Brazil Mode: test Discord Split-Tunnel Exit Country/IP + Discord API through the Discord proxy,
            // plus Direct Game Baseline Ping (proving 0ms game lag!)
            if (settings.Region == RegionPreset.Brazil)
            {
                results.Add(await ProbeBrazilExitCountryAsync(probeHost, probePort, settings));

                using var proxyHandler = CreateOptionalSocksHandler(
                    socksAlive ? probeHost : null,
                    probePort,
                    settings.BrazilTunnel == BrazilTunnelMode.CustomSocks5 ? settings.CustomSocksUsername : null,
                    settings.BrazilTunnel == BrazilTunnelMode.CustomSocks5 ? settings.CustomSocksPassword : null);
                using var proxyHttp = new HttpClient(proxyHandler) { Timeout = TimeSpan.FromSeconds(8) };
                proxyHttp.DefaultRequestHeaders.UserAgent.ParseAdd($"RyanDPI-HealthCheck/{AutoUpdateService.GetCurrentVersionString()}");

                using var directHandler = CreateOptionalSocksHandler(null, 0, null, null);
                using var directHttp = new HttpClient(directHandler) { Timeout = TimeSpan.FromSeconds(6) };
                directHttp.DefaultRequestHeaders.UserAgent.ParseAdd($"RyanDPI-HealthCheck/{AutoUpdateService.GetCurrentVersionString()}");

                var brChecks = await Task.WhenAll(
                    ProbeHttpEndpointAsync(proxyHttp, LocalizationService.T("SelfTest_Discord"), "https://discord.com/api/v10/gateway", HttpMethod.Get),
                    ProbeHttpEndpointAsync(proxyHttp, LocalizationService.T("SelfTest_DiscordMedia"), "https://cdn.discordapp.com/", HttpMethod.Head),
                    ProbeHttpEndpointAsync(directHttp, LocalizationService.T("SelfTest_DirectGameBaseline"), "https://1.1.1.1/cdn-cgi/trace", HttpMethod.Get));

                results.AddRange(brChecks);
                return results;
            }

            // 2. DNS resolution check for blocked domains (Global / Turkey / Russia)
            sw.Restart();
            bool dnsPassed;
            string dnsInfo;
            try
            {
                var dnsTasks = settings.Region == RegionPreset.Russia
                    ? await Task.WhenAll(
                        DnsResolutionAsync("discord.com"),
                        DnsResolutionAsync("www.youtube.com"),
                        DnsResolutionAsync("www.instagram.com"))
                    : await Task.WhenAll(
                        DnsResolutionAsync("discord.com"),
                        DnsResolutionAsync("clientsettings.roblox.com"),
                        DnsResolutionAsync("www.roblox.com"));
                dnsPassed = dnsTasks[0].Length > 0 && dnsTasks[1].Length > 0 && dnsTasks[2].Length > 0;
                dnsInfo = dnsPassed ? LocalizationService.T("SelfTest_DnsClean") : LocalizationService.T("SelfTest_DnsEmpty");
            }
            catch (Exception ex)
            {
                dnsPassed = false;
                dnsInfo = LocalizationService.Tf("SelfTest_DnsFailed", ex.Message);
            }
            sw.Stop();
            results.Add(new SelfTestCheckResult
            {
                Name = LocalizationService.T("SelfTest_Dns"),
                Passed = dnsPassed,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = dnsInfo
            });

            // 3. Parallel live HTTPS reachability + warm keep-alive ping tests
            using var handler = CreateOptionalSocksHandler(null, 0, null, null);
            using var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(6)
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"RyanDPI-HealthCheck/{AutoUpdateService.GetCurrentVersionString()}");

            var endpoints = settings.Region == RegionPreset.Russia
                ? new (string Name, string Url, HttpMethod WarmMethod)[]
                {
                    (LocalizationService.T("SelfTest_Discord"), "https://discord.com/api/v10/gateway", HttpMethod.Get),
                    (LocalizationService.T("SelfTest_YouTube"), "https://www.youtube.com/", HttpMethod.Head),
                    (LocalizationService.T("SelfTest_Instagram"), "https://www.instagram.com/", HttpMethod.Head),
                    (LocalizationService.T("SelfTest_RobloxApp"), "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer", HttpMethod.Get),
                    (LocalizationService.T("SelfTest_RobloxWeb"), "https://www.roblox.com/", HttpMethod.Head),
                    (LocalizationService.T("SelfTest_DirectBaseline"), "https://1.1.1.1/cdn-cgi/trace", HttpMethod.Get)
                }
                : new (string Name, string Url, HttpMethod WarmMethod)[]
                {
                    (LocalizationService.T("SelfTest_Discord"), "https://discord.com/api/v10/gateway", HttpMethod.Get),
                    (LocalizationService.T("SelfTest_RobloxApp"), "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer", HttpMethod.Get),
                    (LocalizationService.T("SelfTest_RobloxStudio"), "https://clientsettings.roblox.com/v2/client-version/WindowsStudio64", HttpMethod.Get),
                    (LocalizationService.T("SelfTest_RobloxWeb"), "https://www.roblox.com/", HttpMethod.Head),
                    (LocalizationService.T("SelfTest_Wattpad"), "https://www.wattpad.com/", HttpMethod.Head),
                    (LocalizationService.T("SelfTest_DirectBaseline"), "https://1.1.1.1/cdn-cgi/trace", HttpMethod.Get)
                };

            var endpointChecks = endpoints.Select(ep => ProbeHttpEndpointAsync(http, ep.Name, ep.Url, ep.WarmMethod));
            results.AddRange(await Task.WhenAll(endpointChecks));
            return results;
        }

        private static SocketsHttpHandler CreateOptionalSocksHandler(string? socksHost, int socksPort, string? user, string? pass)
        {
            var h = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                PooledConnectionLifetime = TimeSpan.FromMinutes(1),
                EnableMultipleHttp2Connections = true
            };
            if (!string.IsNullOrWhiteSpace(socksHost) && socksPort > 0)
            {
                var proxy = new WebProxy($"socks5://{socksHost}:{socksPort}");
                if (!string.IsNullOrWhiteSpace(user))
                {
                    proxy.Credentials = new NetworkCredential(user, pass ?? string.Empty);
                }
                h.UseProxy = true;
                h.Proxy = proxy;
            }
            else
            {
                h.UseProxy = false;
            }
            return h;
        }

        private static async Task<SelfTestCheckResult> ProbeBrazilExitCountryAsync(string host, int port, RyanDpiSettings settings)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var handler = CreateOptionalSocksHandler(
                    host, port,
                    settings.BrazilTunnel == BrazilTunnelMode.CustomSocks5 ? settings.CustomSocksUsername : null,
                    settings.BrazilTunnel == BrazilTunnelMode.CustomSocks5 ? settings.CustomSocksPassword : null);
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"RyanDPI-HealthCheck/{AutoUpdateService.GetCurrentVersionString()}");

                var trace = await http.GetStringAsync("https://1.1.1.1/cdn-cgi/trace");
                sw.Stop();

                string loc = "??";
                string ip = "";
                foreach (var line in trace.Split('\n'))
                {
                    if (line.StartsWith("loc=")) loc = line[4..].Trim();
                    else if (line.StartsWith("ip=")) ip = line[3..].Trim();
                }

                bool isNonBrazil = !string.Equals(loc, "BR", StringComparison.OrdinalIgnoreCase);
                string summary = isNonBrazil
                    ? LocalizationService.Tf("SelfTest_BrazilExitNonBr", loc, ip)
                    : LocalizationService.Tf("SelfTest_BrazilExitStillBr", ip);

                return new SelfTestCheckResult
                {
                    Name = LocalizationService.T("SelfTest_BrazilExitCheck"),
                    Passed = true,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Summary = summary
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new SelfTestCheckResult
                {
                    Name = LocalizationService.T("SelfTest_BrazilExitCheck"),
                    Passed = false,
                    LatencyMs = sw.ElapsedMilliseconds,
                    Summary = LocalizationService.Tf("SelfTest_BlockedFmt", ex.Message.Length > 55 ? ex.Message[..55] + "..." : ex.Message)
                };
            }
        }

        private static async Task<SelfTestCheckResult> ProbeHttpEndpointAsync(HttpClient http, string name, string url, HttpMethod warmMethod)
        {
            var coldSw = Stopwatch.StartNew();
            try
            {
                using var coldReq = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = HttpVersion.Version20,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                };
                using var coldResp = await http.SendAsync(coldReq, HttpCompletionOption.ResponseHeadersRead);
                coldSw.Stop();
                var coldMs = coldSw.ElapsedMilliseconds;
                var code = (int)coldResp.StatusCode;
                bool ok = code >= 200 && code < 500;

                if (!ok)
                {
                    return new SelfTestCheckResult
                    {
                        Name = name,
                        Passed = false,
                        LatencyMs = coldMs,
                        Summary = LocalizationService.Tf("SelfTest_UnexpectedHttp", code)
                    };
                }

                if (warmMethod == HttpMethod.Get)
                {
                    try { await coldResp.Content.ReadAsByteArrayAsync(); } catch { }
                }

                long livePingMs = coldMs;
                try
                {
                    var warmSw = Stopwatch.StartNew();
                    using var warmReq = new HttpRequestMessage(warmMethod, url)
                    {
                        Version = HttpVersion.Version20,
                        VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                    };
                    using var warmResp = await http.SendAsync(warmReq, HttpCompletionOption.ResponseHeadersRead);
                    warmSw.Stop();
                    if ((int)warmResp.StatusCode >= 200 && (int)warmResp.StatusCode < 500)
                    {
                        livePingMs = Math.Min(warmSw.ElapsedMilliseconds, coldMs);
                    }
                }
                catch { }

                return new SelfTestCheckResult
                {
                    Name = name,
                    Passed = true,
                    LatencyMs = livePingMs,
                    Summary = LocalizationService.Tf("SelfTest_UnlockedFmt", code, coldMs)
                };
            }
            catch (Exception ex)
            {
                coldSw.Stop();
                var shortErr = ex.InnerException?.Message ?? ex.Message;
                if (shortErr.Length > 60) shortErr = shortErr[..60] + "...";
                return new SelfTestCheckResult
                {
                    Name = name,
                    Passed = false,
                    LatencyMs = coldSw.ElapsedMilliseconds,
                    Summary = LocalizationService.Tf("SelfTest_BlockedFmt", shortErr)
                };
            }
        }

        public static bool TryParseEndpoint(string? raw, out string host, out int port)
        {
            host = string.Empty;
            port = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var cleaned = raw.Trim()
                .Replace("socks5://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("socks5h://", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            int lastColon = cleaned.LastIndexOf(':');
            if (lastColon <= 0 || lastColon >= cleaned.Length - 1) return false;

            var h = cleaned[..lastColon].Trim().Trim('[', ']');
            var pStr = cleaned[(lastColon + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(h) || !int.TryParse(pStr, out var p) || p < 1 || p > 65535)
                return false;

            host = h;
            port = p;
            return true;
        }

        private async Task<(bool Ok, bool NeedsReboot)> EnsurePrerequisitesAsync()
        {
            return await Task.Run(() =>
            {
                var prereqDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prerequisites");
                bool needsReboot = false;

                // 1. Windows Packet Filter (NDISRD)
                var ndisState = ServiceControlHelper.QueryServiceState(NdisDriverServiceName);
                if (ndisState == ComponentState.NotInstalled)
                {
                    var msiPath = Path.Combine(prereqDir, "Windows.Packet.Filter.3.6.1.1.x64.msi");
                    if (!File.Exists(msiPath))
                    {
                        LogService.Error($"Missing prerequisite: {msiPath}");
                        return (false, false);
                    }

                    LogService.Info("Installing Windows Packet Filter 3.6.1.1 (NDISRD driver)...");
                    var res = CommandRunner.Run("msiexec.exe", $"/i \"{msiPath}\" /quiet /norestart", 120000);
                    LogService.Info($"Windows Packet Filter MSI exit code: {res.ExitCode}");
                    if (res.ExitCode == 3010)
                    {
                        needsReboot = true;
                    }
                    else if (res.ExitCode != 0 && res.ExitCode != 1638)
                    {
                        LogService.Error($"Windows Packet Filter install failed: {res.CombinedOutput}");
                        return (false, false);
                    }
                }
                else
                {
                    LogService.Info("Windows Packet Filter driver (NDISRD) is already installed.");
                }

                // Try starting NDISRD immediately so a reboot is usually unnecessary
                if (ServiceControlHelper.QueryServiceState(NdisDriverServiceName) != ComponentState.Running)
                {
                    var startNdis = CommandRunner.Run("sc.exe", $"start {NdisDriverServiceName}", 15000);
                    LogService.Info($"NDISRD driver start exit code: {startNdis.ExitCode}");
                    if (ServiceControlHelper.QueryServiceState(NdisDriverServiceName) != ComponentState.Running)
                    {
                        needsReboot = true;
                    }
                }

                // 2. VC++ 2015-2022 x64 Redistributable
                if (!IsVcRedistX64Installed())
                {
                    var vcPath = Path.Combine(prereqDir, "VC_redist.x64.exe");
                    if (File.Exists(vcPath))
                    {
                        LogService.Info("Installing Visual C++ x64 Redistributable...");
                        var res = CommandRunner.Run(vcPath, "/install /quiet /norestart", 120000);
                        LogService.Info($"VC_redist.x64 exit code: {res.ExitCode}");
                        if (res.ExitCode == 3010) needsReboot = true;
                    }
                    else
                    {
                        LogService.Warn($"VC_redist.x64.exe not found at {vcPath}; continuing.");
                    }
                }
                else
                {
                    LogService.Info("Visual C++ x64 Redistributable is already installed.");
                }

                return (true, needsReboot);
            });
        }

        private static bool IsVcRedistX64Installed()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
                var installed = key?.GetValue("Installed");
                return installed is int i && i == 1;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> StageEngineAndConfigAsync(RyanDpiSettings settings)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var baseRes = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res");
                    var srcByeDpi = Path.Combine(baseRes, "byedpi", "ciadpi.exe");
                    var srcProxiFyreDir = Path.Combine(baseRes, "proxifyre");

                    if (!File.Exists(srcByeDpi))
                    {
                        LogService.Error($"Engine binary not found: {srcByeDpi}");
                        return false;
                    }

                    if (!Directory.Exists(srcProxiFyreDir))
                    {
                        LogService.Error($"ProxiFyre folder not found: {srcProxiFyreDir}");
                        return false;
                    }

                    // Ensure stopped before overwriting files
                    ServiceControlHelper.StopService(RouterServiceName);
                    ServiceControlHelper.StopService(EngineServiceName);

                    foreach (var procName in new[] { "ProxiFyre", "ciadpi", "usque" })
                    {
                        foreach (var p in Process.GetProcessesByName(procName))
                        {
                            try { p.Kill(); p.WaitForExit(2000); } catch { }
                        }
                    }

                    Directory.CreateDirectory(EngineByeDpiDir);
                    Directory.CreateDirectory(EngineProxiFyreDir);
                    Directory.CreateDirectory(EngineUsqueDir);

                    // Copy ciadpi.exe
                    File.Copy(srcByeDpi, EngineCiadpiExe, overwrite: true);

                    // Stage usque.exe + lightweight RyanDPI service host for Brazil WARP MASQUE split-tunnel mode
                    var srcUsque = Path.Combine(baseRes, "usque", "usque.exe");
                    if (File.Exists(srcUsque))
                    {
                        File.Copy(srcUsque, EngineUsqueExe, overwrite: true);
                        var appBase = AppDomain.CurrentDomain.BaseDirectory;
                        foreach (var hostFile in new[] { "RyanDPI.exe", "RyanDPI.dll", "RyanDPI.runtimeconfig.json", "RyanDPI.deps.json" })
                        {
                            var srcHost = Path.Combine(appBase, hostFile);
                            if (File.Exists(srcHost))
                            {
                                File.Copy(srcHost, Path.Combine(EngineUsqueDir, hostFile), overwrite: true);
                            }
                        }
                    }

                    // Copy ONLY ProxiFyre's expected binaries so ProxiFyre 2.6.1's
                    // EngineServiceInstallLocationPolicy never sees an unexpected PE file
                    foreach (var existingFile in Directory.GetFiles(EngineProxiFyreDir))
                    {
                        var name = Path.GetFileName(existingFile);
                        if (!ProxiFyreAllowedFiles.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                            !name.Equals("app-config.json", StringComparison.OrdinalIgnoreCase))
                        {
                            try { File.Delete(existingFile); } catch { }
                        }
                    }

                    foreach (var fileName in ProxiFyreAllowedFiles)
                    {
                        var src = Path.Combine(srcProxiFyreDir, fileName);
                        if (File.Exists(src))
                        {
                            File.Copy(src, Path.Combine(EngineProxiFyreDir, fileName), overwrite: true);
                        }
                    }

                    // Write system-wide RyanDPI app-config.json
                    WriteProxiFyreConfig(settings);

                    // Harden ACLs for ProxiFyre 2.6.1 security policy
                    HardenEngineDirectoryAcls();

                    LogService.Info($"Staged RyanDPI engine files cleanly to {EngineRootDir}");
                    return File.Exists(EngineCiadpiExe) && File.Exists(EngineProxiFyreExe);
                }
                catch (Exception ex)
                {
                    LogService.Error($"Failed to stage engine files: {ex.Message}");
                    return false;
                }
            });
        }

        public static string BuildProxiFyreConfigJson(RyanDpiSettings settings)
        {
            // CRITICAL loop-prevention exclusions:
            // Exclude exact Windows service names AND executable names/paths so neither ciadpi,
            // usque (Brazil WARP MASQUE), ProxiFyre, nor dnscrypt-proxy can ever loop back.
            var excludes = new List<string>
            {
                EngineServiceName,
                "ciadpi",
                "ciadpi.exe",
                EngineCiadpiExe,
                "usque",
                "usque.exe",
                EngineUsqueExe,
                RouterServiceName,
                "ProxiFyre",
                "ProxiFyre.exe",
                EngineProxiFyreExe,
                DnsService.DnsCryptServiceName,
                "dnscrypt-proxy.exe",
                EngineDnsCryptExe,
                "System"
            };

            foreach (var userApp in settings.ExcludedApps)
            {
                var trimmed = userApp?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) &&
                    !excludes.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    excludes.Add(trimmed);
                }
            }

            var rules = new List<ProxiFyreRuleDto>();

            if (settings.Region == RegionPreset.Brazil)
            {
                // Brazil ANPD Discord Screen-Share / Video Split Tunnel:
                // Routes ONLY Discord (+ optional browsers for Discord Web) through the Geo-Unblock
                // SOCKS5 tunnel, leaving 100% of games (Valorant, CS2, Roblox, Steam, etc.) completely
                // direct at 0ms extra ping!
                var brApps = new List<string> { "Discord", "DiscordPTB", "DiscordCanary", "Update" };
                if (settings.BrazilIncludeBrowsers)
                {
                    brApps.AddRange(new[] { "chrome", "msedge", "firefox", "brave", "opera" });
                }

                string endpoint = $"127.0.0.1:{RyanDpiSettings.UsqueSocksPort}";
                string? user = null;
                string? pass = null;

                if (settings.BrazilTunnel == BrazilTunnelMode.CustomSocks5 &&
                    TryParseEndpoint(settings.CustomSocksEndpoint, out var customHost, out var customPort))
                {
                    endpoint = $"{customHost}:{customPort}";
                    if (!string.IsNullOrWhiteSpace(settings.CustomSocksUsername))
                    {
                        user = settings.CustomSocksUsername.Trim();
                        pass = settings.CustomSocksPassword ?? string.Empty;
                    }
                }

                rules.Add(new ProxiFyreRuleDto
                {
                    AppNames = brApps,
                    Socks5ProxyEndpoint = endpoint,
                    Username = user,
                    Password = pass,
                    SupportedProtocols = settings.BrazilIncludeUdp
                        ? new List<string> { "TCP", "UDP" }
                        : new List<string> { "TCP" }
                });
            }
            else
            {
                var protocols = settings.TunnelUdp
                    ? new List<string> { "TCP", "UDP" }
                    : new List<string> { "TCP" };

                rules.Add(new ProxiFyreRuleDto
                {
                    // Empty string "" is ProxiFyre 2.x's global catch-all matcher:
                    // unlocks EVERY app (Discord, Roblox Studio, Roblox Player, YouTube, browsers, etc.)
                    AppNames = new List<string> { "" },
                    Socks5ProxyEndpoint = $"127.0.0.1:{RyanDpiSettings.DefaultSocksPort}",
                    SupportedProtocols = protocols
                });
            }

            var configObj = new ProxiFyreConfigDto
            {
                LogLevel = "Error",
                BypassLan = settings.BypassLan,
                Proxies = rules,
                Excludes = excludes
            };

            return JsonSerializer.Serialize(configObj, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        private static void WriteProxiFyreConfig(RyanDpiSettings settings)
        {
            var json = BuildProxiFyreConfigJson(settings);
            Directory.CreateDirectory(EngineProxiFyreDir);
            if (File.Exists(EngineProxiFyreConfig))
            {
                try { File.SetAttributes(EngineProxiFyreConfig, FileAttributes.Normal); } catch { }
            }
            File.WriteAllText(EngineProxiFyreConfig, json);
            LogService.Info($"Wrote system-wide ProxiFyre config (UDP={settings.TunnelUdp}, BypassLan={settings.BypassLan}, UserExcludes={settings.ExcludedApps.Count}).");
        }

        private static void HardenEngineDirectoryAcls()
        {
            // ProxiFyre 2.6.1 strictly verifies its folder AND parent chain have no non-admin
            // write/delete/permission-control ACEs (including inherited CREATOR OWNER S-1-3-0).
            // Reset inheritance on tree first, then lock directories WITHOUT /T on (OI)(CI)F
            // so child files cleanly inherit SYSTEM + Administrators Full Control.
            if (Directory.Exists(InstallRootDir))
            {
                CommandRunner.Run("icacls.exe", $"\"{InstallRootDir}\" /setowner \"*S-1-5-32-544\" /C", 15000);
                CommandRunner.Run("icacls.exe",
                    $"\"{InstallRootDir}\" /inheritance:r /grant:r \"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\" \"*S-1-5-32-545:(OI)(CI)RX\"",
                    15000);
            }

            if (Directory.Exists(EngineRootDir))
            {
                CommandRunner.Run("icacls.exe", $"\"{EngineRootDir}\" /setowner \"*S-1-5-32-544\" /C", 15000);
                CommandRunner.Run("icacls.exe",
                    $"\"{EngineRootDir}\" /inheritance:r /grant:r \"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\"",
                    15000);
            }

            CommandRunner.Run("icacls.exe", $"\"{EngineProxiFyreDir}\" /reset /T /C", 15000);
            CommandRunner.Run("icacls.exe", $"\"{EngineProxiFyreDir}\" /setowner \"*S-1-5-32-544\" /T /C", 15000);
            var aclRes = CommandRunner.Run("icacls.exe",
                $"\"{EngineProxiFyreDir}\" /inheritance:r /grant:r \"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\"",
                15000);
            LogService.Info($"Hardened ProxiFyre folder ACLs (exit code {aclRes.ExitCode}).");
        }

        private async Task<bool> InstallAndStartEngineServiceAsync(RyanDpiSettings settings)
        {
            return await Task.Run(async () =>
            {
                ServiceControlHelper.StopAndDeleteService(EngineServiceName);

                string binPath;
                int expectedPort;
                string displayName;
                string description;

                if (settings.UsesBuiltInUsqueService)
                {
                    if (!File.Exists(EngineUsqueExe) || !File.Exists(EngineServiceHostExe))
                    {
                        LogService.Error("Built-in Brazil WARP MASQUE binary (usque.exe) is missing.");
                        return false;
                    }

                    // Pre-register zero-login Cloudflare WARP MASQUE device key if not enrolled yet
                    if (!File.Exists(EngineUsqueConfig))
                    {
                        LogService.Info("Registering zero-login Cloudflare WARP MASQUE key for Brazil Split-Tunnel...");
                        var reg = CommandRunner.Run(EngineUsqueExe, $"register -a -c \"{EngineUsqueConfig}\"", 25000, EngineUsqueDir);
                        LogService.Info($"usque register exit={reg.ExitCode}");
                    }

                    binPath = RyanDpiSettings.BuildUsqueScBinPathValue(EngineServiceHostExe, EngineUsqueExe, EngineUsqueConfig);
                    expectedPort = RyanDpiSettings.UsqueSocksPort;
                    displayName = "RyanDPI Geo-Unblock Engine (WARP MASQUE)";
                    description = "RyanDPI Discord-only zero-game-lag split-tunnel engine.";
                }
                else
                {
                    binPath = settings.BuildScBinPathValue(EngineCiadpiExe);
                    expectedPort = RyanDpiSettings.DefaultSocksPort;
                    displayName = "RyanDPI Engine";
                    description = "RyanDPI local full-speed DPI bypass engine.";
                }

                LogService.Info($"Registering {EngineServiceName} service ({settings.Region})...");
                var createRes = await CreateServiceWithRetryAsync(
                    $"create {EngineServiceName} binPath= \"{binPath}\" start= auto type= own DisplayName= \"{displayName}\"");

                if (createRes.ExitCode != 0)
                {
                    LogService.Error($"Failed to create {EngineServiceName} service: {createRes.CombinedOutput}");
                    return false;
                }

                CommandRunner.Run("sc.exe", $"description {EngineServiceName} \"{description}\"", 10000);
                CommandRunner.Run("sc.exe", $"failure {EngineServiceName} reset= 86400 actions= restart/3000/restart/5000/restart/10000", 10000);

                var startRes = CommandRunner.Run("sc.exe", $"start {EngineServiceName}", 15000);
                LogService.Info($"{EngineServiceName} start exit code: {startRes.ExitCode}");

                var svcRunning = await WaitForServiceRunningAsync(EngineServiceName, 8000);
                var portUp = svcRunning && await WaitForLocalSocksPortAsync(expectedPort, settings.UsesBuiltInUsqueService ? 12000 : 5000);
                LogService.Info($"Engine service running={svcRunning}, local SOCKS5 127.0.0.1:{expectedPort} ready={portUp}");
                return portUp;
            });
        }

        private async Task<bool> InstallAndStartRouterServiceAsync()
        {
            return await Task.Run(async () =>
            {
                ServiceControlHelper.StopAndDeleteService(RouterServiceName);

                LogService.Info($"Registering {RouterServiceName} (delayed-auto, depends on NDISRD + {EngineServiceName})...");
                var createRes = await CreateServiceWithRetryAsync(
                    $"create {RouterServiceName} binPath= \"\\\"{EngineProxiFyreExe}\\\"\" start= delayed-auto depend= {NdisDriverServiceName}/{EngineServiceName} DisplayName= \"RyanDPI Router (ProxiFyre)\"");

                if (createRes.ExitCode != 0)
                {
                    LogService.Error($"Failed to create {RouterServiceName}: {createRes.CombinedOutput}");
                    return false;
                }

                CommandRunner.Run("sc.exe", $"description {RouterServiceName} \"RyanDPI system-wide transparent application router.\"", 10000);
                CommandRunner.Run("sc.exe", $"failure {RouterServiceName} reset= 86400 actions= restart/3000/restart/5000/restart/10000", 10000);

                var startRes = CommandRunner.Run("sc.exe", $"start {RouterServiceName}", 20000);
                LogService.Info($"{RouterServiceName} start exit code: {startRes.ExitCode}");

                var running = await WaitForServiceRunningAsync(RouterServiceName, 5000);
                if (!running)
                {
                    LogService.Error($"{RouterServiceName} failed to reach RUNNING state: {startRes.CombinedOutput}");
                }
                return running;
            });
        }

        private static async Task<CommandResult> CreateServiceWithRetryAsync(string scCreateArgs)
        {
            CommandResult last = default;
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                last = CommandRunner.Run("sc.exe", scCreateArgs, 15000);
                if (last.ExitCode == 0) return last;

                // 1072 = ERROR_SERVICE_MARKED_FOR_DELETE (SCM still finalizing previous delete)
                if (last.ExitCode == 1072 || last.CombinedOutput.Contains("1072"))
                {
                    LogService.Warn($"Service marked for deletion (attempt {attempt}/5); waiting for SCM...");
                    await Task.Delay(500);
                    continue;
                }

                break;
            }
            return last;
        }

        private static async Task<bool> WaitForServiceRunningAsync(string serviceName, int maxWaitMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                if (ServiceControlHelper.QueryServiceState(serviceName) == ComponentState.Running)
                {
                    return true;
                }
                await Task.Delay(250);
            }
            return false;
        }

        private static async Task ConfigureFirewallRulesAsync()
        {
            await Task.Run(() =>
            {
                RemoveFirewallRules();

                var rules = new (string Name, string Program)[]
                {
                    ("RyanDPI Engine", EngineCiadpiExe),
                    ("RyanDPI Geo Engine", EngineUsqueExe),
                    ("RyanDPI Router", EngineProxiFyreExe),
                    ("RyanDPI Encrypted DNS", EngineDnsCryptExe)
                };

                foreach (var (name, prog) in rules)
                {
                    if (!File.Exists(prog)) continue;
                    // Add explicit TCP + UDP rules across all profiles (Domain, Private, Public)
                    // so Windows Firewall never drops packets redirected to ProxiFyre's local listener.
                    var tcpIn = CommandRunner.Run("netsh.exe",
                        $"advfirewall firewall add rule name=\"{name} TCP-In\" dir=in action=allow protocol=TCP profile=any program=\"{prog}\" enable=yes", 10000);
                    var udpIn = CommandRunner.Run("netsh.exe",
                        $"advfirewall firewall add rule name=\"{name} UDP-In\" dir=in action=allow protocol=UDP profile=any program=\"{prog}\" enable=yes", 10000);
                    var anyOut = CommandRunner.Run("netsh.exe",
                        $"advfirewall firewall add rule name=\"{name} Out\" dir=out action=allow profile=any program=\"{prog}\" enable=yes", 10000);

                    if (tcpIn.ExitCode != 0 || udpIn.ExitCode != 0 || anyOut.ExitCode != 0)
                    {
                        LogService.Warn($"Firewall rule warning for {name}: tcpIn={tcpIn.ExitCode}, udpIn={udpIn.ExitCode}, out={anyOut.ExitCode}");
                    }
                }

                LogService.Info("Windows Firewall allow rules configured (TCP/UDP, all profiles).");
            });
        }

        private static void RemoveFirewallRules()
        {
            var names = new[]
            {
                "RyanDPI Engine TCP-In", "RyanDPI Engine UDP-In", "RyanDPI Engine In", "RyanDPI Engine Out",
                "RyanDPI Geo Engine TCP-In", "RyanDPI Geo Engine UDP-In", "RyanDPI Geo Engine In", "RyanDPI Geo Engine Out",
                "RyanDPI Router TCP-In", "RyanDPI Router UDP-In", "RyanDPI Router In", "RyanDPI Router Out",
                "RyanDPI Encrypted DNS TCP-In", "RyanDPI Encrypted DNS UDP-In", "RyanDPI Encrypted DNS In", "RyanDPI Encrypted DNS Out",
                "ProxiFyre Inbound", "ProxiFyre Outbound",
                "ByeDPI ciadpi Inbound", "ByeDPI ciadpi Outbound"
            };

            foreach (var name in names)
            {
                CommandRunner.Run("netsh.exe", $"advfirewall firewall delete rule name=\"{name}\"", 8000);
            }
        }

        /// <summary>
        /// Registers a zero-CPU, event-triggered Windows Scheduled Task (Event 10000: Network Connected)
        /// so switching between Ethernet, Wi-Fi, USB tethering, or booting on Wi-Fi automatically
        /// rebinds ProxiFyreService, refreshes DNS, and clears stale per-ISP cache without needing Repair.
        /// </summary>
        private static async Task ConfigureNetworkWatchTaskAsync(RyanDpiSettings settings)
        {
            await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(EngineRootDir);

                    var scriptContent = BuildNetworkWatchScript(settings.DnsMode);
                    File.WriteAllText(EngineNetworkWatchScript, scriptContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                    // Record current active route signature so switching networks later is detected accurately
                    var captureSigScript = $@"
$r = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Sort-Object RouteMetric, InterfaceMetric | Select-Object -First 1
if ($r) {{
    $ip = (Get-NetIPAddress -InterfaceIndex $r.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -First 1).IPAddress
    ""$($r.InterfaceIndex)|$ip|$($r.NextHop)"" | Set-Content -Path '{EngineLastNetworkStateFile.Replace("'", "''")}' -Encoding UTF8 -Force
}}
";
                    CommandRunner.RunPowerShell(captureSigScript, 10000);

                    var xmlContent = BuildNetworkWatchTaskXml(EngineNetworkWatchScript);
                    var xmlTempPath = Path.Combine(Path.GetTempPath(), $"ryandpi_netwatch_{Environment.ProcessId}.xml");
                    File.WriteAllText(xmlTempPath, xmlContent, Encoding.Unicode);

                    try
                    {
                        var res = CommandRunner.Run("schtasks.exe", $"/Create /TN \"{NetworkWatchTaskName}\" /XML \"{xmlTempPath}\" /F", 15000);
                        if (res.ExitCode == 0)
                        {
                            LogService.Info("Configured automatic Wi-Fi / network-switch self-healing task (RyanDpiNetworkWatch).");
                        }
                        else
                        {
                            LogService.Warn($"Could not register {NetworkWatchTaskName} scheduled task (exit {res.ExitCode}): {res.CombinedOutput}");
                        }
                    }
                    finally
                    {
                        try { if (File.Exists(xmlTempPath)) File.Delete(xmlTempPath); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warn($"Network watcher setup warning: {ex.Message}");
                }
            });
        }

        private static void RemoveNetworkWatchTask()
        {
            CommandRunner.Run("schtasks.exe", $"/Delete /TN \"{NetworkWatchTaskName}\" /F", 10000);
            try
            {
                if (File.Exists(EngineNetworkWatchScript)) File.Delete(EngineNetworkWatchScript);
                if (File.Exists(EngineLastNetworkStateFile)) File.Delete(EngineLastNetworkStateFile);
            }
            catch
            {
                // Non-fatal
            }
        }

        public static string BuildNetworkWatchScript(DnsProtectionMode dnsMode)
        {
            var stateFileEsc = EngineLastNetworkStateFile.Replace("'", "''");
            var dnsModeName = dnsMode.ToString();

            return $@"# RyanDPI Automatic Wi-Fi / Network-Switch Self-Healing Helper
$ErrorActionPreference = 'SilentlyContinue'

# Respect Pause: do nothing if RyanDpiEngine is not running
$engineSvc = Get-Service -Name '{EngineServiceName}' -ErrorAction SilentlyContinue
if (-not $engineSvc -or $engineSvc.Status -ne 'Running') {{ exit 0 }}

# Find active IPv4 default gateway route (ignore virtual/VPN-only noise when no real internet route exists)
$routes = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Sort-Object RouteMetric, InterfaceMetric
$route = $null
$adapter = $null
foreach ($cand in $routes) {{
    $a = Get-NetAdapter -InterfaceIndex $cand.InterfaceIndex -ErrorAction SilentlyContinue
    if ($a -and $a.Status -eq 'Up' -and
        $a.InterfaceDescription -notlike '*Virtual*' -and
        $a.InterfaceDescription -notlike '*Hyper-V*' -and
        $a.InterfaceDescription -notlike '*WSL*' -and
        $a.Name -notlike '*vEthernet*' -and
        $a.InterfaceDescription -notlike '*TAP*' -and
        $a.InterfaceDescription -notlike '*TUN*' -and
        $a.InterfaceDescription -notlike '*VPN*' -and
        $a.InterfaceDescription -notlike '*Tailscale*' -and
        $a.InterfaceDescription -notlike '*Bluetooth*') {{
        $route = $cand
        $adapter = $a
        break
    }}
}}
if (-not $route -or -not $adapter) {{ exit 0 }}

$ipv4 = (Get-NetIPAddress -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -First 1).IPAddress
if ([string]::IsNullOrWhiteSpace($ipv4) -or $ipv4.StartsWith('169.254.')) {{ exit 0 }}

$currentSig = ""$($route.InterfaceIndex)|$ipv4|$($route.NextHop)""
$stateFile = '{stateFileEsc}'
$lastSig = if (Test-Path $stateFile) {{ (Get-Content -Path $stateFile -TotalCount 1 -ErrorAction SilentlyContinue).Trim() }} else {{ '' }}

$routerSvc = Get-Service -Name '{RouterServiceName}' -ErrorAction SilentlyContinue
$dnsSvc = Get-Service -Name '{DnsService.DnsCryptServiceName}' -ErrorAction SilentlyContinue
$dnsMode = '{dnsModeName}'

$networkChanged = ($currentSig -ne $lastSig)
$routerStopped = ($routerSvc -and $routerSvc.Status -ne 'Running')
$dnsStopped = ($dnsMode -eq 'EncryptedDnsProxy' -and $dnsSvc -and $dnsSvc.Status -ne 'Running')

# Ensure newly connected adapter (e.g. Work Wi-Fi or USB hotspot) always has RyanDPI DNS applied
$dnsNeedsApply = $false
if ($dnsMode -eq 'EncryptedDnsProxy') {{
    $curDns = (Get-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses
    if (-not $curDns -or $curDns[0] -ne '127.0.0.1') {{ $dnsNeedsApply = $true }}
}}

if (-not $networkChanged -and -not $routerStopped -and -not $dnsStopped -and -not $dnsNeedsApply) {{
    exit 0
}}

Set-Content -Path $stateFile -Value $currentSig -Encoding UTF8 -Force

if ($dnsMode -eq 'EncryptedDnsProxy') {{
    if ($dnsStopped) {{
        Start-Service -Name '{DnsService.DnsCryptServiceName}' -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 600
    }}
    if ($dnsNeedsApply -or $networkChanged) {{
        Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses '127.0.0.1', '::1' -ErrorAction SilentlyContinue
        netsh interface ip set dns ""$($adapter.Name)"" static 127.0.0.1 | Out-Null
        netsh interface ipv6 set dns ""$($adapter.Name)"" static ::1 | Out-Null
        Clear-DnsClientCache -ErrorAction SilentlyContinue
        ipconfig /flushdns | Out-Null
    }}
}} elseif ($dnsMode -eq 'WindowsNativeDoH' -and $networkChanged) {{
    Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses '8.8.8.8', '9.9.9.9', '2001:4860:4860::8888', '2620:fe::9' -ErrorAction SilentlyContinue
    Clear-DnsClientCache -ErrorAction SilentlyContinue
    ipconfig /flushdns | Out-Null
}}

# Re-check RyanDpiEngine wasn't paused while we were checking
$engineSvc = Get-Service -Name '{EngineServiceName}' -ErrorAction SilentlyContinue
if (-not $engineSvc -or $engineSvc.Status -ne 'Running') {{ exit 0 }}

if ($networkChanged) {{
    # Cleanly rebind ProxiFyre WinpkFilter to the new Wi-Fi/Ethernet adapter and clear stale ISP cache in ciadpi
    Stop-Service -Name '{RouterServiceName}' -Force -ErrorAction SilentlyContinue
    Restart-Service -Name '{EngineServiceName}' -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Start-Service -Name '{RouterServiceName}' -ErrorAction SilentlyContinue
}} elseif ($routerStopped) {{
    Start-Service -Name '{RouterServiceName}' -ErrorAction SilentlyContinue
}}
";
        }

        public static string BuildNetworkWatchTaskXml(string scriptPath)
        {
            var escapedArgs = SecurityElement.Escape(
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"");

            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Automatically rebinds RyanDPI router and encrypted DNS when switching between Ethernet, Wi-Fi, or waking from sleep.</Description>
    <Author>RyanDPI</Author>
  </RegistrationInfo>
  <Triggers>
    <EventTrigger>
      <Enabled>true</Enabled>
      <Delay>PT3S</Delay>
      <Subscription>&lt;QueryList&gt;&lt;Query Id=&quot;0&quot; Path=&quot;Microsoft-Windows-NetworkProfile/Operational&quot;&gt;&lt;Select Path=&quot;Microsoft-Windows-NetworkProfile/Operational&quot;&gt;*[System[(EventID=10000)]]&lt;/Select&gt;&lt;/Query&gt;&lt;/QueryList&gt;</Subscription>
    </EventTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT1M</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>powershell.exe</Command>
      <Arguments>{escapedArgs}</Arguments>
    </Exec>
  </Actions>
</Task>";
        }

        private static void CleanupLegacyDiscordDrover()
        {
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var discordDir = Path.Combine(localAppData, "Discord");
                if (!Directory.Exists(discordDir)) return;

                foreach (var appDir in Directory.GetDirectories(discordDir, "app-*"))
                {
                    var droverIni = Path.Combine(appDir, "drover.ini");
                    var versionDll = Path.Combine(appDir, "version.dll");
                    if (!File.Exists(droverIni)) continue;

                    LogService.Info($"Found legacy drover files in {appDir}; cleaning up...");
                    try { File.Delete(droverIni); } catch { }
                    if (File.Exists(versionDll))
                    {
                        try { File.Delete(versionDll); } catch { }
                    }
                }
            }
            catch
            {
                // Non-fatal
            }
        }

        private static async Task<bool> WaitForLocalSocksPortAsync(int port, int maxWaitMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                if (await ProbeTcpPortAsync("127.0.0.1", port, 500))
                {
                    return true;
                }
                await Task.Delay(250);
            }
            return false;
        }

        private static async Task<bool> ProbeTcpPortAsync(string host, int port, int timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(timeoutMs);
                await client.ConnectAsync(host, port, cts.Token);
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<IPAddress[]> DnsResolutionAsync(string host)
        {
            using var cts = new CancellationTokenSource(4000);
            return await System.Net.Dns.GetHostAddressesAsync(host, cts.Token);
        }

        private sealed class ProxiFyreConfigDto
        {
            [JsonPropertyName("logLevel")]
            public string LogLevel { get; set; } = "Error";

            [JsonPropertyName("bypassLan")]
            public bool BypassLan { get; set; } = true;

            [JsonPropertyName("proxies")]
            public List<ProxiFyreRuleDto> Proxies { get; set; } = new();

            [JsonPropertyName("excludes")]
            public List<string> Excludes { get; set; } = new();
        }

        private sealed class ProxiFyreRuleDto
        {
            [JsonPropertyName("appNames")]
            public List<string> AppNames { get; set; } = new();

            [JsonPropertyName("socks5ProxyEndpoint")]
            public string Socks5ProxyEndpoint { get; set; } = string.Empty;

            [JsonPropertyName("username")]
            public string? Username { get; set; }

            [JsonPropertyName("password")]
            public string? Password { get; set; }

            [JsonPropertyName("supportedProtocols")]
            public List<string> SupportedProtocols { get; set; } = new();
        }
    }
}
