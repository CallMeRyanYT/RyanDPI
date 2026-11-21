using System.IO;
using System.Text.Json;

namespace RyanDPI.Models
{
    public enum DnsProtectionMode
    {
        EncryptedDnsProxy, // dnscrypt-proxy (127.0.0.1 / ::1) - works on Win 10 & 11
        WindowsNativeDoH,  // Windows 11 native DoH (Google 8.8.8.8 + Quad9 9.9.9.9)
        KeepSystemDns      // Do not touch adapter DNS settings
    }

    public enum BypassProfile
    {
        AutoUniversal, // 6-stage adaptive Turkey ISP chain (Recommended)
        ClassicByeDpi, // Classic lightweight ByeDPI strategy with full-speed buffers
        StrictIsp,     // Aggressive TLS record + OOB + fake TTL chain
        Custom         // User-defined custom ciadpi.exe parameters
    }

    public sealed class RyanDpiSettings
    {
        public const int DefaultSocksPort = 1080;

        // Zero-retransmit Stage 1 (--tlsrec 1+s) unlocks Turkish ISPs on the first packet with 0ms
        // added DPI latency (no TCP out-of-order/retransmit lag!), with 5 automatic fallback stages.
        public const string AutoUniversalStrategy =
            "--tlsrec 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --tlsrec 1+s --split 1 --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
            "--auto=torst,redirect,ssl_err --fake -1 --ttl 5 --disorder 3+s " +
            "--auto=torst,redirect,ssl_err";

        public const string ClassicByeDpiStrategy =
            "--tlsrec 1+s --mod-http=h,d --auto=torst --split 1 --disorder 3+s";

        public const string StrictIspStrategy =
            "--tlsrec 1+s --split 1 --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
            "--auto=torst,redirect,ssl_err --fake -1 --ttl 5 --disorder 3+s " +
            "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err";

        public bool IsDarkTheme { get; set; } = true;
        public string Language { get; set; } = "en";
        public bool AutoUpdateOnStartup { get; set; } = true;
        public DnsProtectionMode DnsMode { get; set; } = DnsProtectionMode.EncryptedDnsProxy;
        public BypassProfile Profile { get; set; } = BypassProfile.AutoUniversal;
        public string CustomStrategyArgs { get; set; } = AutoUniversalStrategy;
        public bool BypassLan { get; set; } = true;
        public bool TunnelUdp { get; set; } = false;
        public List<string> ExcludedApps { get; set; } = new();

        public static string SettingsDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RyanDPI");

        public static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

        public string BuildCiadpiArguments()
        {
            // High-throughput, lowest-latency loopback settings:
            // -i 127.0.0.1 -p 1080 : Loopback-only local SOCKS5 proxy
            // -c 4096              : Support 4,096 concurrent sockets across all apps
            // -b 131072            : 128 KB socket buffer for full gigabit line speed
            // -u 86400 -T 2 -L 3   : 24h per-IP strategy cache, fast 2s fallback trigger, adaptive sorting
            var baseFlags = $"-i 127.0.0.1 -p {DefaultSocksPort} -c 4096 -b 131072 -u 86400 -T 2 -L 3";
            if (TunnelUdp)
            {
                baseFlags += " -a 1";
            }

            var rawStrategy = Profile switch
            {
                BypassProfile.ClassicByeDpi => ClassicByeDpiStrategy,
                BypassProfile.StrictIsp => StrictIspStrategy,
                BypassProfile.Custom => string.IsNullOrWhiteSpace(CustomStrategyArgs)
                    ? AutoUniversalStrategy
                    : CustomStrategyArgs,
                _ => AutoUniversalStrategy
            };

            // Normalize whitespace/newlines so sc.exe binPath never breaks on multi-line pasted flags
            var sanitized = string.Join(" ",
                rawStrategy.Replace("\r", " ").Replace("\n", " ")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));

            return $"{baseFlags} {sanitized}";
        }

        /// <summary>
        /// Escapes arguments safely for embedding inside sc.exe binPath="...".
        /// </summary>
        public string BuildScBinPathValue(string executablePath)
        {
            var args = BuildCiadpiArguments().Replace("\"", "\\\"");
            return $"\\\"{executablePath}\\\" {args}";
        }

        public static RyanDpiSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var loaded = JsonSerializer.Deserialize<RyanDpiSettings>(json);
                    if (loaded != null)
                    {
                        loaded.ExcludedApps ??= new List<string>();
                        loaded.Language = Services.LocalizationService.NormalizeLanguage(loaded.Language);
                        if (string.IsNullOrWhiteSpace(loaded.CustomStrategyArgs))
                        {
                            loaded.CustomStrategyArgs = AutoUniversalStrategy;
                        }
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fall back to clean defaults if settings file is corrupted
            }

            return new RyanDpiSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Non-fatal
            }
        }
    }
}
