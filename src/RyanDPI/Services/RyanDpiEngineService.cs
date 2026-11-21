using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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

        public static readonly string InstallRootDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RyanDPI");

        public static readonly string EngineRootDir = Path.Combine(InstallRootDir, "engine");
        public static readonly string EngineByeDpiDir = Path.Combine(EngineRootDir, "byedpi");
        public static readonly string EngineProxiFyreDir = Path.Combine(EngineRootDir, "proxifyre");
        public static readonly string EngineDnsCryptDir = Path.Combine(EngineRootDir, "dnscrypt");

        public static readonly string EngineCiadpiExe = Path.Combine(EngineByeDpiDir, "ciadpi.exe");
        public static readonly string EngineProxiFyreExe = Path.Combine(EngineProxiFyreDir, "ProxiFyre.exe");
        public static readonly string EngineProxiFyreConfig = Path.Combine(EngineProxiFyreDir, "app-config.json");
        public static readonly string EngineDnsCryptExe = Path.Combine(EngineDnsCryptDir, "dnscrypt-proxy.exe");

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
                    Title = "Preparing system",
                    Detail = "Stopping and removing any conflicting legacy DPI or VPN services..."
                });
                await CleanupLegacyServicesAsync(includeCurrentRyanDpi: true);

                // Step 2: Prerequisites (Windows Packet Filter driver + VC++ runtime)
                progress?.Report(new InstallProgressUpdate
                {
                    StepIndex = 2,
                    TotalSteps = totalSteps,
                    Title = "Checking prerequisites",
                    Detail = "Verifying Windows Packet Filter driver (NDISRD) and VC++ Runtime..."
                });
                var (prereqOk, ndisNeedsReboot) = await EnsurePrerequisitesAsync();
                if (!prereqOk)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = "Failed to install the Windows Packet Filter driver (NDISRD). Check the Logs tab for details."
                    };
                }

                // Step 3: Stage binaries to C:\Program Files\RyanDPI\engine + write all-apps config + ACL harden
                progress?.Report(new InstallProgressUpdate
                {
                    StepIndex = 3,
                    TotalSteps = totalSteps,
                    Title = "Deploying RyanDPI engine",
                    Detail = "Installing system-wide router, full-speed engine, and protected ACL security..."
                });
                var stageOk = await StageEngineAndConfigAsync(settings);
                if (!stageOk)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = "Failed to deploy RyanDPI engine files. Make sure no antivirus quarantined the files."
                    };
                }

                // Step 4: DNS protection + Firewall rules
                progress?.Report(new InstallProgressUpdate
                {
                    StepIndex = 4,
                    TotalSteps = totalSteps,
                    Title = "Configuring Encrypted DNS & Firewall",
                    Detail = "Eliminating ISP DNS poisoning and adding Windows Firewall allow rules..."
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
                    Title = "Starting RyanDPI",
                    Detail = "Launching local DPI desync engine and system-wide transparent router..."
                });

                var engineStarted = await InstallAndStartEngineServiceAsync(settings);
                if (!engineStarted)
                {
                    return new OperationResult
                    {
                        Success = false,
                        Message = "RyanDPI engine service could not start on 127.0.0.1:1080. Check if another app is using port 1080 or check the Logs tab."
                    };
                }

                var routerStarted = await InstallAndStartRouterServiceAsync();
                if (!routerStarted)
                {
                    if (ndisNeedsReboot)
                    {
                        LogService.Warn("ProxiFyreService is installed and will activate automatically after reboot once NDISRD initializes.");
                        return new OperationResult
                        {
                            Success = true,
                            RequiresReboot = true,
                            Message = "RyanDPI is installed! A quick PC restart is needed so Windows can finish loading the new Packet Filter driver."
                        };
                    }

                    return new OperationResult
                    {
                        Success = false,
                        Message = "RyanDPI router service (ProxiFyreService) failed to start. Try restarting your PC or check the Logs tab."
                    };
                }

                LogService.Info("=== RyanDPI installed and running smoothly across all applications ===");
                return new OperationResult
                {
                    Success = true,
                    RequiresReboot = false,
                    Message = $"RyanDPI is active! All apps and websites are unlocked at native internet speed. ({dnsSummary})"
                };
            }
            catch (Exception ex)
            {
                LogService.Error($"Installation failed: {ex.Message}");
                return new OperationResult
                {
                    Success = false,
                    Message = $"Installation error: {ex.Message}"
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

                    var serviceRunning = await WaitForServiceRunningAsync(EngineServiceName, 5000);
                    var portReady = serviceRunning && await WaitForLocalSocksPortAsync(RyanDpiSettings.DefaultSocksPort, 5000);
                    if (!portReady)
                    {
                        LogService.Error($"RyanDPI engine failed verification (serviceRunning={serviceRunning}).");
                        return new OperationResult
                        {
                            Success = false,
                            Message = "RyanDPI engine failed to start on 127.0.0.1:1080. Check Logs for details."
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
                            Message = "RyanDPI Router (ProxiFyreService) could not start. Check Logs for details."
                        };
                    }

                    LogService.Info("RyanDPI services are now running.");
                    return new OperationResult { Success = true, Message = "RyanDPI is now active." };
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
                    return new OperationResult { Success = true, Message = "RyanDPI paused." };
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
                        Message = "Settings saved! Click 'Install & Enable RyanDPI' on Home to activate them."
                    };
                }

                // Perform all blocking service & ACL operations off the UI thread
                await Task.Run(() =>
                {
                    ServiceControlHelper.StopService(RouterServiceName);
                    ServiceControlHelper.StopService(EngineServiceName);

                    WriteProxiFyreConfig(settings);
                    HardenEngineDirectoryAcls();

                    var binPath = settings.BuildScBinPathValue(EngineCiadpiExe);
                    var cfgRes = CommandRunner.Run("sc.exe", $"config {EngineServiceName} binPath= \"{binPath}\"", 15000);
                    LogService.Info($"Updated RyanDPI service parameters (exit code {cfgRes.ExitCode}).");
                });

                // Apply DNS mode change
                var bundledDnsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res", "dnscrypt-proxy");
                await _dnsService.ApplyDnsModeAsync(settings.DnsMode, bundledDnsDir);

                // Refresh firewall rules (e.g. if dnscrypt-proxy was just enabled)
                await ConfigureFirewallRulesAsync();

                // Restart engine & router cleanly
                return await StartAsync(settings);
            }
            catch (Exception ex)
            {
                LogService.Error($"Applying settings failed: {ex.Message}");
                return new OperationResult { Success = false, Message = $"Failed to apply settings: {ex.Message}" };
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
                        Message = "App updated, but refreshing background engine files failed. Click 'Reinstall / Repair'."
                    };
                }

                await ConfigureFirewallRulesAsync();

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
                            ? "Background services upgraded cleanly."
                            : "App updated, but restarting background services failed. Click 'Reinstall / Repair'."
                    };
                }

                await Task.Run(() =>
                {
                    var binPath = settings.BuildScBinPathValue(EngineCiadpiExe);
                    CommandRunner.Run("sc.exe", $"config {EngineServiceName} binPath= \"{binPath}\"", 15000);
                });

                if (wasRunning)
                {
                    return await StartAsync(settings);
                }

                return new OperationResult { Success = true, Message = "Background engine updated (currently paused)." };
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
                        ? $"Cleaned up {removedCount} legacy DPI/tunneling service(s)."
                        : "No legacy conflicting services were found — your system is clean.";
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
                    foreach (var procName in new[] { "ProxiFyre", "ciadpi", "dnscrypt-proxy" })
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
                    Message = "RyanDPI, its background services, firewall rules, and DNS changes have been completely removed."
                };
            }
            catch (Exception ex)
            {
                LogService.Error($"Uninstall failed: {ex.Message}");
                return new OperationResult { Success = false, Message = $"Uninstall error: {ex.Message}" };
            }
        }

        public async Task<List<SelfTestCheckResult>> RunSelfTestAsync()
        {
            var results = new List<SelfTestCheckResult>();

            // 1. Local RyanDPI SOCKS5 Engine socket + service check
            var sw = Stopwatch.StartNew();
            var engineSvcRunning = await Task.Run(() => ServiceControlHelper.QueryServiceState(EngineServiceName) == ComponentState.Running);
            var routerSvcRunning = await Task.Run(() => ServiceControlHelper.QueryServiceState(RouterServiceName) == ComponentState.Running);
            var socksAlive = await ProbeTcpPortAsync("127.0.0.1", RyanDpiSettings.DefaultSocksPort, 1500);
            sw.Stop();

            bool enginePassed = engineSvcRunning && routerSvcRunning && socksAlive;
            string engineSummary = enginePassed
                ? $"Active on 127.0.0.1:{RyanDpiSettings.DefaultSocksPort} (All Apps)"
                : (!engineSvcRunning && socksAlive)
                    ? "Legacy proxy on :1080 — click Install RyanDPI to upgrade"
                    : !engineSvcRunning
                        ? "RyanDPI Engine is not running"
                        : !routerSvcRunning
                            ? "RyanDPI Router (ProxiFyreService) is not running"
                            : $"Port 127.0.0.1:{RyanDpiSettings.DefaultSocksPort} unreachable";

            results.Add(new SelfTestCheckResult
            {
                Name = "RyanDPI Local Engine",
                Passed = enginePassed,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = engineSummary
            });

            // 2. DNS resolution check for blocked Turkey domains
            sw.Restart();
            bool dnsPassed;
            string dnsInfo;
            try
            {
                var dnsTasks = await Task.WhenAll(
                    DnsResolutionAsync("discord.com"),
                    DnsResolutionAsync("clientsettings.roblox.com"));
                dnsPassed = dnsTasks[0].Length > 0 && dnsTasks[1].Length > 0;
                dnsInfo = dnsPassed ? "Clean DNS (Discord & Roblox resolved)" : "DNS lookup returned no addresses";
            }
            catch (Exception ex)
            {
                dnsPassed = false;
                dnsInfo = $"DNS failed: {ex.Message}";
            }
            sw.Stop();
            results.Add(new SelfTestCheckResult
            {
                Name = "DNS Resolution",
                Passed = dnsPassed,
                LatencyMs = sw.ElapsedMilliseconds,
                Summary = dnsInfo
            });

            // 3. Parallel live HTTPS reachability tests (goes through live system stack -> ProxiFyre -> RyanDPI!)
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseProxy = false
            };
            using var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RyanDPI-HealthCheck/2.0");

            var endpoints = new (string Name, string Url)[]
            {
                ("Discord", "https://discord.com/api/v10/gateway"),
                ("Roblox Studio API", "https://clientsettings.roblox.com/v2/client-version/WindowsStudio64"),
                ("Roblox Web", "https://www.roblox.com/"),
                ("Wattpad", "https://www.wattpad.com/"),
                ("Direct Speed Baseline", "https://1.1.1.1/cdn-cgi/trace")
            };

            var endpointChecks = endpoints.Select(async ep =>
            {
                var epSw = Stopwatch.StartNew();
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, ep.Url);
                    using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    epSw.Stop();
                    var code = (int)resp.StatusCode;
                    bool ok = code >= 200 && code < 500;
                    return new SelfTestCheckResult
                    {
                        Name = ep.Name,
                        Passed = ok,
                        LatencyMs = epSw.ElapsedMilliseconds,
                        Summary = ok ? $"Unlocked (HTTP {code})" : $"Unexpected HTTP {code}"
                    };
                }
                catch (Exception ex)
                {
                    epSw.Stop();
                    var shortErr = ex.InnerException?.Message ?? ex.Message;
                    if (shortErr.Length > 60) shortErr = shortErr[..60] + "...";
                    return new SelfTestCheckResult
                    {
                        Name = ep.Name,
                        Passed = false,
                        LatencyMs = epSw.ElapsedMilliseconds,
                        Summary = $"Blocked / Timeout ({shortErr})"
                    };
                }
            });

            results.AddRange(await Task.WhenAll(endpointChecks));
            return results;
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

                    foreach (var procName in new[] { "ProxiFyre", "ciadpi" })
                    {
                        foreach (var p in Process.GetProcessesByName(procName))
                        {
                            try { p.Kill(); p.WaitForExit(2000); } catch { }
                        }
                    }

                    Directory.CreateDirectory(EngineByeDpiDir);
                    Directory.CreateDirectory(EngineProxiFyreDir);

                    // Copy ciadpi.exe
                    File.Copy(srcByeDpi, EngineCiadpiExe, overwrite: true);

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
            // For Windows services (tag != 0), ProxiFyre's owner_module_resolver resolves
            // BOTH base_name and full_path to the WINDOWS SERVICE NAME via I_QueryTagInformation
            // (e.g. "RyanDpiEngine", "dnscrypt-proxy", "ProxiFyreService") rather than "ciadpi.exe"!
            // Excluding both the exact Windows service names AND the executable names/paths
            // guarantees zero infinite routing loops in both service mode and interactive mode.
            var excludes = new List<string>
            {
                EngineServiceName,
                "ciadpi",
                "ciadpi.exe",
                EngineCiadpiExe,
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

            var protocols = settings.TunnelUdp
                ? new List<string> { "TCP", "UDP" }
                : new List<string> { "TCP" };

            var configObj = new ProxiFyreConfigDto
            {
                LogLevel = "Error",
                BypassLan = settings.BypassLan,
                Proxies = new List<ProxiFyreRuleDto>
                {
                    new()
                    {
                        // Empty string "" is ProxiFyre 2.x's global catch-all matcher:
                        // unlocks EVERY app (Roblox Studio, Roblox Player, Discord, browsers, CLI tools, etc.)
                        AppNames = new List<string> { "" },
                        Socks5ProxyEndpoint = $"127.0.0.1:{RyanDpiSettings.DefaultSocksPort}",
                        SupportedProtocols = protocols
                    }
                },
                Excludes = excludes
            };

            return JsonSerializer.Serialize(configObj, new JsonSerializerOptions { WriteIndented = true });
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

                var binPath = settings.BuildScBinPathValue(EngineCiadpiExe);
                LogService.Info($"Registering {EngineServiceName} service...");
                var createRes = await CreateServiceWithRetryAsync(
                    $"create {EngineServiceName} binPath= \"{binPath}\" start= auto type= own DisplayName= \"RyanDPI Engine\"");

                if (createRes.ExitCode != 0)
                {
                    LogService.Error($"Failed to create {EngineServiceName} service: {createRes.CombinedOutput}");
                    return false;
                }

                CommandRunner.Run("sc.exe", $"description {EngineServiceName} \"RyanDPI local full-speed DPI bypass engine.\"", 10000);
                CommandRunner.Run("sc.exe", $"failure {EngineServiceName} reset= 86400 actions= restart/3000/restart/5000/restart/10000", 10000);

                var startRes = CommandRunner.Run("sc.exe", $"start {EngineServiceName}", 15000);
                LogService.Info($"{EngineServiceName} start exit code: {startRes.ExitCode}");

                var svcRunning = await WaitForServiceRunningAsync(EngineServiceName, 5000);
                var portUp = svcRunning && await WaitForLocalSocksPortAsync(RyanDpiSettings.DefaultSocksPort, 5000);
                LogService.Info($"Engine service running={svcRunning}, local SOCKS5 127.0.0.1:{RyanDpiSettings.DefaultSocksPort} ready={portUp}");
                return portUp;
            });
        }

        private async Task<bool> InstallAndStartRouterServiceAsync()
        {
            return await Task.Run(async () =>
            {
                ServiceControlHelper.StopAndDeleteService(RouterServiceName);

                LogService.Info($"Registering {RouterServiceName} (depends on NDISRD + {EngineServiceName})...");
                var createRes = await CreateServiceWithRetryAsync(
                    $"create {RouterServiceName} binPath= \"\\\"{EngineProxiFyreExe}\\\"\" start= auto depend= {NdisDriverServiceName}/{EngineServiceName} DisplayName= \"RyanDPI Router (ProxiFyre)\"");

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

            [JsonPropertyName("supportedProtocols")]
            public List<string> SupportedProtocols { get; set; } = new();
        }
    }
}
