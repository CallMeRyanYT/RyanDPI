using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
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
        public static readonly string FallbackProxiFyreRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RyanDPI");
        public static readonly string FallbackProxiFyreDir = Path.Combine(FallbackProxiFyreRoot, "proxifyre");
        public static readonly string FallbackProxiFyreExe = Path.Combine(FallbackProxiFyreDir, "ProxiFyre.exe");
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

        private static string _activeProxiFyreDir = EngineProxiFyreDir;
        public static string ActiveProxiFyreDir => _activeProxiFyreDir;
        public static string ActiveProxiFyreExe => Path.Combine(_activeProxiFyreDir, "ProxiFyre.exe");
        public static string ActiveProxiFyreConfig => Path.Combine(_activeProxiFyreDir, "app-config.json");

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
                if (settings.Region == RegionPreset.Brazil &&
                    settings.BrazilTunnel == BrazilTunnelMode.CustomSocks5 &&
                    !TryParseEndpoint(settings.CustomSocksEndpoint, out _, out _))
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = LocalizationService.T("Msg_CustomSocksInvalid")
                    };
                }

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

                await ConfigureFirewallRulesAsync(settings);

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

                    var rootReason = DetectRootAclPolicyFailure(ActiveProxiFyreExe);
                    return new OperationResult
                    {
                        Success = false,
                        Message = LocalizationService.T("Msg_RouterStartFailed"),
                        RootAclPolicyFailureReason = rootReason
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

                    // 1. Ensure Windows Packet Filter (NDISRD) driver is installed and running
                    var ndisState = ServiceControlHelper.QueryServiceState(NdisDriverServiceName);
                    if (ndisState == ComponentState.NotInstalled)
                    {
                        LogService.Warn("NDISRD driver is not installed — running prerequisite installer...");
                        var (prereqOk, ndisReboot) = await EnsurePrerequisitesAsync();
                        if (!prereqOk)
                        {
                            return new OperationResult
                            {
                                Success = false,
                                Message = LocalizationService.T("Msg_NdisFailed")
                            };
                        }
                        if (ndisReboot && ServiceControlHelper.QueryServiceState(NdisDriverServiceName) != ComponentState.Running)
                        {
                            return new OperationResult
                            {
                                Success = false,
                                RequiresReboot = true,
                                Message = LocalizationService.T("Msg_RebootNeeded")
                            };
                        }
                    }
                    else if (ndisState != ComponentState.Running)
                    {
                        var ndisStart = CommandRunner.Run("sc.exe", $"start {NdisDriverServiceName}", 15000);
                        LogService.Info($"NDISRD start exit code: {ndisStart.ExitCode}");
                        if (ndisStart.ExitCode != 0 && !string.IsNullOrWhiteSpace(ndisStart.CombinedOutput))
                        {
                            LogService.Warn($"NDISRD start output: {CollapseWhitespace(ndisStart.CombinedOutput)}");
                        }
                    }

                    if (!await WaitForServiceRunningAsync(NdisDriverServiceName, 3000))
                    {
                        LogService.Error("NDISRD driver is not running — a Windows reboot is required to initialize Windows Packet Filter.");
                        return new OperationResult
                        {
                            Success = false,
                            RequiresReboot = true,
                            Message = LocalizationService.T("Msg_RebootNeeded")
                        };
                    }

                    // 2. Ensure ProxiFyre's required VC++ x64 runtime DLLs actually load from System32
                    var missingVcDlls = GetMissingVcRedistDlls();
                    if (missingVcDlls.Count > 0)
                    {
                        LogService.Warn($"Broken or missing VC++ x64 runtime DLLs ({string.Join(", ", missingVcDlls)}) — repairing VC++ Redistributable...");
                        await EnsurePrerequisitesAsync(forceRepairVc: true);
                    }

                    // 3. Start RyanDPI engine first (install it cleanly if missing)
                    CommandResult? engStart = null;
                    if (ServiceControlHelper.QueryServiceState(EngineServiceName) == ComponentState.NotInstalled)
                    {
                        LogService.Warn($"{EngineServiceName} service missing during Start — installing cleanly...");
                        if (!File.Exists(EngineCiadpiExe) || (settings.UsesBuiltInUsqueService && (!File.Exists(EngineUsqueExe) || !File.Exists(EngineServiceHostExe))))
                        {
                            await StageEngineAndConfigAsync(settings);
                        }
                        var installedEng = await InstallAndStartEngineServiceAsync(settings);
                        if (!installedEng)
                        {
                            return new OperationResult
                            {
                                Success = false,
                                Message = LocalizationService.T("Msg_StartEngineFail")
                            };
                        }
                    }
                    else if (ServiceControlHelper.QueryServiceState(EngineServiceName) != ComponentState.Running)
                    {
                        engStart = CommandRunner.Run("sc.exe", $"start {EngineServiceName}", 15000);
                        if (engStart.Value.ExitCode != 0)
                        {
                            LogService.Warn($"{EngineServiceName} start exit code {engStart.Value.ExitCode}: {CollapseWhitespace(engStart.Value.CombinedOutput)}");
                        }
                    }

                    var serviceRunning = await WaitForServiceRunningAsync(EngineServiceName, 8000);
                    var portReady = serviceRunning && await WaitForLocalSocksPortAsync(settings.ActiveLocalEnginePort, settings.UsesBuiltInUsqueService ? 10000 : 5000);
                    if (portReady && settings.UsesBuiltInUsqueService)
                    {
                        // Wait briefly for usque's WARP MASQUE tunnel (port 1083 behind the 1082 UDP-fix bridge) to actually come online
                        var tunnelReady = await WaitForUsqueTunnelReadyAsync(settings.ActiveLocalEnginePort, 15000);
                        if (!tunnelReady)
                        {
                            LogService.Warn("Brazil WARP MASQUE tunnel did not complete handshake within 15s (HTTP/2 fallback will keep retrying in background).");
                            LogUsqueTail();
                        }
                    }
                    if (!portReady)
                    {
                        var qcEng = CommandRunner.Run("sc.exe", $"qc {EngineServiceName}", 8000);
                        LogService.Error($"RyanDPI engine failed verification (serviceRunning={serviceRunning}, port=127.0.0.1:{settings.ActiveLocalEnginePort}, scExit={engStart?.ExitCode}).");
                        LogService.Warn($"[Engine qc] {CollapseWhitespace(qcEng.CombinedOutput)}");
                        if (settings.UsesBuiltInUsqueService) LogUsqueTail();
                        return new OperationResult
                        {
                            Success = false,
                            Message = LocalizationService.T("Msg_StartEngineFail")
                        };
                    }

                    // 4. Heal ProxiFyreService registration (binPath + depend= NDISRD/RyanDpiEngine) and start router
                    CommandResult? rtrStart = null;
                    bool routerRunning;
                    if (ServiceControlHelper.QueryServiceState(RouterServiceName) == ComponentState.NotInstalled)
                    {
                        LogService.Warn($"{RouterServiceName} service missing during Start — registering cleanly...");
                        if (!AreProxiFyreFilesIntact())
                        {
                            RestageProxiFyreDirectory(settings);
                        }
                        routerRunning = await InstallAndStartRouterServiceAsync();
                    }
                    else
                    {
                        EnsureProxiFyreServiceConfigured();
                        if (ServiceControlHelper.QueryServiceState(RouterServiceName) != ComponentState.Running)
                        {
                            rtrStart = CommandRunner.Run("sc.exe", $"start {RouterServiceName}", 20000);
                            if (rtrStart.Value.ExitCode != 0)
                            {
                                LogService.Warn($"{RouterServiceName} start exit code {rtrStart.Value.ExitCode}: {CollapseWhitespace(rtrStart.Value.CombinedOutput)}");
                            }
                        }
                        routerRunning = await WaitForServiceRunningAsync(RouterServiceName, 6000);
                    }

                    // 5. Automatic one-shot self-repair if ProxiFyreService still failed to reach Running
                    if (!routerRunning)
                    {
                        LogProxiFyreFailureDiagnostics(rtrStart);
                        LogService.Warn("ProxiFyreService did not reach Running state — running automatic self-repair (VC++ LoadLibraryEx check, ProxiFyre policy/fallback path, ACLs & service re-registration)...");

                        bool vcBroken = GetMissingVcRedistDlls().Count > 0;
                        var (_, repairReboot) = await EnsurePrerequisitesAsync(forceRepairVc: vcBroken);
                        RestageProxiFyreDirectory(settings);
                        await ConfigureFirewallRulesAsync(settings);

                        // Ensure engine is still running after repair prep
                        if (ServiceControlHelper.QueryServiceState(EngineServiceName) != ComponentState.Running)
                        {
                            CommandRunner.Run("sc.exe", $"start {EngineServiceName}", 15000);
                            await WaitForServiceRunningAsync(EngineServiceName, 6000);
                        }

                        routerRunning = await InstallAndStartRouterServiceAsync();
                        if (!routerRunning)
                        {
                            bool ndisNotRunning = ServiceControlHelper.QueryServiceState(NdisDriverServiceName) != ComponentState.Running;
                            if (ndisNotRunning || repairReboot)
                            {
                                return new OperationResult
                                {
                                    Success = false,
                                    RequiresReboot = true,
                                    Message = LocalizationService.T("Msg_RebootNeeded")
                                };
                            }
                            var rootReason = DetectRootAclPolicyFailure(ActiveProxiFyreExe);
                            return new OperationResult
                            {
                                Success = false,
                                Message = LocalizationService.T("Msg_StartRouterFail"),
                                RootAclPolicyFailureReason = rootReason
                            };
                        }

                        LogService.Info("Automatic ProxiFyreService self-repair succeeded!");
                    }

                    // Ensure C:\ has BUILTIN\Users Read & Execute so Electron/Chromium apps (Discord) never fail to launch
                    EnsureSystemDriveUsersReadExecute();

                    // Block browser QUIC UDP/443 in non-Brazil DPI modes so Chrome/Edge/Firefox instantly fall back
                    // from throttled QUIC to full-speed TCP/443 HTTP/2 through ciadpi (fixes Russia YouTube in browsers!)
                    ApplyQuicFallbackRule(settings);

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
                    RemoveQuicBlockRule();
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
                    KillEngineProcesses(includeDnsCrypt: false);

                    // Always keep usque + RyanDPI service host binaries up to date when Brazil Built-in WARP MASQUE is enabled
                    if (settings.UsesBuiltInUsqueService)
                    {
                        var srcUsque = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "usque", "usque.exe");
                        Directory.CreateDirectory(EngineUsqueDir);
                        if (File.Exists(srcUsque) && !File.Exists(EngineUsqueExe))
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

                    if (settings.UsesBuiltInUsqueService && !WindowsServiceHost.IsValidUsqueConfig(EngineUsqueConfig) && File.Exists(EngineUsqueExe))
                    {
                        try { if (File.Exists(EngineUsqueConfig)) File.Delete(EngineUsqueConfig); } catch { }
                        var reg = CommandRunner.Run(EngineUsqueExe, $"register -a -c \"{EngineUsqueConfig}\"", 25000, EngineUsqueDir);
                        LogService.Info($"usque register exit={reg.ExitCode}");
                        if (!WindowsServiceHost.IsValidUsqueConfig(EngineUsqueConfig))
                        {
                            LogService.Warn($"usque register output: {reg.CombinedOutput}");
                        }
                    }

                    if (!AreProxiFyreFilesIntact())
                    {
                        RestageProxiFyreDirectory(settings);
                    }
                    else
                    {
                        WriteProxiFyreConfig(settings);
                        HardenEngineDirectoryAcls();
                    }

                    if (!File.Exists(EngineCiadpiExe))
                    {
                        var srcByeDpi = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "byedpi", "ciadpi.exe");
                        Directory.CreateDirectory(EngineByeDpiDir);
                        if (File.Exists(srcByeDpi))
                        {
                            File.Copy(srcByeDpi, EngineCiadpiExe, overwrite: true);
                        }
                    }

                    var binPath = settings.UsesBuiltInUsqueService
                        ? RyanDpiSettings.BuildUsqueScBinPathValue(EngineServiceHostExe, EngineUsqueExe, EngineUsqueConfig)
                        : settings.BuildScBinPathValue(EngineCiadpiExe);
                    var cfgRes = CommandRunner.Run("sc.exe", $"config {EngineServiceName} binPath= \"{binPath}\"", 15000);
                    LogService.Info($"Updated RyanDPI service parameters for {settings.Region} (exit code {cfgRes.ExitCode}).");

                    // Always keep ProxiFyreService binPath & dependencies aligned with RyanDpiEngine
                    EnsureProxiFyreServiceConfigured();
                });

                // If switching from EncryptedDnsProxy (dnscrypt-proxy running) -> KeepSystemDns (e.g. Turkey -> Brazil),
                // cleanly restore DHCP DNS so adapters never stay stuck on 127.0.0.1!
                if (settings.DnsMode == DnsProtectionMode.KeepSystemDns &&
                    status.DnsProxyState != ComponentState.NotInstalled)
                {
                    LogService.Info("Switching to KeepSystemDns from RyanDPI Encrypted DNS — restoring automatic adapter DNS...");
                    await _dnsService.ResetDnsToAutomaticAsync(removeDnsCryptService: true);
                }
                else
                {
                    var bundledDnsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "dnscrypt-proxy");
                    await _dnsService.ApplyDnsModeAsync(settings.DnsMode, bundledDnsDir);
                }

                // Refresh firewall rules & network-switch watcher (e.g. if DNS mode changed)
                await ConfigureFirewallRulesAsync(settings);
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

                await ConfigureFirewallRulesAsync(settings);
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
                    var binPath = settings.UsesBuiltInUsqueService
                        ? RyanDpiSettings.BuildUsqueScBinPathValue(EngineServiceHostExe, EngineUsqueExe, EngineUsqueConfig)
                        : settings.BuildScBinPathValue(EngineCiadpiExe);
                    CommandRunner.Run("sc.exe", $"config {EngineServiceName} binPath= \"{binPath}\"", 15000);
                    // Heal ProxiFyreService binPath, NDISRD/RyanDpiEngine dependencies & delayed-auto start
                    EnsureProxiFyreServiceConfigured();
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
                await Task.Run(() => KillEngineProcesses(includeDnsCrypt: true));

                await Task.Delay(500);

                foreach (var cleanupDir in new[] { EngineRootDir, FallbackProxiFyreRoot })
                {
                    if (Directory.Exists(cleanupDir))
                    {
                        try
                        {
                            Directory.Delete(cleanupDir, recursive: true);
                            LogService.Info($"Removed engine directory: {cleanupDir}");
                        }
                        catch (Exception ex)
                        {
                            LogService.Warn($"Could not fully remove {cleanupDir}: {ex.Message}");
                        }
                    }
                }
                _activeProxiFyreDir = EngineProxiFyreDir;

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
                if (socksAlive)
                {
                    results.Add(await ProbeBrazilExitCountryAsync(probeHost, probePort, settings));
                }
                else
                {
                    results.Add(new SelfTestCheckResult
                    {
                        Name = LocalizationService.T("SelfTest_BrazilExitCheck"),
                        Passed = false,
                        LatencyMs = 0,
                        Summary = LocalizationService.T("SelfTest_LocalEngineStopped")
                    });
                }

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
                if (settings.UsesBuiltInUsqueService && results.Any(r => !r.Passed))
                {
                    LogUsqueTail();
                }
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
                    ("YouTube Video CDN", "https://i.ytimg.com/generate_204", HttpMethod.Get),
                    (LocalizationService.T("SelfTest_Instagram"), "https://www.instagram.com/", HttpMethod.Head),
                    (LocalizationService.T("SelfTest_RobloxApp"), "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer", HttpMethod.Get),
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

        private async Task<(bool Ok, bool NeedsReboot)> EnsurePrerequisitesAsync(bool forceRepairVc = false)
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

                // 2. VC++ 2015-2022 x64 Redistributable (verified via LoadLibraryEx on all 4 DLLs ProxiFyre preloads)
                if (forceRepairVc || !IsVcRedistX64Installed())
                {
                    var vcPath = Path.Combine(prereqDir, "VC_redist.x64.exe");
                    if (File.Exists(vcPath))
                    {
                        var args = forceRepairVc ? "/repair /quiet /norestart" : "/install /quiet /norestart";
                        LogService.Info(forceRepairVc
                            ? "Repairing Visual C++ x64 Redistributable..."
                            : "Installing Visual C++ x64 Redistributable...");
                        var res = CommandRunner.Run(vcPath, args, 120000);
                        LogService.Info($"VC_redist.x64 ({(forceRepairVc ? "repair" : "install")}) exit code: {res.ExitCode}");
                        if (res.ExitCode == 3010) needsReboot = true;

                        // If /repair failed (e.g. older version or not registered), fall back to /install
                        if (forceRepairVc && !IsVcRedistX64Installed())
                        {
                            var res2 = CommandRunner.Run(vcPath, "/install /quiet /norestart", 120000);
                            LogService.Info($"VC_redist.x64 (install fallback) exit code: {res2.ExitCode}");
                            if (res2.ExitCode == 3010) needsReboot = true;
                        }
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

        private const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private static readonly string[] RequiredVcRedistDlls =
        {
            "MSVCP140.dll",
            "MSVCP140_ATOMIC_WAIT.dll",
            "VCRUNTIME140.dll",
            "VCRUNTIME140_1.dll"
        };

        private static List<string> GetMissingVcRedistDlls()
        {
            var broken = new List<string>();
            try
            {
                var sysDir = Environment.SystemDirectory;
                foreach (var dll in RequiredVcRedistDlls)
                {
                    var fullPath = Path.Combine(sysDir, dll);
                    if (!File.Exists(fullPath))
                    {
                        broken.Add($"{dll}:missing");
                        continue;
                    }

                    // Match ProxiFyre's exact NativeDependencyLoader check: actually load the x64 DLL from System32
                    var handle = LoadLibraryExW(fullPath, IntPtr.Zero, LOAD_LIBRARY_SEARCH_SYSTEM32);
                    if (handle == IntPtr.Zero)
                    {
                        int err = Marshal.GetLastWin32Error();
                        broken.Add($"{dll}:LoadErr={err}");
                    }
                    else
                    {
                        FreeLibrary(handle);
                    }
                }
            }
            catch (Exception ex)
            {
                broken.Add($"checkErr:{ex.Message}");
            }
            return broken;
        }

        private static int GetDotNetFramework4Release()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
                var rel = key?.GetValue("Release");
                return rel is int i ? i : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static bool IsVcRedistX64Installed()
        {
            try
            {
                // ProxiFyre 2.6.1 explicitly preloads all 4 x64 VC++ 2019+/2022 runtime DLLs from System32.
                // Older VC++ 2015/2017 installs set Registry Installed=1 without MSVCP140_ATOMIC_WAIT.dll,
                // and corrupt/32-bit DLLs dropped into System32 fail LoadLibraryEx even though the file exists!
                if (GetMissingVcRedistDlls().Count > 0)
                {
                    return false;
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
                var installed = key?.GetValue("Installed");
                return installed is int i && i == 1;
            }
            catch
            {
                return false;
            }
        }

        private static (bool Checked, bool IsProtected, string Reason) CheckProxiFyreInstallLocationPolicy(string proxiFyreExePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(proxiFyreExePath);
                if (string.IsNullOrWhiteSpace(dir)) return (false, false, "Invalid path");
                var cfgDll = Path.Combine(dir, "ProxiFyre.Configuration.dll");
                if (!File.Exists(cfgDll) || !File.Exists(proxiFyreExePath))
                {
                    return (false, false, "ProxiFyre files missing");
                }

                // Invoke ProxiFyre's exact ServiceInstallLocationPolicy.IsProtected inside PowerShell 5.1 (.NET Framework 4.x)
                var dllEsc = cfgDll.Replace("'", "''");
                var exeEsc = proxiFyreExePath.Replace("'", "''");
                var ps = $@"
try {{
    Add-Type -Path '{dllEsc}' -ErrorAction Stop
    $reason = $null
    $ok = [ProxiFyre.Configuration.ServiceInstallLocationPolicy]::IsProtected('{exeEsc}', [ref]$reason)
    Write-Output ""POLICY_OK=$ok""
    Write-Output ""POLICY_REASON=$reason""
}} catch {{
    Write-Output ""POLICY_ERR=$($_.Exception.Message)""
}}
";
                var res = CommandRunner.RunPowerShell(ps, 8000);
                bool? ok = null;
                string reason = string.Empty;
                foreach (var line in res.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("POLICY_OK=", StringComparison.OrdinalIgnoreCase))
                    {
                        ok = line[10..].Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith("POLICY_REASON=", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = line[14..].Trim();
                    }
                    else if (line.StartsWith("POLICY_ERR=", StringComparison.OrdinalIgnoreCase))
                    {
                        reason = line[11..].Trim();
                    }
                }

                if (ok.HasValue)
                {
                    return (true, ok.Value, reason);
                }
                return (false, false, CollapseWhitespace(res.CombinedOutput));
            }
            catch (Exception ex)
            {
                return (false, false, ex.Message);
            }
        }

        private static bool AreProxiFyreFilesIntact()
        {
            var dir = ActiveProxiFyreDir;
            var cfg = ActiveProxiFyreConfig;
            if (!Directory.Exists(dir) || !File.Exists(cfg))
                return false;

            foreach (var fileName in ProxiFyreAllowedFiles)
            {
                if (!File.Exists(Path.Combine(dir, fileName)))
                    return false;
            }

            // Ensure no unexpected PE/manifest files exist that would trip ProxiFyre's security policy
            foreach (var existing in Directory.GetFiles(dir))
            {
                var name = Path.GetFileName(existing);
                if (!ProxiFyreAllowedFiles.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                    !name.Equals("app-config.json", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StageProxiFyreIntoDir(string targetDir, string srcProxiFyreDir, RyanDpiSettings settings)
        {
            Directory.CreateDirectory(targetDir);

            // Copy ONLY ProxiFyre's expected binaries so ProxiFyre 2.6.1's
            // EngineServiceInstallLocationPolicy never sees an unexpected PE file
            foreach (var existingFile in Directory.GetFiles(targetDir))
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
                    File.Copy(src, Path.Combine(targetDir, fileName), overwrite: true);
                }
            }

            WriteProxiFyreConfigToDir(settings, targetDir);
            return File.Exists(Path.Combine(targetDir, "ProxiFyre.exe")) &&
                   File.Exists(Path.Combine(targetDir, "app-config.json"));
        }

        private static bool RestageProxiFyreDirectory(RyanDpiSettings settings)
        {
            try
            {
                var srcProxiFyreDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "proxifyre");
                if (!Directory.Exists(srcProxiFyreDir))
                {
                    LogService.Error($"ProxiFyre folder not found: {srcProxiFyreDir}");
                    return false;
                }

                ServiceControlHelper.StopService(RouterServiceName);
                foreach (var p in Process.GetProcessesByName("ProxiFyre"))
                {
                    try { p.Kill(); p.WaitForExit(2500); } catch { }
                    finally { try { p.Dispose(); } catch { } }
                }

                // 1. Primary path: C:\Program Files\RyanDPI\engine\proxifyre
                var primaryOk = StageProxiFyreIntoDir(EngineProxiFyreDir, srcProxiFyreDir, settings);
                HardenEngineDirectoryAcls();

                if (primaryOk)
                {
                    var (polChecked, isProtected, reason) = CheckProxiFyreInstallLocationPolicy(EngineProxiFyreExe);
                    if (!polChecked || isProtected)
                    {
                        _activeProxiFyreDir = EngineProxiFyreDir;
                        return true;
                    }

                    // C:\Program Files (or a parent/file) failed ProxiFyre's strict security policy!
                    // Automatically fall back to C:\ProgramData\RyanDPI\proxifyre (bypasses C:\Program Files completely!)
                    LogService.Warn($"[ProxiFyre Policy] Primary path {EngineProxiFyreDir} rejected: {reason}");
                    LogService.Info($"Testing protected fallback path {FallbackProxiFyreDir}...");

                    Directory.CreateDirectory(FallbackProxiFyreRoot);
                    var fbOk = StageProxiFyreIntoDir(FallbackProxiFyreDir, srcProxiFyreDir, settings);
                    HardenDirectoryAclStrict(FallbackProxiFyreRoot, allowUsersReadExecute: false, lockChildFiles: false);
                    HardenDirectoryAclStrict(FallbackProxiFyreDir, allowUsersReadExecute: false, lockChildFiles: true);

                    if (fbOk)
                    {
                        var (fbChecked, fbProtected, fbReason) = CheckProxiFyreInstallLocationPolicy(FallbackProxiFyreExe);
                        if (fbChecked && fbProtected)
                        {
                            _activeProxiFyreDir = FallbackProxiFyreDir;
                            LogService.Info($"Relocated ProxiFyre cleanly to {FallbackProxiFyreDir} (passed ProxiFyre security policy).");
                            return true;
                        }
                        LogService.Warn($"[ProxiFyre Policy] Fallback path {FallbackProxiFyreDir} also rejected: {fbReason}");
                    }
                }

                _activeProxiFyreDir = EngineProxiFyreDir;
                return primaryOk;
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to re-stage ProxiFyre files: {ex.Message}");
                return false;
            }
        }

        private static bool EnsureProxiFyreServiceConfigured()
        {
            if (ServiceControlHelper.QueryServiceState(RouterServiceName) == ComponentState.NotInstalled)
            {
                return false;
            }

            // Preserve active ProgramData fallback across app restarts if ProxiFyreService was relocated there
            if (_activeProxiFyreDir.Equals(EngineProxiFyreDir, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(FallbackProxiFyreExe))
            {
                var qc = CommandRunner.Run("sc.exe", $"qc {RouterServiceName}", 8000);
                if (qc.StdOut.Contains(FallbackProxiFyreDir, StringComparison.OrdinalIgnoreCase))
                {
                    _activeProxiFyreDir = FallbackProxiFyreDir;
                }
            }

            var cfgRes = CommandRunner.Run(
                "sc.exe",
                $"config {RouterServiceName} binPath= \"\\\"{ActiveProxiFyreExe}\\\"\" start= delayed-auto depend= {NdisDriverServiceName}/{EngineServiceName} DisplayName= \"RyanDPI Router (ProxiFyre)\"",
                15000);
            if (cfgRes.ExitCode != 0)
            {
                LogService.Warn($"Could not update {RouterServiceName} service config (exit {cfgRes.ExitCode}): {CollapseWhitespace(cfgRes.CombinedOutput)}");
            }
            CommandRunner.Run("sc.exe", $"failure {RouterServiceName} reset= 86400 actions= restart/3000/restart/5000/restart/10000", 10000);
            return cfgRes.ExitCode == 0;
        }

        private const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

        public static bool IsSystemDriveRootPolicyFailure(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return false;
            var rootDrive = (Path.GetPathRoot(EngineProxiFyreExe) ?? @"C:\").TrimEnd('\\') + @"\";
            return reason.Contains($"'{rootDrive}'", StringComparison.OrdinalIgnoreCase);
        }

        private static string? DetectRootAclPolicyFailure(string proxiFyreExePath)
        {
            var (polChecked, polOk, polReason) = CheckProxiFyreInstallLocationPolicy(proxiFyreExePath);
            if (polChecked && !polOk && IsSystemDriveRootPolicyFailure(polReason))
            {
                return polReason;
            }
            return null;
        }

        /// <summary>
        /// User-confirmed repair that restores Microsoft's official factory-default permissions
        /// on the system drive root ONLY (no /T recursion — zero subfolders or files touched)
        /// when an overly-permissive non-admin ACE on C:\ blocks ProxiFyre's security policy,
        /// then starts RyanDPI services immediately.
        /// </summary>
        public async Task<OperationResult> RepairSystemRootAclAndStartAsync(RyanDpiSettings settings)
        {
            var rootDrive = (Path.GetPathRoot(EngineProxiFyreExe) ?? @"C:\").TrimEnd('\\') + @"\";
            var repaired = await Task.Run(() =>
            {
                try
                {
                    LogService.Info($"Restoring official Windows default permissions on {rootDrive} root ONLY (user confirmed, non-recursive)...");
                    var before = CommandRunner.Run("icacls.exe", rootDrive, 8000);
                    LogService.Info($"[RootAcl BEFORE {rootDrive}] {CollapseWhitespace(before.CombinedOutput)}");

                    // Inspect current non-trusted direct dangerous SIDs + owner on rootDrive
                    var dangerousNonTrustedSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool ownerUntrusted = false;
                    string originalOwnerSid = TrustedInstallerSid;
                    try
                    {
                        var ds = new DirectoryInfo(rootDrive).GetAccessControl();
                        var owner = ds.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
                        if (owner != null)
                        {
                            originalOwnerSid = owner.Value;
                            ownerUntrusted = !IsTrustedRootSid(owner.Value);
                        }

                        const FileSystemRights dangerousMask =
                            FileSystemRights.DeleteSubdirectoriesAndFiles |
                            FileSystemRights.Delete |
                            FileSystemRights.ChangePermissions |
                            FileSystemRights.TakeOwnership |
                            FileSystemRights.WriteData;

                        foreach (FileSystemAccessRule rule in ds.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                        {
                            if (rule.AccessControlType != AccessControlType.Allow) continue;
                            if ((rule.PropagationFlags & PropagationFlags.InheritOnly) != 0) continue;
                            if (rule.IdentityReference is not SecurityIdentifier sid) continue;
                            if (IsTrustedRootSid(sid.Value)) continue;

                            int raw = (int)rule.FileSystemRights;
                            bool hasGenericDangerous = (raw & 0x10000000) != 0 || (raw & 0x40000000) != 0; // GenericAll | GenericWrite
                            if (hasGenericDangerous || (rule.FileSystemRights & dangerousMask) != 0)
                            {
                                dangerousNonTrustedSids.Add(sid.Value);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Warn($"Could not pre-inspect {rootDrive} ACLs: {ex.Message}");
                        dangerousNonTrustedSids.Add("S-1-5-11");
                    }

                    if (dangerousNonTrustedSids.Count == 0)
                    {
                        dangerousNonTrustedSids.Add("S-1-5-11");
                    }

                    // Helper to run icacls on rootDrive (unquoted 3-char "C:\" avoids trailing backslash escaping!)
                    bool tookOwnership = false;
                    CommandResult RunRootIcacls(string extraArgs)
                    {
                        var r = CommandRunner.Run("icacls.exe", $"{rootDrive} {extraArgs}", 15000);
                        if (r.ExitCode != 0 && !tookOwnership)
                        {
                            LogService.Warn($"icacls {extraArgs} returned exit {r.ExitCode} ({CollapseWhitespace(r.CombinedOutput)}) — taking temporary Administrators ownership of {rootDrive} root ONLY...");
                            var ownRes = CommandRunner.Run("takeown.exe", $"/F {rootDrive} /A", 15000);
                            LogService.Info($"takeown {rootDrive} exit={ownRes.ExitCode}: {CollapseWhitespace(ownRes.CombinedOutput)}");
                            tookOwnership = true;
                            CommandRunner.Run("icacls.exe", $"{rootDrive} /grant \"*S-1-5-18:(OI)(CI)F\"", 15000);
                            CommandRunner.Run("icacls.exe", $"{rootDrive} /grant \"*S-1-5-32-544:(OI)(CI)F\"", 15000);
                            r = CommandRunner.Run("icacls.exe", $"{rootDrive} {extraArgs}", 15000);
                        }
                        return r;
                    }

                    // Always ensure SYSTEM + Administrators keep full control and Users keep Read & Execute on rootDrive
                    // (Chromium/Electron sandboxes such as Discord require BUILTIN\Users RX on C:\ to launch cleanly!)
                    RunRootIcacls("/grant \"*S-1-5-18:(OI)(CI)F\"");
                    RunRootIcacls("/grant \"*S-1-5-32-544:(OI)(CI)F\"");
                    RunRootIcacls("/grant \"*S-1-5-32-545:(OI)(CI)RX\"");

                    foreach (var sid in dangerousNonTrustedSids)
                    {
                        LogService.Info($"Hardening non-admin SID {sid} on {rootDrive} root...");
                        var rem = RunRootIcacls($"/remove:g \"*{sid}\"");
                        LogService.Info($"icacls /remove:g *{sid} exit={rem.ExitCode}");

                        if (sid.Equals("S-1-5-11", StringComparison.OrdinalIgnoreCase))
                        {
                            // Restore Microsoft's exact factory-default Windows 10/11 Authenticated Users ACEs on C:\
                            var g1 = RunRootIcacls("/grant \"*S-1-5-11:(AD)\"");
                            var g2 = RunRootIcacls("/grant \"*S-1-5-11:(OI)(CI)(IO)M\"");
                            LogService.Info($"Restored stock Windows S-1-5-11 rules on {rootDrive} (AD exit={g1.ExitCode}, IO-M exit={g2.ExitCode}).");
                        }
                        else if (sid.Equals("S-1-5-32-545", StringComparison.OrdinalIgnoreCase))
                        {
                            // Restore stock Windows Users Read & Execute on C:\
                            var gUsers = RunRootIcacls("/grant \"*S-1-5-32-545:(OI)(CI)RX\"");
                            LogService.Info($"Restored stock Windows Users RX rule on {rootDrive} (exit={gUsers.ExitCode}).");
                        }
                    }

                    // Restore trusted owner on C:\ (TrustedInstaller if available, else Administrators)
                    if (tookOwnership || ownerUntrusted)
                    {
                        var targetOwner = IsTrustedRootSid(originalOwnerSid) ? originalOwnerSid : TrustedInstallerSid;
                        var setOwn = CommandRunner.Run("icacls.exe", $"{rootDrive} /setowner \"*{targetOwner}\"", 15000);
                        if (setOwn.ExitCode != 0 && !targetOwner.Equals("S-1-5-32-544", StringComparison.OrdinalIgnoreCase))
                        {
                            setOwn = CommandRunner.Run("icacls.exe", $"{rootDrive} /setowner \"*S-1-5-32-544\"", 15000);
                        }
                        LogService.Info($"Restored trusted owner on {rootDrive} root (exit={setOwn.ExitCode}).");
                    }

                    var after = CommandRunner.Run("icacls.exe", rootDrive, 8000);
                    LogService.Info($"[RootAcl AFTER {rootDrive}] {CollapseWhitespace(after.CombinedOutput)}");

                    // Re-verify ProxiFyre's exact ServiceInstallLocationPolicy now
                    RestageProxiFyreDirectory(settings);
                    var (polChecked, polOk, polReason) = CheckProxiFyreInstallLocationPolicy(ActiveProxiFyreExe);
                    LogService.Info($"Post-repair ProxiFyre policy check: checked={polChecked}, isProtected={polOk}, reason={(string.IsNullOrEmpty(polReason) ? "OK" : polReason)}");
                    return !polChecked || polOk;
                }
                catch (Exception ex)
                {
                    LogService.Error($"System root ACL repair failed: {ex.Message}");
                    return false;
                }
            });

            if (!repaired)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = LocalizationService.T("Msg_RootAclRepairFailed")
                };
            }

            await ConfigureFirewallRulesAsync(settings);
            return await StartAsync(settings);
        }

        private static bool IsTrustedRootSid(string sid) =>
            sid.Equals("S-1-5-18", StringComparison.OrdinalIgnoreCase) ||
            sid.Equals("S-1-5-32-544", StringComparison.OrdinalIgnoreCase) ||
            sid.Equals(TrustedInstallerSid, StringComparison.OrdinalIgnoreCase);

        private static string CollapseWhitespace(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return string.Join(" ", text.Replace("\r", " ").Replace("\n", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static void LogProxiFyreFailureDiagnostics(CommandResult? startResult)
        {
            try
            {
                var ndisState = ServiceControlHelper.QueryServiceState(NdisDriverServiceName);
                var engState = ServiceControlHelper.QueryServiceState(EngineServiceName);
                var rtrState = ServiceControlHelper.QueryServiceState(RouterServiceName);
                var missingVc = GetMissingVcRedistDlls();
                var net4Release = GetDotNetFramework4Release();
                LogService.Warn(
                    $"ProxiFyre diagnostics: NDISRD={ndisState}, {EngineServiceName}={engState}, {RouterServiceName}={rtrState}, " +
                    $"NetFx4Release={net4Release} (need>=461808), BrokenVcDlls=[{(missingVc.Count == 0 ? "none" : string.Join(",", missingVc))}], ActiveDir={ActiveProxiFyreDir}");

                // Probe 1: Run ProxiFyre.exe --help (runs NativeDependencyLoader.Initialize + verifies .NET 4.7.2 & AV execution)
                if (File.Exists(ActiveProxiFyreExe))
                {
                    var helpRes = CommandRunner.Run(ActiveProxiFyreExe, "--help", 6000, ActiveProxiFyreDir);
                    var helpOut = CollapseWhitespace(helpRes.StdOut);
                    if (helpOut.Length > 140) helpOut = helpOut[..140] + "...";
                    var helpErr = CollapseWhitespace(helpRes.StdErr);
                    LogService.Warn($"[ProxiFyre --help] exit={helpRes.ExitCode} | err={(string.IsNullOrEmpty(helpErr) ? "<none>" : helpErr)} | out={(string.IsNullOrEmpty(helpOut) ? "<none>" : helpOut)}");

                    // Probe 2: Run ProxiFyre's exact ServiceInstallLocationPolicy.IsProtected check
                    var (polChecked, polOk, polReason) = CheckProxiFyreInstallLocationPolicy(ActiveProxiFyreExe);
                    LogService.Warn($"[ProxiFyre Policy] checked={polChecked}, isProtected={polOk}, reason={(string.IsNullOrEmpty(polReason) ? "<none>" : polReason)}");
                    if (polChecked && !polOk && IsSystemDriveRootPolicyFailure(polReason))
                    {
                        var rootDrive = Path.GetPathRoot(ActiveProxiFyreExe) ?? @"C:\";
                        var rootAcl = CommandRunner.Run("icacls.exe", rootDrive, 8000);
                        LogService.Warn($"[RootAcl {rootDrive}] {CollapseWhitespace(rootAcl.CombinedOutput)}");
                    }
                }

                if (startResult.HasValue && (startResult.Value.ExitCode != 0 || !string.IsNullOrWhiteSpace(startResult.Value.CombinedOutput)))
                {
                    LogService.Warn($"[sc start {RouterServiceName}] exit={startResult.Value.ExitCode}: {CollapseWhitespace(startResult.Value.CombinedOutput)}");
                }

                var qc = CommandRunner.Run("sc.exe", $"qc {RouterServiceName}", 8000);
                if (!string.IsNullOrWhiteSpace(qc.CombinedOutput))
                {
                    LogService.Warn($"[sc qc {RouterServiceName}] {CollapseWhitespace(qc.CombinedOutput)}");
                }

                // Read latest ProxiFyre NLog output (if ProxiFyre reached managed initialization)
                var pfLogsDir = Path.Combine(ActiveProxiFyreDir, "logs");
                if (Directory.Exists(pfLogsDir))
                {
                    var newestLog = new DirectoryInfo(pfLogsDir)
                        .GetFiles("*.log")
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .FirstOrDefault();
                    if (newestLog != null)
                    {
                        var tail = File.ReadAllLines(newestLog.FullName)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .TakeLast(10)
                            .ToList();
                        if (tail.Count > 0)
                        {
                            LogService.Warn($"[ProxiFyre log: {newestLog.Name}]");
                            foreach (var line in tail)
                            {
                                LogService.Warn($"  {line.Trim()}");
                            }
                        }
                    }
                    else
                    {
                        LogService.Warn($"[ProxiFyre log] No .log files found in {pfLogsDir}.");
                    }
                }
                else
                {
                    LogService.Warn($"[ProxiFyre log] Logs folder {pfLogsDir} does not exist (ProxiFyre exited before NLog init).");
                }

                // Capture recent System / Application Event Log errors mentioning ProxiFyre, NDISRD, socksify, or .NET crash
                const string evScript = @"
$since = (Get-Date).AddMinutes(-3)
$evts = @()
foreach ($ln in @('System', 'Application')) {
    try {
        $evts += Get-WinEvent -FilterHashtable @{ LogName = $ln; Level = 1,2,3; StartTime = $since } -MaxEvents 25 -ErrorAction SilentlyContinue
    } catch {}
}
$evts | Where-Object {
    $_.Message -match 'ProxiFyre|NDISRD|RyanDpi|socksify|VCRUNTIME|MSVCP140' -or
    $_.ProviderName -match 'ProxiFyre|\.NET Runtime|Application Error'
} | Sort-Object TimeCreated -Descending | Select-Object -First 4 | ForEach-Object {
    $msg = ($_.Message -replace '\r?\n', ' ') -replace '\s+', ' '
    if ($msg.Length -gt 240) { $msg = $msg.Substring(0, 240) + '...' }
    ""[$($_.LogName)/$($_.ProviderName) #$($_.Id)] $msg""
}
";
                var evRes = CommandRunner.RunPowerShell(evScript, 6000);
                if (!string.IsNullOrWhiteSpace(evRes.StdOut))
                {
                    foreach (var line in evRes.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        LogService.Warn($"[EventLog] {line.Trim()}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Warn($"Could not collect ProxiFyre diagnostics: {ex.Message}");
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
                    KillEngineProcesses(includeDnsCrypt: false);

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

                    // Stage ProxiFyre cleanly and verify ProxiFyre's ServiceInstallLocationPolicy
                    // (automatically falls back to C:\ProgramData\RyanDPI\proxifyre if C:\Program Files has broken ACLs)
                    var pfStaged = RestageProxiFyreDirectory(settings);

                    LogService.Info($"Staged RyanDPI engine files cleanly to {EngineRootDir} (ProxiFyre={ActiveProxiFyreDir})");
                    return File.Exists(EngineCiadpiExe) && pfStaged && File.Exists(ActiveProxiFyreExe);
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
                EngineServiceHostExe,
                RouterServiceName,
                "ProxiFyre",
                "ProxiFyre.exe",
                EngineProxiFyreExe,
                FallbackProxiFyreExe,
                DnsService.DnsCryptServiceName,
                "dnscrypt-proxy.exe",
                EngineDnsCryptExe,
                "System",
                // Third-party VPN coexistence exclusions (prevent routing loops if Proton VPN / WireGuard / WARP / Tailscale is installed or active)
                "ProtonVPN",
                "ProtonVPNService",
                "ProtonVPN.Client",
                "ProtonVPN.WireGuardService",
                "wireguard",
                "openvpn",
                "warp-svc",
                "mullvad-daemon",
                "nordvpn-service",
                "tailscaled",
                "Tailscale",
                "zerotier-one_x64"
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
                var brApps = new List<string>
                {
                    "Discord",
                    "DiscordPTB",
                    "DiscordCanary",
                    @"Discord\Update.exe",
                    @"DiscordPTB\Update.exe",
                    @"DiscordCanary\Update.exe"
                };
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
            WriteProxiFyreConfigToDir(settings, ActiveProxiFyreDir);
            if (!ActiveProxiFyreDir.Equals(EngineProxiFyreDir, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(EngineProxiFyreDir))
            {
                try { WriteProxiFyreConfigToDir(settings, EngineProxiFyreDir, logInfo: false); } catch { }
            }
            if (!ActiveProxiFyreDir.Equals(FallbackProxiFyreDir, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(FallbackProxiFyreDir))
            {
                try { WriteProxiFyreConfigToDir(settings, FallbackProxiFyreDir, logInfo: false); } catch { }
            }
        }

        private static void WriteProxiFyreConfigToDir(RyanDpiSettings settings, string targetDir, bool logInfo = true)
        {
            var json = BuildProxiFyreConfigJson(settings);
            Directory.CreateDirectory(targetDir);
            var cfgPath = Path.Combine(targetDir, "app-config.json");
            if (File.Exists(cfgPath))
            {
                try { File.SetAttributes(cfgPath, FileAttributes.Normal); } catch { }
            }
            File.WriteAllText(cfgPath, json);
            if (!logInfo) return;

            if (settings.Region == RegionPreset.Brazil)
            {
                var ep = settings.BrazilTunnel == BrazilTunnelMode.CustomSocks5
                    ? settings.CustomSocksEndpoint
                    : $"127.0.0.1:{RyanDpiSettings.UsqueSocksPort} (WARP MASQUE)";
                LogService.Info($"Wrote Brazil Discord-Only Split-Tunnel ProxiFyre config (Endpoint={ep}, UDP={settings.BrazilIncludeUdp}, Browsers={settings.BrazilIncludeBrowsers}, Games=100% Direct).");
            }
            else
            {
                LogService.Info($"Wrote system-wide ProxiFyre config (Region={settings.Region}, UDP={settings.TunnelUdp}, BypassLan={settings.BypassLan}, UserExcludes={settings.ExcludedApps.Count}).");
            }
        }

        private static void KillEngineProcesses(bool includeDnsCrypt)
        {
            var names = includeDnsCrypt
                ? new[] { "ProxiFyre", "ciadpi", "dnscrypt-proxy", "usque" }
                : new[] { "ProxiFyre", "ciadpi", "usque" };

            foreach (var procName in names)
            {
                foreach (var p in Process.GetProcessesByName(procName))
                {
                    try { p.Kill(); p.WaitForExit(2500); } catch { }
                    finally { try { p.Dispose(); } catch { } }
                }
            }

            // Also terminate any lingering Brazil usque SCM host (engine\usque\RyanDPI.exe), never our own GUI process
            int selfPid = Environment.ProcessId;
            foreach (var p in Process.GetProcessesByName("RyanDPI"))
            {
                try
                {
                    if (p.Id == selfPid) continue;
                    var path = p.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path) &&
                        path.StartsWith(EngineRootDir, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill();
                        p.WaitForExit(2500);
                    }
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
        }

        private static void HardenDirectoryAclStrict(string dirPath, bool allowUsersReadExecute, bool lockChildFiles)
        {
            if (!Directory.Exists(dirPath)) return;

            // 1. Set owner to BUILTIN\Administrators FIRST via icacls before modifying ACLs
            var ownerRes = CommandRunner.Run("icacls.exe", $"\"{dirPath}\" /setowner \"*S-1-5-32-544\" {(lockChildFiles ? "/T " : "")}/C", 15000);
            if (ownerRes.ExitCode != 0)
            {
                LogService.Warn($"icacls /setowner warning on {dirPath} (exit {ownerRes.ExitCode}): {CollapseWhitespace(ownerRes.CombinedOutput)}");
            }

            // 2. Apply exact native .NET DirectorySecurity (100% locale-independent, removes ALL non-trusted ACEs including CREATOR OWNER)
            try
            {
                var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

                var di = new DirectoryInfo(dirPath);
                var ds = new DirectorySecurity();
                ds.SetOwner(adminSid);
                ds.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                ds.AddAccessRule(new FileSystemAccessRule(
                    systemSid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
                ds.AddAccessRule(new FileSystemAccessRule(
                    adminSid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow));
                if (allowUsersReadExecute)
                {
                    ds.AddAccessRule(new FileSystemAccessRule(
                        usersSid, FileSystemRights.ReadAndExecute,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None, AccessControlType.Allow));
                }
                di.SetAccessControl(ds);

                if (lockChildFiles)
                {
                    foreach (var file in di.GetFiles())
                    {
                        var fs = new FileSecurity();
                        fs.SetOwner(adminSid);
                        fs.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                        fs.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Allow));
                        fs.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, AccessControlType.Allow));
                        file.SetAccessControl(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Warn($"Native ACL hardening warning on {dirPath}: {ex.Message}");
            }

            // 3. Reinforce with icacls
            var grantSpec = allowUsersReadExecute
                ? "\"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\" \"*S-1-5-32-545:(OI)(CI)RX\""
                : "\"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\"";
            var aclRes = CommandRunner.Run("icacls.exe", $"\"{dirPath}\" /inheritance:r /grant:r {grantSpec}", 15000);
            if (aclRes.ExitCode != 0)
            {
                LogService.Warn($"icacls grant warning on {dirPath} (exit {aclRes.ExitCode}): {CollapseWhitespace(aclRes.CombinedOutput)}");
            }
        }

        private static void HardenEngineDirectoryAcls()
        {
            // ProxiFyre 2.6.1 strictly verifies its folder, every payload file, AND its parent chain
            // have trusted owners (SYSTEM/Administrators/TrustedInstaller) and zero non-admin
            // write/delete/permission-control ACEs (including inherited CREATOR OWNER S-1-3-0).
            HardenDirectoryAclStrict(InstallRootDir, allowUsersReadExecute: true, lockChildFiles: false);
            HardenDirectoryAclStrict(EngineRootDir, allowUsersReadExecute: false, lockChildFiles: false);
            HardenDirectoryAclStrict(EngineProxiFyreDir, allowUsersReadExecute: false, lockChildFiles: true);
            if (Directory.Exists(FallbackProxiFyreDir))
            {
                HardenDirectoryAclStrict(FallbackProxiFyreRoot, allowUsersReadExecute: false, lockChildFiles: false);
                HardenDirectoryAclStrict(FallbackProxiFyreDir, allowUsersReadExecute: false, lockChildFiles: true);
            }
            LogService.Info($"Hardened ProxiFyre folder ACLs ({ActiveProxiFyreDir}).");
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

                    // Pre-register zero-login Cloudflare WARP MASQUE device key if missing or corrupted
                    if (!WindowsServiceHost.IsValidUsqueConfig(EngineUsqueConfig))
                    {
                        try { if (File.Exists(EngineUsqueConfig)) File.Delete(EngineUsqueConfig); } catch { }
                        LogService.Info("Registering zero-login Cloudflare WARP MASQUE key for Brazil Split-Tunnel...");
                        var reg = CommandRunner.Run(EngineUsqueExe, $"register -a -c \"{EngineUsqueConfig}\"", 25000, EngineUsqueDir);
                        LogService.Info($"usque register exit={reg.ExitCode}");
                        if (!WindowsServiceHost.IsValidUsqueConfig(EngineUsqueConfig))
                        {
                            LogService.Warn($"usque register attempt 1 failed ({reg.CombinedOutput}); retrying once...");
                            try { if (File.Exists(EngineUsqueConfig)) File.Delete(EngineUsqueConfig); } catch { }
                            await Task.Delay(1200);
                            var reg2 = CommandRunner.Run(EngineUsqueExe, $"register -a -c \"{EngineUsqueConfig}\"", 25000, EngineUsqueDir);
                            LogService.Info($"usque register retry exit={reg2.ExitCode}");
                        }
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
                    EngineServiceName,
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

                LogService.Info($"Registering {RouterServiceName} (delayed-auto, bin={ActiveProxiFyreExe}, depends on NDISRD + {EngineServiceName})...");
                var createRes = await CreateServiceWithRetryAsync(
                    RouterServiceName,
                    $"create {RouterServiceName} binPath= \"\\\"{ActiveProxiFyreExe}\\\"\" start= delayed-auto depend= {NdisDriverServiceName}/{EngineServiceName} DisplayName= \"RyanDPI Router (ProxiFyre)\"");

                if (createRes.ExitCode != 0)
                {
                    LogService.Error($"Failed to create {RouterServiceName}: {createRes.CombinedOutput}");
                    return false;
                }

                CommandRunner.Run("sc.exe", $"description {RouterServiceName} \"RyanDPI system-wide transparent application router.\"", 10000);
                CommandRunner.Run("sc.exe", $"failure {RouterServiceName} reset= 86400 actions= restart/3000/restart/5000/restart/10000", 10000);

                var startRes = CommandRunner.Run("sc.exe", $"start {RouterServiceName}", 20000);
                LogService.Info($"{RouterServiceName} start exit code: {startRes.ExitCode}");

                var running = await WaitForServiceRunningAsync(RouterServiceName, 6000);
                if (!running)
                {
                    LogService.Error($"{RouterServiceName} failed to reach RUNNING state: {CollapseWhitespace(startRes.CombinedOutput)}");
                    LogProxiFyreFailureDiagnostics(startRes);
                }
                return running;
            });
        }

        private static async Task<CommandResult> CreateServiceWithRetryAsync(string serviceName, string scCreateArgs)
        {
            CommandResult last = default;
            for (int attempt = 1; attempt <= 8; attempt++)
            {
                last = CommandRunner.Run("sc.exe", scCreateArgs, 15000);
                if (last.ExitCode == 0) return last;

                // 1072 = ERROR_SERVICE_MARKED_FOR_DELETE (SCM still finalizing previous delete)
                if (last.ExitCode == 1072 || last.CombinedOutput.Contains("1072"))
                {
                    LogService.Warn($"Service {serviceName} marked for deletion (attempt {attempt}/8); waiting for SCM...");
                    if (serviceName.Equals(EngineServiceName, StringComparison.OrdinalIgnoreCase) ||
                        serviceName.Equals(RouterServiceName, StringComparison.OrdinalIgnoreCase))
                    {
                        KillEngineProcesses(includeDnsCrypt: false);
                    }
                    await Task.Delay(500);
                    continue;
                }

                // 1073 = ERROR_SERVICE_EXISTS (force stop + delete and retry cleanly)
                if (last.ExitCode == 1073 || last.CombinedOutput.Contains("1073"))
                {
                    LogService.Warn($"Service {serviceName} still exists (1073, attempt {attempt}/8); force-removing before retry...");
                    CommandRunner.Run("sc.exe", $"stop \"{serviceName}\"", 10000);
                    if (serviceName.Equals(EngineServiceName, StringComparison.OrdinalIgnoreCase) ||
                        serviceName.Equals(RouterServiceName, StringComparison.OrdinalIgnoreCase))
                    {
                        KillEngineProcesses(includeDnsCrypt: false);
                    }
                    CommandRunner.Run("sc.exe", $"delete \"{serviceName}\"", 10000);
                    ServiceControlHelper.StopAndDeleteService(serviceName, 4000);
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

        private const string QuicBlockRuleName = "RyanDPI Block QUIC UDP-443";

        private static void ApplyQuicFallbackRule(RyanDpiSettings? settings)
        {
            RemoveQuicBlockRule();
            // In Global / Russia / Turkey DPI modes (never Brazil, where usque uses WARP MASQUE over UDP/443!),
            // blocking outbound UDP/443 forces Chrome, Edge, Brave, Opera, Yandex & Firefox to immediately fall back
            // from ISP-throttled HTTP/3 QUIC to full-speed TCP/443 HTTP/2 routed cleanly through ciadpi!
            // Zero impact on games (games don't use UDP/443), Discord Voice (UDP 50000-65535), or dnscrypt-proxy (http3=false).
            if (settings != null && settings.Region != RegionPreset.Brazil)
            {
                var q = CommandRunner.Run("netsh.exe",
                    $"advfirewall firewall add rule name=\"{QuicBlockRuleName}\" dir=out action=block protocol=UDP remoteport=443 profile=any enable=yes",
                    10000);
                if (q.ExitCode == 0)
                {
                    LogService.Info("Enabled browser QUIC (UDP/443) → TCP/443 HTTP/2 fallback rule for full YouTube/browser unblock.");
                }
                else
                {
                    LogService.Warn($"Could not add QUIC fallback rule (exit {q.ExitCode}): {CollapseWhitespace(q.CombinedOutput)}");
                }
            }
        }

        private static void RemoveQuicBlockRule()
        {
            CommandRunner.Run("netsh.exe", $"advfirewall firewall delete rule name=\"{QuicBlockRuleName}\"", 8000);
        }

        private static async Task ConfigureFirewallRulesAsync(RyanDpiSettings? settings = null)
        {
            await Task.Run(() =>
            {
                RemoveFirewallRules();

                var rules = new (string Name, string Program)[]
                {
                    ("RyanDPI Engine", EngineCiadpiExe),
                    ("RyanDPI Geo Engine", EngineUsqueExe),
                    ("RyanDPI Geo Bridge", EngineServiceHostExe),
                    ("RyanDPI Router", ActiveProxiFyreExe),
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

                ApplyQuicFallbackRule(settings);
                LogService.Info("Windows Firewall allow rules configured (TCP/UDP, all profiles).");
            });
        }

        private static void RemoveFirewallRules()
        {
            var names = new[]
            {
                "RyanDPI Engine TCP-In", "RyanDPI Engine UDP-In", "RyanDPI Engine In", "RyanDPI Engine Out",
                "RyanDPI Geo Engine TCP-In", "RyanDPI Geo Engine UDP-In", "RyanDPI Geo Engine In", "RyanDPI Geo Engine Out",
                "RyanDPI Geo Bridge TCP-In", "RyanDPI Geo Bridge UDP-In", "RyanDPI Geo Bridge In", "RyanDPI Geo Bridge Out",
                "RyanDPI Router TCP-In", "RyanDPI Router UDP-In", "RyanDPI Router In", "RyanDPI Router Out",
                "RyanDPI Encrypted DNS TCP-In", "RyanDPI Encrypted DNS UDP-In", "RyanDPI Encrypted DNS In", "RyanDPI Encrypted DNS Out",
                QuicBlockRuleName,
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
    # Cleanly rebind ProxiFyre WinpkFilter to the new Wi-Fi/Ethernet adapter and clear stale ISP cache in ciadpi/usque
    Stop-Service -Name '{RouterServiceName}' -Force -ErrorAction SilentlyContinue
    Restart-Service -Name '{EngineServiceName}' -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 1200
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

        private static void EnsureSystemDriveUsersReadExecute()
        {
            try
            {
                var rootDrive = (Path.GetPathRoot(EngineProxiFyreExe) ?? @"C:\").TrimEnd('\\') + @"\";
                var ds = new DirectoryInfo(rootDrive).GetAccessControl();
                bool hasUsersRx = false;
                foreach (FileSystemAccessRule rule in ds.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (rule.AccessControlType != AccessControlType.Allow) continue;
                    if ((rule.PropagationFlags & PropagationFlags.InheritOnly) != 0) continue;
                    if (rule.IdentityReference is SecurityIdentifier sid &&
                        sid.Value.Equals("S-1-5-32-545", StringComparison.OrdinalIgnoreCase) &&
                        (rule.FileSystemRights & FileSystemRights.ReadAndExecute) == FileSystemRights.ReadAndExecute)
                    {
                        hasUsersRx = true;
                        break;
                    }
                }
                if (!hasUsersRx)
                {
                    var res = CommandRunner.Run("icacls.exe", $"{rootDrive} /grant \"*S-1-5-32-545:(OI)(CI)RX\"", 10000);
                    LogService.Info($"Restored missing BUILTIN\\Users Read & Execute on {rootDrive} root (exit={res.ExitCode}).");
                }
            }
            catch
            {
                // Non-fatal
            }
        }

        private static async Task<bool> WaitForUsqueTunnelReadyAsync(int bridgePort, int maxWaitMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                if (await ProbeSocks5TcpConnectAsync("127.0.0.1", bridgePort, "1.1.1.1", 443, 2500))
                {
                    LogService.Info($"Brazil WARP MASQUE end-to-end SOCKS5 tunnel verified ready in {sw.ElapsedMilliseconds} ms.");
                    return true;
                }
                await Task.Delay(750);
            }
            return false;
        }

        private static async Task<bool> ProbeSocks5TcpConnectAsync(string proxyHost, int proxyPort, string targetIpv4, int targetPort, int timeoutMs)
        {
            try
            {
                using var tcp = new TcpClient();
                using var cts = new CancellationTokenSource(timeoutMs);
                await tcp.ConnectAsync(proxyHost, proxyPort, cts.Token);
                var ns = tcp.GetStream();
                await ns.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, cts.Token);
                var greet = new byte[2];
                int n = await ns.ReadAsync(greet, cts.Token);
                if (n < 2 || greet[0] != 0x05 || greet[1] != 0x00) return false;

                var ipBytes = IPAddress.Parse(targetIpv4).GetAddressBytes();
                var req = new byte[10];
                req[0] = 0x05; req[1] = 0x01; req[2] = 0x00; req[3] = 0x01;
                Buffer.BlockCopy(ipBytes, 0, req, 4, 4);
                req[8] = (byte)(targetPort >> 8);
                req[9] = (byte)(targetPort & 0xFF);
                await ns.WriteAsync(req, cts.Token);

                var rep = new byte[10];
                int r = await ns.ReadAsync(rep, cts.Token);
                return r >= 2 && rep[0] == 0x05 && rep[1] == 0x00;
            }
            catch
            {
                return false;
            }
        }

        private static void LogUsqueTail()
        {
            try
            {
                var logPath = Path.Combine(EngineUsqueDir, "usque.log");
                if (!File.Exists(logPath)) return;
                var tail = File.ReadAllLines(logPath).Where(l => !string.IsNullOrWhiteSpace(l)).TakeLast(10).ToList();
                if (tail.Count == 0) return;
                LogService.Warn("[usque.log tail]");
                foreach (var line in tail)
                {
                    LogService.Warn($"  {line.Trim()}");
                }
            }
            catch { }
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
