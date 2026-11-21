using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using RyanDPI.Models;

namespace RyanDPI.Services
{
    public sealed class DnsService
    {
        public const string DnsCryptServiceName = "dnscrypt-proxy";

        private readonly string _engineDnsCryptDir;

        public DnsService(string engineRootDir)
        {
            _engineDnsCryptDir = Path.Combine(engineRootDir, "dnscrypt");
        }

        public ComponentState GetDnsCryptState()
        {
            return ServiceControlHelper.QueryServiceState(DnsCryptServiceName);
        }

        public async Task<(bool Success, string Summary)> ApplyDnsModeAsync(DnsProtectionMode mode, string bundledSourceDir)
        {
            switch (mode)
            {
                case DnsProtectionMode.KeepSystemDns:
                    LogService.Info("DNS mode is set to 'Keep My Existing DNS'. Skipping DNS configuration.");
                    return (true, "Kept existing system DNS.");

                case DnsProtectionMode.WindowsNativeDoH:
                    LogService.Info("Applying Windows Native DoH (Google 8.8.8.8 + Quad9 9.9.9.9)...");
                    StopDnsCryptIfRunning();
                    var dohOk = await Task.Run(ApplyWindowsNativeDoH);
                    return dohOk
                        ? (true, "Windows Native DoH (Google + Quad9) configured.")
                        : (false, "Could not verify Windows Native DoH configuration.");

                case DnsProtectionMode.EncryptedDnsProxy:
                default:
                    LogService.Info("Configuring Encrypted DNS via DNSCrypt-Proxy (127.0.0.1 / ::1)...");
                    var proxyReady = await InstallAndVerifyDnsCryptAsync(bundledSourceDir);
                    if (proxyReady)
                    {
                        var adapterOk = await Task.Run(SetAdaptersToLocalDnsCrypt);
                        if (adapterOk)
                        {
                            LogService.Info("Encrypted DNS (DNSCrypt-Proxy) is active and verified.");
                            return (true, "Encrypted DNS (DNSCrypt-Proxy) active.");
                        }
                    }

                    // Safe fallback so the user is never left without DNS
                    LogService.Warn("DNSCrypt-Proxy could not start or respond on port 53. Falling back to Windows Native DoH...");
                    StopDnsCryptIfRunning();
                    var fallbackOk = await Task.Run(ApplyWindowsNativeDoH);
                    return fallbackOk
                        ? (true, "Fallback active: Windows Native DoH (Google + Quad9).")
                        : (false, "Both DNSCrypt-Proxy and Windows DoH failed; system DNS was left untouched.");
            }
        }

