using System.IO;
using System.Text.Json;

namespace RyanDPI.Models
{
    public enum RegionPreset
    {
        GlobalUniversal, // Worldwide zero-lag DPI bypass (Turkey + Russia TSPU + all DPI blocks)
        Turkey,          // Turkey BTK zero-retransmit preset (Discord, Roblox, Roblox Studio, Wattpad)
        Russia,          // Russia TSPU-first preset (Discord + Voice, YouTube, Instagram, Roblox)
        Brazil           // Brazil ANPD Discord Screen Share / Video unlock (Discord-Only Split Tunnel, 0ms Game Lag)
    }

    public enum BrazilTunnelMode
    {
        BuiltInWarpMasque, // Free zero-login Cloudflare WARP over HTTP/3 MASQUE (usque on 127.0.0.1:1082)
        CustomSocks5       // User-provided non-Brazil SOCKS5 proxy (US/AR/EU — 100% guaranteed non-BR IP)
    }

    public enum DnsProtectionMode
    {
        EncryptedDnsProxy, // dnscrypt-proxy (127.0.0.1 / ::1) - works on Win 10 & 11
        WindowsNativeDoH,  // Windows 11 native DoH (Google 8.8.8.8 + Quad9 9.9.9.9)
        KeepSystemDns      // Do not touch adapter DNS settings
    }

    public enum BypassProfile
    {
        AutoUniversal, // 7-stage zero-retransmit Global adaptive chain (any unlisted country)
        TurkeyBtk,     // Ultra-lean 3-stage Turkey BTK chain (0ms Stage 1, lowest RAM/latency)
        RussiaTspu,    // Russia TSPU-first chain (YouTube, Discord, Instagram, Roblox on packet 1)
        ClassicByeDpi, // Classic lightweight ByeDPI strategy with full-speed buffers
        StrictIsp,     // Aggressive TLS record + OOB + fake SNI TTL chain
        Custom         // User-defined custom ciadpi.exe parameters
    }

    public sealed class RyanDpiSettings
    {
        public const int DefaultSocksPort = 1080;
        public const int UsqueSocksPort = 1082;

        // 7-stage Global Zero-Lag strategy (Windows & Android parity):
        // Stage 1 (--tlsrec 1+s) has 0ms added latency & unlocks Turkey / light DPI on packet 1.
        // Stages 2-6 automatically adapt across Russia TSPU (YouTube/googlevideo, Discord, Instagram),
        // Chrome/Edge TLS 1.3 Post-Quantum ML-KEM/Kyber ClientHello, Superonline, mobile networks,
        // and strict global DPI firewalls; Stage 7 falls back cleanly to passthrough.
        // Per-IP 24h caching (-u 86400 -L 3) locks onto the fastest working stage with zero extra lag!
        public const string AutoUniversalStrategy =
            "--tlsrec 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --disorder 1+s --tlsrec 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --disoob 1+s --tlsrec 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s --split 1+s " +
            "--auto=torst,redirect,ssl_err --fake -1 --ttl 8 --fake-sni www.wikipedia.org --fake-tls-mod=rand --disorder 1+s --tlsrec 1+s " +
            "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --tlsrec 3+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err";

        // Ultra-lean 3-stage Turkey BTK chain: 0ms Stage 1 (--tlsrec 1+s) + Superonline split/OOB fallbacks.
        // Skips Russia/Iran fake-SNI stages so slow sites never waste time on unnecessary retries.
        public const string TurkeyBtkStrategy =
            "--tlsrec 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --tlsrec 1+s --split 1 --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
            "--auto=torst,redirect,ssl_err";

        // Russia TSPU-first chain tuned for browser YouTube (youtube.com + googlevideo.com), Discord, Instagram & Roblox:
        // Uses +s SNI-anchored disorder/disoob/split/tlsrec (works even with Chrome's 1.8KB Post-Quantum ML-KEM/Kyber ClientHello!)
        // + randomized fake TLS with neutral non-Google SNI at TTL 8 & TTL 5, paired with automatic browser QUIC UDP/443 fallback!
        public const string RussiaTspuStrategy =
            "--disorder 1+s --tlsrec 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --disoob 1+s --tlsrec 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --split 1+s --tlsrec 1+s --oob 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --fake -1 --ttl 8 --fake-sni www.wikipedia.org --fake-tls-mod=rand --disorder 1+s --tlsrec 1+s " +
            "--auto=torst,redirect,ssl_err --fake -1 --ttl 5 --fake-sni www.wikipedia.org --fake-tls-mod=rand --split 1+s --disorder 3+s " +
            "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --tlsrec 3+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err";