        public async Task<bool> ResetDnsToAutomaticAsync(bool removeDnsCryptService = true)
        {
            return await Task.Run(() =>
            {
                try
                {
                    LogService.Info("Resetting DNS and DoH settings to Automatic (DHCP)...");

                    if (removeDnsCryptService)
                    {
                        ServiceControlHelper.StopAndDeleteService(DnsCryptServiceName);
                    }
                    else
                    {
                        StopDnsCryptIfRunning();
                    }

                    // Reset union of ALL physical adapters and active non-virtual adapters (incl. USB tether / hotspot)
                    const string psScript = @"
$seen = @{}
$targets = @()

$phys = Get-NetAdapter -Physical -ErrorAction SilentlyContinue
foreach ($a in $phys) {
    if (-not $seen.ContainsKey($a.InterfaceIndex)) {
        $seen[$a.InterfaceIndex] = $true
        $targets += $a
    }
}

$active = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object {
    $_.Status -eq 'Up' -and
    $_.InterfaceDescription -notlike '*Virtual*' -and
    $_.InterfaceDescription -notlike '*Hyper-V*' -and
    $_.InterfaceDescription -notlike '*WSL*' -and
    $_.Name -notlike '*vEthernet*' -and
    $_.InterfaceDescription -notlike '*TAP*' -and
    $_.InterfaceDescription -notlike '*TUN*' -and
    $_.InterfaceDescription -notlike '*VPN*'
}
foreach ($a in $active) {
    if (-not $seen.ContainsKey($a.InterfaceIndex)) {
        $seen[$a.InterfaceIndex] = $true
        $targets += $a
    }
}

foreach ($adapter in $targets) {
    try {
        Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ResetServerAddresses -ErrorAction SilentlyContinue
        netsh interface ip set dns ""$($adapter.Name)"" dhcp | Out-Null
        netsh interface ipv6 set dns ""$($adapter.Name)"" dhcp | Out-Null
    } catch {}

    try {
        $dohPath = 'HKLM:\System\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\' + $adapter.InterfaceGuid + '\DohInterfaceSettings'
        if (Test-Path $dohPath) {
            Remove-Item -Path $dohPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    } catch {}
}

try {
    $globalDohPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient'
    if (Test-Path $globalDohPath) {
        Remove-ItemProperty -Path $globalDohPath -Name 'DohEnabled' -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $globalDohPath -Name 'DohServerAddress' -ErrorAction SilentlyContinue
    }
} catch {}

Clear-DnsClientCache -ErrorAction SilentlyContinue
ipconfig /flushdns | Out-Null
";

                    var result = CommandRunner.RunPowerShell(psScript, 45000);
                    LogService.Info($"DNS reset completed (exit code {result.ExitCode}).");
                    return result.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    LogService.Error($"DNS reset failed: {ex.Message}");
                    return false;
                }
            });
        }

        public void EnsureDnsCryptRunningIfInstalled()
        {
            var state = GetDnsCryptState();
            if (state == ComponentState.Stopped)
            {
                LogService.Info("Starting dnscrypt-proxy service...");
                CommandRunner.Run("sc.exe", $"start {DnsCryptServiceName}", 15000);
            }
        }

        private void StopDnsCryptIfRunning()
        {
            var state = GetDnsCryptState();
            if (state == ComponentState.Running)
            {
                LogService.Info("Stopping dnscrypt-proxy service...");
                ServiceControlHelper.StopService(DnsCryptServiceName);
            }
        }

        private async Task<bool> InstallAndVerifyDnsCryptAsync(string bundledSourceDir)
        {
            try
            {
                var targetExe = Path.Combine(_engineDnsCryptDir, "dnscrypt-proxy.exe");
                var targetToml = Path.Combine(_engineDnsCryptDir, "dnscrypt-proxy.toml");

                var currentState = GetDnsCryptState();
                var needsServiceRegistration = currentState == ComponentState.NotInstalled;

                if (!needsServiceRegistration)
                {
                    var qc = CommandRunner.Run("sc.exe", $"qc {DnsCryptServiceName}", 10000);
                    if (!qc.StdOut.Contains(_engineDnsCryptDir, StringComparison.OrdinalIgnoreCase))
                    {
                        LogService.Info("Existing dnscrypt-proxy service points to another folder. Migrating cleanly to RyanDPI engine...");
                        ServiceControlHelper.StopAndDeleteService(DnsCryptServiceName);
                        needsServiceRegistration = true;
                        currentState = ComponentState.NotInstalled;
                    }
                }

                var bundledToml = Path.Combine(bundledSourceDir, "dnscrypt-proxy.toml");
                bool configUpdated = File.Exists(bundledToml) && File.Exists(targetToml) &&
                    !File.ReadAllBytes(bundledToml).SequenceEqual(File.ReadAllBytes(targetToml));

                // Stage/refresh files whenever service is missing, required file is missing, or config was upgraded
                if (needsServiceRegistration || !File.Exists(targetExe) || !File.Exists(targetToml) || configUpdated)
                {
                    if (configUpdated)
                    {
                        LogService.Info("Detected updated low-latency dnscrypt-proxy.toml; refreshing DNS proxy configuration...");
                    }
                    if (currentState == ComponentState.Running)
                    {
                        ServiceControlHelper.StopService(DnsCryptServiceName);
                        await Task.Delay(300);
                    }

                    if (!Directory.Exists(bundledSourceDir))
                    {
                        LogService.Error($"Bundled dnscrypt-proxy directory missing: {bundledSourceDir}");
                        return false;
                    }

                    Directory.CreateDirectory(_engineDnsCryptDir);
                    foreach (var file in Directory.GetFiles(bundledSourceDir))
                    {
                        var dest = Path.Combine(_engineDnsCryptDir, Path.GetFileName(file));
                        File.Copy(file, dest, overwrite: true);
                    }

                    if (!File.Exists(targetExe))
                    {
                        LogService.Error("Failed to copy dnscrypt-proxy.exe to engine directory.");
                        return false;
                    }
                }

                // Only register the Windows service if it is not already registered
                if (needsServiceRegistration || GetDnsCryptState() == ComponentState.NotInstalled)
                {
                    var installRes = CommandRunner.Run(targetExe, "-service install", 20000, _engineDnsCryptDir);
                    LogService.Info($"dnscrypt-proxy service install exit code: {installRes.ExitCode}");

                    if (GetDnsCryptState() == ComponentState.NotInstalled)
                    {
                        LogService.Error($"dnscrypt-proxy service registration failed: {installRes.CombinedOutput}");
                        return false;
                    }

                    CommandRunner.Run("sc.exe", $"config {DnsCryptServiceName} start= auto", 10000);
                    CommandRunner.Run("sc.exe", $"failure {DnsCryptServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000", 10000);
                    CommandRunner.Run("sc.exe", $"description {DnsCryptServiceName} \"RyanDPI encrypted DNS resolver (DNSCrypt-Proxy)\"", 10000);
                }

                if (GetDnsCryptState() != ComponentState.Running)
                {
                    var startRes = CommandRunner.Run("sc.exe", $"start {DnsCryptServiceName}", 20000);
                    LogService.Info($"dnscrypt-proxy start exit code: {startRes.ExitCode}");
                }

                // Verify live DNS resolution on 127.0.0.1:53 before touching adapter DNS!
                for (int attempt = 1; attempt <= 8; attempt++)
                {
                    if (await ProbeLocalDnsAsync("discord.com", 2500))
                    {
                        LogService.Info($"DNSCrypt-Proxy live DNS verification passed (attempt {attempt}).");
                        return true;
                    }

                    LogService.Info($"Waiting for DNSCrypt-Proxy to warm up resolvers (attempt {attempt}/8)...");
                    await Task.Delay(1200);
                }

                LogService.Warn("DNSCrypt-Proxy did not respond on 127.0.0.1:53 within timeout.");
                return false;
            }
            catch (Exception ex)
            {
                LogService.Error($"DNSCrypt-Proxy setup error: {ex.Message}");
                return false;
            }
        }

        private static bool SetAdaptersToLocalDnsCrypt()
        {
            const string psScript = @"
$adapters = Get-NetAdapter | Where-Object {
    $_.Status -eq 'Up' -and
    $_.InterfaceDescription -notlike '*Virtual*' -and
    $_.InterfaceDescription -notlike '*Hyper-V*' -and
    $_.InterfaceDescription -notlike '*WSL*' -and
    $_.Name -notlike '*vEthernet*' -and
    $_.InterfaceDescription -notlike '*TAP*' -and
    $_.InterfaceDescription -notlike '*TUN*' -and
    $_.InterfaceDescription -notlike '*VPN*'
}
foreach ($adapter in $adapters) {
    try {
        Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses '127.0.0.1', '::1' -ErrorAction SilentlyContinue
        netsh interface ip set dns ""$($adapter.Name)"" static 127.0.0.1 | Out-Null
        netsh interface ipv6 set dns ""$($adapter.Name)"" static ::1 | Out-Null
    } catch {}

    try {
        $dohPath = 'HKLM:\System\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\' + $adapter.InterfaceGuid + '\DohInterfaceSettings'
        if (Test-Path $dohPath) {
            Remove-Item -Path $dohPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    } catch {}
}
Clear-DnsClientCache -ErrorAction SilentlyContinue
ipconfig /flushdns | Out-Null
";
            var res = CommandRunner.RunPowerShell(psScript, 45000);
            LogService.Info($"Configured active adapters to 127.0.0.1 / ::1 (exit code {res.ExitCode}).");
            return res.ExitCode == 0;
        }

        private static bool ApplyWindowsNativeDoH()
        {
            const string psScript = @"
$adapters = Get-NetAdapter | Where-Object {
    $_.Status -eq 'Up' -and
    $_.InterfaceDescription -notlike '*Virtual*' -and
    $_.InterfaceDescription -notlike '*Hyper-V*' -and
    $_.InterfaceDescription -notlike '*WSL*' -and
    $_.Name -notlike '*vEthernet*' -and
    $_.InterfaceDescription -notlike '*TAP*' -and
    $_.InterfaceDescription -notlike '*TUN*' -and
    $_.InterfaceDescription -notlike '*VPN*'
}

foreach ($adapter in $adapters) {
    $adapterName = $adapter.Name
    $adapterGuid = $adapter.InterfaceGuid

    try {
        Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses '8.8.8.8', '9.9.9.9', '2001:4860:4860::8888', '2620:fe::9' -ErrorAction SilentlyContinue
        netsh interface ip set dns ""$adapterName"" static 8.8.8.8 | Out-Null
        netsh interface ip add dns ""$adapterName"" 9.9.9.9 index=2 | Out-Null
        netsh interface ipv6 set dns ""$adapterName"" static 2001:4860:4860::8888 | Out-Null
        netsh interface ipv6 add dns ""$adapterName"" 2620:fe::9 index=2 | Out-Null
    } catch {}

    $dohPath = 'HKLM:\System\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\' + $adapterGuid + '\DohInterfaceSettings'
    try {
        if (Test-Path $dohPath) {
            Remove-Item -Path $dohPath -Recurse -Force -ErrorAction SilentlyContinue
        }

        $entries = @(
            @{ Sub = 'Doh\8.8.8.8'; Template = 'https://dns.google/dns-query' },
            @{ Sub = 'Doh\9.9.9.9'; Template = 'https://dns.quad9.net/dns-query' },
            @{ Sub = 'Doh6\2001:4860:4860::8888'; Template = 'https://dns.google/dns-query' },
            @{ Sub = 'Doh6\2620:fe::9'; Template = 'https://dns.quad9.net/dns-query' }
        )

        foreach ($entry in $entries) {
            $p = Join-Path $dohPath $entry.Sub
            New-Item -Path $p -Force | Out-Null
            New-ItemProperty -Path $p -Name 'DohFlags' -Value 1 -PropertyType Qword -Force | Out-Null
            New-ItemProperty -Path $p -Name 'DohTemplate' -Value $entry.Template -PropertyType String -Force | Out-Null
        }
    } catch {}
}

Clear-DnsClientCache -ErrorAction SilentlyContinue
ipconfig /flushdns | Out-Null
";
            var res = CommandRunner.RunPowerShell(psScript, 45000);
            LogService.Info($"Configured Windows Native DoH on active adapters (exit code {res.ExitCode}).");
            return res.ExitCode == 0;
        }

        /// <summary>
        /// Sends a minimal standard DNS A-record query directly to 127.0.0.1:53 via UDP
        /// to verify DNSCrypt-Proxy is alive and returning real non-error DNS answers.
        /// </summary>
        public static async Task<bool> ProbeLocalDnsAsync(string hostname, int timeoutMs = 2500)
        {
            try
            {
                var query = BuildDnsAQuery(hostname);
                using var udp = new UdpClient();
                udp.Client.ReceiveTimeout = timeoutMs;
                var endpoint = new IPEndPoint(IPAddress.Loopback, 53);

                await udp.SendAsync(query, query.Length, endpoint);

                using var cts = new CancellationTokenSource(timeoutMs);
                var response = await udp.ReceiveAsync(cts.Token);
                var data = response.Buffer;

                // Valid DNS response: >= 12 bytes, matching txid, QR=1, RCODE=0, ANCOUNT >= 1
                if (data.Length < 12) return false;
                if (data[0] != query[0] || data[1] != query[1]) return false;
                bool isResponse = (data[2] & 0x80) != 0;
                int rcode = data[3] & 0x0F;
                int anCount = (data[6] << 8) | data[7];
                return isResponse && rcode == 0 && anCount >= 1;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] BuildDnsAQuery(string hostname)
        {
            using var ms = new MemoryStream();
            // Transaction ID (0x52, 0x44 = "RD")
            ms.WriteByte(0x52);
            ms.WriteByte(0x44);
            // Flags: standard query with recursion desired (0x0100)
            ms.WriteByte(0x01);
            ms.WriteByte(0x00);
            // QDCOUNT = 1, ANCOUNT = 0, NSCOUNT = 0, ARCOUNT = 0
            ms.Write(new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

            foreach (var label in hostname.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                var bytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes);
            }
            ms.WriteByte(0x00); // Root terminator
            // QTYPE = A (1), QCLASS = IN (1)
            ms.Write(new byte[] { 0x00, 0x01, 0x00, 0x01 });
            return ms.ToArray();
        }
    }

    internal static class ServiceControlHelper
    {
        // Strictly parse ONLY the STATE line so TYPE/EXIT_CODE/CHECKPOINT digits can never false-match!
        private static readonly Regex StateLineRegex =
            new(@"(?m)^\s*STATE\s*:\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ComponentState QueryServiceState(string serviceName)
        {
            var res = CommandRunner.Run("sc.exe", $"query \"{serviceName}\"", 8000);
            if (res.ExitCode == 1060 || res.CombinedOutput.Contains("1060"))
            {
                return ComponentState.NotInstalled;
            }

            if (res.ExitCode != 0 && string.IsNullOrWhiteSpace(res.StdOut))
            {
                return ComponentState.NotInstalled;
            }

            var match = StateLineRegex.Match(res.StdOut);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int stateCode))
            {
                // Win32 SERVICE_STATUS dwCurrentState:
                // 4 = SERVICE_RUNNING, 1 = STOPPED, 2 = START_PENDING, 3 = STOP_PENDING, etc.
                return stateCode == 4 ? ComponentState.Running : ComponentState.Stopped;
            }

            // Fallback text check for unusual localized outputs without "STATE" label
            if (res.StdOut.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
            {
                return ComponentState.Running;
            }

            return ComponentState.NotInstalled;
        }

        public static void StopService(string serviceName, int waitMs = 6000)
        {
            var state = QueryServiceState(serviceName);
            if (state == ComponentState.NotInstalled || state == ComponentState.Stopped) return;

            CommandRunner.Run("sc.exe", $"stop \"{serviceName}\"", 15000);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < waitMs)
            {
                if (QueryServiceState(serviceName) != ComponentState.Running) break;
                Thread.Sleep(250);
            }
        }

        public static void StopAndDeleteService(string serviceName, int waitDeleteMs = 6000)
        {
            var state = QueryServiceState(serviceName);
            if (state == ComponentState.NotInstalled) return;

            StopService(serviceName);
            CommandRunner.Run("sc.exe", $"delete \"{serviceName}\"", 15000);

            // Wait for SCM to finalize deletion so an immediate sc create never hits ERROR 1072
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < waitDeleteMs)
            {
                if (QueryServiceState(serviceName) == ComponentState.NotInstalled) break;
                Thread.Sleep(200);
            }
        }
    }
}