        public const string ClassicByeDpiStrategy =
            "--tlsrec 1+s --mod-http=h,d --auto=torst --split 1 --disorder 3+s";

        public const string StrictIspStrategy =
            "--disorder 1+s --tlsrec 1+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err --disoob 1+s --tlsrec 1+s " +
            "--auto=torst,redirect,ssl_err --fake -1 --ttl 8 --fake-sni www.wikipedia.org --fake-tls-mod=rand --disorder 1+s --tlsrec 1+s " +
            "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
            "--auto=torst,redirect,ssl_err";

        public bool IsDarkTheme { get; set; } = true;
        public string Language { get; set; } = "en";
        public bool AutoUpdateOnStartup { get; set; } = true;

        // Global / Country preset settings
        public bool RegionConfigured { get; set; } = false;
        public RegionPreset Region { get; set; } = RegionPreset.GlobalUniversal;
        public BrazilTunnelMode BrazilTunnel { get; set; } = BrazilTunnelMode.BuiltInWarpMasque;
        public bool BrazilIncludeBrowsers { get; set; } = false;
        public bool BrazilIncludeUdp { get; set; } = true;
        public string CustomSocksEndpoint { get; set; } = string.Empty;
        public string CustomSocksUsername { get; set; } = string.Empty;
        public string CustomSocksPassword { get; set; } = string.Empty;

        public DnsProtectionMode DnsMode { get; set; } = DnsProtectionMode.EncryptedDnsProxy;
        public BypassProfile Profile { get; set; } = BypassProfile.AutoUniversal;
        public string CustomStrategyArgs { get; set; } = AutoUniversalStrategy;
        public bool BypassLan { get; set; } = true;
        public bool TunnelUdp { get; set; } = false;
        public List<string> ExcludedApps { get; set; } = new();

        public static string SettingsDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RyanDPI");

        public static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

        public bool UsesBuiltInUsqueService =>
            Region == RegionPreset.Brazil && BrazilTunnel == BrazilTunnelMode.BuiltInWarpMasque;

        public int ActiveLocalEnginePort =>
            UsesBuiltInUsqueService ? UsqueSocksPort : DefaultSocksPort;

        public void ApplyRegionPreset(RegionPreset region)
        {
            Region = region;
            RegionConfigured = true;

            switch (region)
            {
                case RegionPreset.Turkey:
                    Profile = BypassProfile.TurkeyBtk;
                    DnsMode = DnsProtectionMode.EncryptedDnsProxy;
                    TunnelUdp = false;
                    break;

                case RegionPreset.Russia:
                    Profile = BypassProfile.RussiaTspu;
                    DnsMode = DnsProtectionMode.EncryptedDnsProxy;
                    TunnelUdp = true;
                    break;

                case RegionPreset.Brazil:
                    Profile = BypassProfile.AutoUniversal;
                    DnsMode = DnsProtectionMode.KeepSystemDns;
                    BrazilIncludeUdp = true;
                    break;

                default: // GlobalUniversal
                    Profile = BypassProfile.AutoUniversal;
                    DnsMode = DnsProtectionMode.EncryptedDnsProxy;
                    TunnelUdp = false;
                    break;
            }
        }

        public string BuildCiadpiArguments()
        {
            // Low-RAM, lowest-latency loopback settings:
            // -i 127.0.0.1 -p 1080 : Loopback-only local SOCKS5 proxy
            // -c 2048              : Supports 2,048 concurrent sockets at half the RAM footprint
            // -b 65536             : 64 KB socket buffer (saturates 2.5 Gbps while keeping RSS tiny)
            // -u 86400 -T 2 -L 3   : 24h per-IP strategy cache, fast 2s fallback trigger, adaptive sorting
            var baseFlags = $"-i 127.0.0.1 -p {DefaultSocksPort} -c 2048 -b 65536 -u 86400 -T 2 -L 3";
            if (TunnelUdp)
            {
                baseFlags += " -a 3";
            }

            var rawStrategy = Profile switch
            {
                BypassProfile.TurkeyBtk => TurkeyBtkStrategy,
                BypassProfile.RussiaTspu => RussiaTspuStrategy,
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

        public static string BuildUsqueScBinPathValue(string hostExePath, string usqueExePath, string usqueConfigPath)
        {
            return $"\\\"{hostExePath}\\\" --service-usque \\\"{usqueExePath}\\\" \\\"{usqueConfigPath}\\\" {UsqueSocksPort}";
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
                        loaded.CustomSocksEndpoint ??= string.Empty;
                        loaded.CustomSocksUsername ??= string.Empty;
                        loaded.CustomSocksPassword ??= string.Empty;
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
