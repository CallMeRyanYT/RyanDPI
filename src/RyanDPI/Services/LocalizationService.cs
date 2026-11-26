namespace RyanDPI.Services
{
    public static class LocalizationService
    {
        public const string English = "en";
        public const string Turkish = "tr";
        public const string Portuguese = "pt";
        public const string Russian = "ru";
        public const string Persian = "fa";
        public const string Indonesian = "id";
        public const string Chinese = "zh";
        public const string Arabic = "ar";

        public sealed record LanguageOption(string Code, string ShortLabel, string NativeName);

        public static readonly LanguageOption[] AllLanguages =
        {
            new(English, "EN", "English"),
            new(Turkish, "TR", "Türkçe (Türkiye)"),
            new(Portuguese, "PT", "Português (Brasil)"),
            new(Russian, "RU", "Русский (Россия)"),
            new(Persian, "FA", "فارسی (ایران)"),
            new(Indonesian, "ID", "Bahasa Indonesia"),
            new(Chinese, "ZH", "简体中文 (中国)"),
            new(Arabic, "AR", "العربية (مصر / الشرق الأوسط)")
        };

        public static readonly string[] SupportedLanguages =
            { English, Turkish, Portuguese, Russian, Persian, Indonesian, Chinese, Arabic };

        private static string _currentLanguage = English;

        public static string CurrentLanguage => _currentLanguage;
        public static bool IsTurkish => _currentLanguage == Turkish;
        public static bool IsPortuguese => _currentLanguage == Portuguese;
        public static bool IsRussian => _currentLanguage == Russian;
        public static bool IsPersian => _currentLanguage == Persian;
        public static bool IsIndonesian => _currentLanguage == Indonesian;
        public static bool IsChinese => _currentLanguage == Chinese;
        public static bool IsArabic => _currentLanguage == Arabic;

        public static event Action? LanguageChanged;

        public static void SetLanguage(string? languageCode)
        {
            var normalized = NormalizeLanguage(languageCode);
            if (_currentLanguage == normalized) return;

            _currentLanguage = normalized;
            LanguageChanged?.Invoke();
        }

        public static string GetNextLanguage(string? current)
        {
            var norm = NormalizeLanguage(current);
            int idx = Array.IndexOf(SupportedLanguages, norm);
            return SupportedLanguages[(idx + 1) % SupportedLanguages.Length];
        }

        public static string NormalizeLanguage(string? languageCode)
        {
            var raw = languageCode?.Trim().ToLowerInvariant() ?? string.Empty;
            if (raw.StartsWith("tr")) return Turkish;
            if (raw.StartsWith("pt")) return Portuguese;
            if (raw.StartsWith("ru")) return Russian;
            if (raw.StartsWith("fa") || raw.StartsWith("pr")) return Persian;
            if (raw.StartsWith("id") || raw.StartsWith("in")) return Indonesian;
            if (raw.StartsWith("zh") || raw.StartsWith("cn")) return Chinese;
            if (raw.StartsWith("ar")) return Arabic;
            return English;
        }

        public static string T(string key)
        {
            var dict = _currentLanguage switch
            {
                Turkish => Tr,
                Portuguese => Pt,
                Russian => Ru,
                Persian => Fa,
                Indonesian => Id,
                Chinese => Zh,
                Arabic => Ar,
                _ => En
            };
            if (dict.TryGetValue(key, out var value)) return value;
            if (En.TryGetValue(key, out var fallback)) return fallback;
            return key;
        }

        public static string Tf(string key, params object[] args)
        {
            var template = T(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        public static IReadOnlyDictionary<string, string> GetEnglishTable() => En;
        public static IReadOnlyDictionary<string, string> GetTurkishTable() => Tr;
        public static IReadOnlyDictionary<string, string> GetPortugueseTable() => Pt;
        public static IReadOnlyDictionary<string, string> GetRussianTable() => Ru;
        public static IReadOnlyDictionary<string, string> GetPersianTable() => Fa;
        public static IReadOnlyDictionary<string, string> GetIndonesianTable() => Id;
        public static IReadOnlyDictionary<string, string> GetChineseTable() => Zh;
        public static IReadOnlyDictionary<string, string> GetArabicTable() => Ar;

        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetAllLanguageTables() =>
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [English] = En,
                [Turkish] = Tr,
                [Portuguese] = Pt,
                [Russian] = Ru,
                [Persian] = Fa,
                [Indonesian] = Id,
                [Chinese] = Zh,
                [Arabic] = Ar
            };

        private static readonly Dictionary<string, string> En = new()
        {
            // Title bar & Nav
            ["Badge_AllAppsSpeed"] = "GLOBAL • ZERO LAG",
            ["LangButton_Text"] = "EN",
            ["LangButton_Tooltip"] = "Choose language (EN / TR / PT-BR / RU / FA / ID / ZH / AR)",
            ["RegionButton_Tooltip"] = "Select Country / Bypass Mode (Global, Turkey, Russia, Brazil)",
            ["ThemeButton_Tooltip"] = "Toggle Dark / Light Theme",
            ["Minimize_Tooltip"] = "Minimize",
            ["Close_Tooltip"] = "Close",
            ["Refresh_Tooltip"] = "Refresh Status",
            ["Nav_Home"] = "Home",
            ["Nav_Settings"] = "Settings",
            ["Nav_Tools"] = "Tools & Logs",

            // First-Run / Region Picker
            ["Region_PickerTitle"] = "Choose Your Region Mode",
            ["Region_PickerSubtitle"] = "Pick your country if listed for the fastest, lowest-lag routing — or use Universal Global for any other country.",
            ["Region_GlobalTag"] = "GLOBAL",
            ["Region_TurkeyTag"] = "TURKEY",
            ["Region_RussiaTag"] = "RUSSIA",
            ["Region_BrazilTag"] = "BRAZIL",
            ["Region_GlobalTitle"] = "Universal Global (Other Countries)",
            ["Region_GlobalDesc"] = "7-stage zero-lag DPI engine for unlisted countries (Iran, Indonesia, Egypt, etc.) with 24h per-IP caching.",
            ["Region_TurkeyTitle"] = "Turkey — BTK Ultra-Lean (Lowest Lag)",
            ["Region_TurkeyDesc"] = "Ultra-lean 3-stage BTK engine for Discord, Roblox, Roblox Studio & Wattpad. Direct UDP for 0ms gaming ping.",
            ["Region_RussiaTitle"] = "Russia — TSPU Full Unblock (Lowest Lag)",
            ["Region_RussiaDesc"] = "Instant packet-1 TSPU split/disorder/fake-SNI + UDP voice for YouTube, Discord, Instagram & Roblox.",
            ["Region_BrazilTitle"] = "Brazil — Discord Screen Share & Video",
            ["Region_BrazilDesc"] = "Required in Brazil (ANPD Geo-IP): tunnels ONLY Discord (0ms lag in games!) via WARP MASQUE or Custom SOCKS5.",
            ["Region_PickerClose"] = "Continue",

            // Health Strip
            ["Pill_EngineTitle"] = "ENGINE",
            ["Pill_RouterTitle"] = "APP ROUTER",
            ["Pill_DnsTitle"] = "SECURE DNS",
            ["State_Checking"] = "Checking",
            ["State_Running"] = "Running",
            ["State_Stopped"] = "Stopped",
            ["State_NotSet"] = "Not Set",
            ["State_AllApps"] = "All Apps",
            ["State_DiscordOnly"] = "Discord Only",
            ["State_Paused"] = "Paused",
            ["State_Encrypted"] = "Encrypted",
            ["State_NativeDoH"] = "Native DoH",
            ["State_Custom"] = "Custom",
            ["State_Default"] = "Direct",

            // Hero Status
            ["Status_CheckingHeadline"] = "Checking status...",
            ["Status_CheckingSubtitle"] = "Scanning Windows services and network settings...",
            ["Status_ActiveHeadline"] = "RyanDPI is Active",
            ["Status_ActiveSubtitle"] = "Every blocked app & website is unlocked automatically at full native internet speed.",
            ["Status_ActiveSubtitleBrazil"] = "Brazil Discord Split-Tunnel active — ONLY Discord is tunneled; all games stay at 0ms native ping!",
            ["Status_ActiveCta"] = "Pause RyanDPI",
            ["Status_ActiveCaption"] = "Running in background as a Windows service — stays active automatically after every restart.",
            ["Status_PartialHeadline"] = "RyanDPI Partially Active",
            ["Status_PartialSubtitle"] = "One of the background services is stopped. Click below to start both cleanly.",
            ["Status_PausedHeadline"] = "RyanDPI is Paused",
            ["Status_PausedSubtitle"] = "RyanDPI is installed and ready. Click below to re-enable instant access.",
            ["Status_PausedCta"] = "Enable RyanDPI",
            ["Status_PausedCaption"] = "Enables your selected region bypass mode with zero gaming slowdown.",
            ["Status_ReadyHeadline"] = "Ready to Unlock Everything",
            ["Status_ReadySubtitle"] = "One-click setup unlocks Discord, Roblox, YouTube & blocked apps/sites with zero gaming lag.",
            ["Status_ReadySubtitleBrazil"] = "One-click Brazil Discord Split-Tunnel unlocks Screen Share & Go Live while keeping games at 0ms ping.",
            ["Status_ReadyLegacySubtitle"] = "Legacy DPI detected ({0}). RyanDPI will replace it cleanly.",
            ["Status_ReadyCta"] = "Install & Enable RyanDPI",
            ["Status_ReadyCaption"] = "Zero gaming ping penalty — automatic region-optimized local DPI bypass & Discord split-tunneling.",
            ["Btn_Reinstall"] = "Reinstall / Repair",
            ["Btn_Uninstall"] = "Uninstall",
            ["Btn_RestartPc"] = "Restart PC",

            // Self-Test Card
            ["SelfTest_Title"] = "Unblock & Speed Self-Test",
            ["SelfTest_Subtitle"] = "Verify unlocked services, exit status & live ping right now.",
            ["SelfTest_Run"] = "Run Test",
            ["SelfTest_Testing"] = "Testing...",
            ["SelfTest_Placeholder"] = "Click 'Run Test' to verify live access and response latency.",
            ["SelfTest_Failed"] = "Failed",
            ["SelfTest_LocalEngine"] = "RyanDPI Local Engine",
            ["SelfTest_LocalEngineActive"] = "Active on 127.0.0.1:{0} (All Apps)",
            ["SelfTest_BrazilEngineActive"] = "Discord Split-Tunnel active ({0} — Games 100% Direct)",
            ["SelfTest_LocalEngineLegacy"] = "Legacy proxy on :1080 — click Install RyanDPI to upgrade",
            ["SelfTest_LocalEngineStopped"] = "RyanDPI Engine is not running",
            ["SelfTest_LocalRouterStopped"] = "RyanDPI Router (ProxiFyreService) is not running",
            ["SelfTest_LocalPortUnreachable"] = "Port {0} unreachable",
            ["SelfTest_Dns"] = "DNS Resolution",
            ["SelfTest_DnsClean"] = "Clean Encrypted DNS (Blocked domains resolved)",
            ["SelfTest_DnsEmpty"] = "DNS lookup returned no addresses",
            ["SelfTest_DnsFailed"] = "DNS failed: {0}",
            ["SelfTest_Discord"] = "Discord API",
            ["SelfTest_DiscordMedia"] = "Discord Media CDN",
            ["SelfTest_YouTube"] = "YouTube",
            ["SelfTest_Instagram"] = "Instagram",
            ["SelfTest_RobloxApp"] = "Roblox App (Player)",
            ["SelfTest_RobloxStudio"] = "Roblox Studio API",
            ["SelfTest_RobloxWeb"] = "Roblox Web",
            ["SelfTest_Wattpad"] = "Wattpad",
            ["SelfTest_DirectBaseline"] = "Direct Speed Baseline",
            ["SelfTest_DirectGameBaseline"] = "Direct Game Baseline (0ms Lag)",
            ["SelfTest_BrazilExitCheck"] = "Discord Tunnel Exit IP",
            ["SelfTest_BrazilExitNonBr"] = "Non-BR Exit: {0} ({1}) — Screen Share Ready!",
            ["SelfTest_BrazilExitStillBr"] = "Exit in BR ({0}) — If Go Live stays hidden, set a Non-BR SOCKS5 in Settings",
            ["SelfTest_UnlockedFmt"] = "Unlocked (HTTP {0} • Cold {1} ms)",
            ["SelfTest_UnexpectedHttp"] = "Unexpected HTTP {0}",
            ["SelfTest_BlockedFmt"] = "Blocked / Timeout ({0})",

            // Settings Page
            ["Settings_RegionTitle"] = "Country / Region Mode",
            ["Settings_BrazilCardTitle"] = "Brazil Discord Split-Tunnel (0ms Game Lag)",
            ["Settings_BrazilNotice"] = "Discord disabled Screen Share & Video in Brazil by Geo-IP (ANPD order), not ISP DPI. RyanDPI routes ONLY Discord while leaving Valorant, CS2, Roblox & all games 100% direct!",
            ["Brazil_TunnelWarp"] = "Built-in Free Cloudflare WARP MASQUE (Zero Login)",
            ["Brazil_TunnelCustom"] = "Custom Non-Brazil SOCKS5 Proxy (US / AR / EU — Guaranteed Non-BR IP)",
            ["Brazil_CustomEndpointLabel"] = "Non-Brazil SOCKS5 Endpoint (host:port):",
            ["Brazil_CustomUserLabel"] = "Username (optional):",
            ["Brazil_CustomPassLabel"] = "Password (optional):",
            ["Brazil_UdpTitle"] = "Route Discord UDP (Go Live Video & Voice)",
            ["Brazil_UdpDesc"] = "ON routes full Discord TCP+UDP. Turn OFF for API-only unlock (direct local UDP voice/video).",
            ["Brazil_BrowsersTitle"] = "Also Route Web Browsers (Discord Web)",
            ["Brazil_BrowsersDesc"] = "Include Chrome, Edge, Firefox, Brave & Opera if you screen share in browser.",

            ["Settings_ProfileTitle"] = "DPI Bypass Profile",
            ["Profile_AutoUniversal"] = "Auto Universal (7-Stage — Other Countries)",
            ["Profile_TurkeyBtk"] = "Turkey BTK (Ultra-Lean 3-Stage — Lowest Lag)",
            ["Profile_RussiaTspu"] = "Russia TSPU (YouTube, Discord, Instagram — First Packet)",
            ["Profile_Classic"] = "Classic Mode (Lightweight TLS Record Split)",
            ["Profile_StrictIsp"] = "Strict ISP (Superonline / Mobile / Aggressive DPI)",
            ["Profile_Custom"] = "Custom Arguments",
            ["Profile_Desc_Auto"] = "7-stage global strategy for unlisted countries: Stage 1 (--tlsrec 1+s) gives 0ms lag on light DPI, auto-escalating across strict worldwide ISPs with 24h per-IP caching.",
            ["Profile_Desc_Turkey"] = "Ultra-lean 3-stage BTK strategy: 0ms Stage 1 (--tlsrec 1+s) + Superonline split/OOB fallbacks with zero wasted Russia/Iran retries and half the RAM overhead.",
            ["Profile_Desc_Russia"] = "Starts immediately with TSPU-proven split + disorder + TLS record desync, escalating to OOB and fake-SNI TTL packets for Rostelecom, MTS, Beeline & YouTube/Discord.",
            ["Profile_Desc_Classic"] = "Lightweight zero-retransmit TLS record split (--tlsrec 1+s --mod-http=h,d) with automatic split/disorder fallback and low-RAM full-speed buffers.",
            ["Profile_Desc_Strict"] = "Starts immediately with TLS record + TCP split, then escalates to OOB, disorder, and fake-SNI low-TTL packets for aggressive ISPs.",
            ["Profile_Desc_Custom"] = "Enter custom ciadpi.exe desync flags below. Low-RAM full-speed socket flags (-i 127.0.0.1 -p 1080 -c 2048 -b 65536) are prepended automatically.",
            ["Settings_CustomArgsLabel"] = "Custom ciadpi.exe strategy arguments:",

            ["Settings_DnsTitle"] = "DNS Protection",
            ["Dns_Encrypted"] = "Encrypted DNS — DNSCrypt-Proxy (Recommended for TR / RU)",
            ["Dns_NativeDoH"] = "Windows Native DoH (Google + Quad9)",
            ["Dns_KeepSystem"] = "Keep My Existing DNS Settings (Recommended for Brazil)",
            ["Dns_Desc_Encrypted"] = "Runs local encrypted DNS (DNSCrypt-Proxy) via Cloudflare, Google & Quad9 (fastest-first) with an 8,192-entry cache. Defeats ISP DNS poisoning on Windows 10 & 11.",
            ["Dns_Desc_NativeDoH"] = "Uses Windows 11 built-in DNS-over-HTTPS with Google (8.8.8.8) and Quad9 (9.9.9.9) without running a local DNS proxy.",
            ["Dns_Desc_KeepSystem"] = "Leaves your network adapter DNS untouched — ideal for Brazil or if you already manage your own encrypted DNS.",

            ["Settings_NetworkTitle"] = "Network & Speed Options",
            ["Settings_BypassLanTitle"] = "Bypass LAN & Localhost",
            ["Settings_BypassLanDesc"] = "Keep local devices, routers, and localhost dev servers completely direct.",
            ["Settings_TunnelUdpTitle"] = "Also Tunnel UDP (Voice / QUIC)",
            ["Settings_TunnelUdpDesc"] = "OFF for 0ms gaming ping in Turkey; ON in Russia if your ISP blocks Discord voice UDP or QUIC.",
            ["Settings_AutoUpdateTitle"] = "Auto-Update on Startup",
            ["Settings_AutoUpdateDesc"] = "Automatically download, verify, and install new RyanDPI releases when the app opens.",

            ["Settings_ExcludesTitle"] = "Excluded Apps (Optional)",
            ["Settings_ExcludesDesc"] = "In Global/Turkey/Russia mode, all apps are unlocked by default. Add apps here only if you want them to skip RyanDPI.",
            ["Settings_ExcludeTooltip"] = "Enter an executable name (e.g. game.exe)",
            ["Btn_Add"] = "Add",
            ["Btn_Browse"] = "Browse...",
            ["Settings_NoExcludes"] = "No custom app exclusions configured.",
            ["Btn_SaveApply"] = "Save & Apply Settings",
            ["Btn_Applying"] = "Applying...",
            ["BrowseExclude_Title"] = "Select an application to exclude from RyanDPI",
            ["BrowseExclude_Filter"] = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",

            // Tools & Logs Page
            ["Tools_Title"] = "System Tools",
            ["Btn_ResetDns"] = "Reset DNS & Flush Cache",
            ["Btn_CleanLegacy"] = "Clean Legacy DPI Services",
            ["Btn_CleanLegacyCount"] = "Clean Legacy DPI ({0})",
            ["Btn_OpenLogs"] = "Open Logs Folder",
            ["Btn_UninstallFull"] = "Uninstall RyanDPI",
            ["Logs_Title"] = "Live Activity Log",
            ["Btn_Copy"] = "Copy",
            ["Btn_Clear"] = "Clear",
            ["About_TitleFmt"] = "RyanDPI v{0} Unbound — Global Zero-Lag DPI Bypass & Split-Tunnel",
            ["About_Subtitle"] = "Turkey • Russia • Brazil • Worldwide. Built with ByeDPI, ProxiFyre, DNSCrypt-Proxy & Usque WARP MASQUE.",
            ["Btn_CheckUpdates"] = "Check Updates",
            ["Btn_Checking"] = "Checking...",
            ["Btn_Updating"] = "Updating...",

            // Progress & Messages
            ["Progress_InstallingTitle"] = "Installing RyanDPI...",
            ["Progress_Starting"] = "Starting...",
            ["Progress_Initializing"] = "Initializing installation...",
            ["Progress_StepFmt"] = "Step {0} / {1}",
            ["Step1_Title"] = "Preparing system",
            ["Step1_Detail"] = "Stopping and removing any conflicting legacy DPI or VPN services...",
            ["Step2_Title"] = "Checking prerequisites",
            ["Step2_Detail"] = "Verifying Windows Packet Filter driver (NDISRD) and VC++ Runtime...",
            ["Step3_Title"] = "Deploying RyanDPI engine",
            ["Step3_Detail"] = "Installing region-optimized router, engine binaries, and ACL security...",
            ["Step4_Title"] = "Configuring DNS & Firewall",
            ["Step4_Detail"] = "Applying DNS protection and Windows Firewall allow rules...",
            ["Step5_Title"] = "Starting RyanDPI",
            ["Step5_Detail"] = "Launching local engine and transparent application router...",
            ["Progress_UninstallingTitle"] = "Uninstalling RyanDPI",
            ["Progress_CleaningUp"] = "Cleaning up...",
            ["Progress_UninstallingDetail"] = "Stopping services, removing firewall rules, and restoring automatic DNS...",

            ["Msg_NdisFailed"] = "Failed to install the Windows Packet Filter driver (NDISRD). Check the Logs tab for details.",
            ["Msg_StageFailed"] = "Failed to deploy RyanDPI engine files. Make sure no antivirus quarantined the files.",
            ["Msg_EngineStartFailed"] = "RyanDPI engine service could not start cleanly. Check the Logs tab for details.",
            ["Msg_RebootNeeded"] = "RyanDPI is installed! A quick PC restart is needed so Windows can finish loading the new Packet Filter driver.",
            ["Msg_RouterStartFailed"] = "RyanDPI router service (ProxiFyreService) failed to start. Try restarting your PC or check the Logs tab.",
            ["Msg_InstallSuccess"] = "RyanDPI is active! ({0})",
            ["Msg_InstallError"] = "Installation error: {0}",
            ["Msg_Started"] = "RyanDPI is now active.",
            ["Msg_StartEngineFail"] = "RyanDPI engine failed to start. Check Logs for details.",
            ["Msg_StartRouterFail"] = "RyanDPI Router (ProxiFyreService) could not start. Check Logs for details.",
            ["Msg_Paused"] = "RyanDPI paused.",
            ["Msg_SettingsSavedNotInstalled"] = "Settings saved! Click 'Install & Enable RyanDPI' on Home to activate them.",
            ["Msg_SettingsApplyFailed"] = "Failed to apply settings: {0}",
            ["Msg_CustomSocksInvalid"] = "Please enter a valid SOCKS5 endpoint in host:port format (e.g. 1.2.3.4:1080).",
            ["Msg_LegacyRemoved"] = "Cleaned up {0} legacy DPI/tunneling service(s).",
            ["Msg_LegacyNone"] = "No legacy conflicting services were found — your system is clean.",
            ["Msg_UninstallSuccess"] = "RyanDPI, its background services, firewall rules, and DNS changes have been completely removed.",
            ["Msg_UninstallError"] = "Uninstall error: {0}",
            ["Msg_DnsResetOk"] = "DNS & DoH reset to Automatic (DHCP) and DNS cache flushed.",
            ["Msg_DnsResetWarn"] = "DNS reset finished with warnings. Check logs.",
            ["DnsSummary_Kept"] = "Kept existing system DNS.",
            ["DnsSummary_NativeDoH"] = "Windows Native DoH (Google + Quad9) configured.",
            ["DnsSummary_NativeDoHFail"] = "Could not verify Windows Native DoH configuration.",
            ["DnsSummary_EncryptedOk"] = "Encrypted DNS (DNSCrypt-Proxy) active.",
            ["DnsSummary_FallbackDoH"] = "Fallback active: Windows Native DoH (Google + Quad9).",
            ["DnsSummary_AllFail"] = "Both DNSCrypt-Proxy and Windows DoH failed; system DNS was left untouched.",

            // Auto-Updater
            ["Update_FinishingTitle"] = "Finishing update to {0}...",
            ["Update_SyncingStep"] = "Syncing engine",
            ["Update_SyncingDetail"] = "Updating background RyanDPI engine binaries, config, and firewall rules...",
            ["Update_UpdatedRefreshed"] = "RyanDPI automatically updated to {0} and refreshed background services!",
            ["Update_UpdatedWarn"] = "Updated to {0}: {1}",
            ["Update_UpdatedSimple"] = "RyanDPI automatically updated to {0}!",
            ["Update_CheckFailed"] = "Could not check for updates: {0}",
            ["Update_UpToDate"] = "You are on the latest version (RyanDPI v{0}).",
            ["Update_UpdatingTitle"] = "Updating RyanDPI to v{0}...",
            ["Update_StartingStep"] = "Starting",
            ["Update_StartingDetail"] = "Found new release v{0}. Downloading update automatically...",
            ["Update_DownloadingStep"] = "Downloading",
            ["Update_DownloadingFile"] = "Downloading {0} from GitHub Releases...",
            ["Update_DownloadingPct"] = "Downloading {0} ({1:F1}{2})...",
            ["Update_VerifyingStep"] = "Verifying",
            ["Update_VerifyingDetail"] = "Verifying download integrity (size, format & SHA-256)...",
            ["Update_InstallingTitle"] = "Installing RyanDPI v{0}...",
            ["Update_RestartingStep"] = "Restarting",
            ["Update_RestartingDetail"] = "Launching silent update and restarting RyanDPI...",
            ["Update_Failed"] = "Automatic update to v{0} failed: {1}",
            ["Update_LaunchFailed"] = "Downloaded v{0}, but could not launch the installer automatically.",
            ["Update_BgRefreshFail"] = "App updated, but refreshing background engine files failed. Click 'Reinstall / Repair'.",
            ["Update_BgRefreshOk"] = "Background services upgraded cleanly.",
            ["Update_BgRefreshRestartFail"] = "App updated, but restarting background services failed. Click 'Reinstall / Repair'.",
            ["Update_BgRefreshPaused"] = "Background engine updated (currently paused).",

            // Confirmation & Startup Dialogs
            ["Confirm_UninstallTitle"] = "Uninstall RyanDPI",
            ["Confirm_UninstallBody"] = "Completely remove RyanDPI services, firewall rules, and reset DNS back to automatic (DHCP)?",
            ["Confirm_RebootTitle"] = "Restart PC",
            ["Confirm_RebootBody"] = "Restart your computer now? Make sure your work is saved.",
            ["Confirm_ResetDnsTitle"] = "Reset DNS & Flush Cache",
            ["Confirm_ResetDnsBody"] = "Reset DNS and DoH settings on all physical network adapters back to Automatic (DHCP) and flush the DNS cache?",
            ["Confirm_CleanLegacyTitle"] = "Clean Legacy DPI Services",
            ["Confirm_CleanLegacyBody"] = "Stop and remove any conflicting third-party DPI or tunneling services (ByeDPI, GoodbyeDPI, Zapret, WinDivert, WireSock) and Discord drover leftovers?\n\nYour active RyanDPI installation will not be affected.",
            ["Dlg_AlreadyRunningTitle"] = "RyanDPI Already Running",
            ["Dlg_AlreadyRunningBody"] = "RyanDPI is already running. Check your taskbar or end RyanDPI.exe in Task Manager.",
            ["Dlg_AdminRequiredTitle"] = "Administrator Privileges Required",
            ["Dlg_AdminRequiredBody"] = "RyanDPI requires Administrator privileges to manage Windows network services and DNS.\n\nPlease right-click RyanDPI.exe and select 'Run as administrator'.",
            ["Dlg_ErrorTitle"] = "RyanDPI Error",
            ["Dlg_ErrorBody"] = "An unexpected error occurred:\n{0}",
            ["Dlg_CriticalTitle"] = "RyanDPI Critical Error",
            ["Dlg_CriticalBody"] = "A critical error occurred:\n{0}"
        };

        private static readonly Dictionary<string, string> Tr = new()
        {
            ["Badge_AllAppsSpeed"] = "EVRENSEL • SIFIR GECİKME",
            ["LangButton_Text"] = "TR",
            ["LangButton_Tooltip"] = "Dil değiştir (EN / TR / PT-BR / RU)",
            ["RegionButton_Tooltip"] = "Ülke / Aşım Modu Seç (Evrensel, Türkiye, Rusya, Brezilya)",
            ["ThemeButton_Tooltip"] = "Koyu / Açık Tema Değiştir",
            ["Minimize_Tooltip"] = "Küçült",
            ["Close_Tooltip"] = "Kapat",
            ["Refresh_Tooltip"] = "Durumu Yenile",
            ["Nav_Home"] = "Ana Sayfa",
            ["Nav_Settings"] = "Ayarlar",
            ["Nav_Tools"] = "Araçlar ve Günlük",

            ["Region_PickerTitle"] = "Bölge Modunu Seç",
            ["Region_PickerSubtitle"] = "En hızlı sıfır gecikmeli ayarlar için ülkeni seç veya tüm DPI engelli ülkelerde çalışan Evrensel modu kullan.",
            ["Region_GlobalTag"] = "EVRENSEL",
            ["Region_TurkeyTag"] = "TÜRKİYE",
            ["Region_RussiaTag"] = "RUSYA",
            ["Region_BrazilTag"] = "BREZİLYA",
            ["Region_GlobalTitle"] = "Evrensel Küresel (Önerilen)",
            ["Region_GlobalDesc"] = "Türkiye, Rusya ve tüm DPI engelli İSS'lerde 24 saat IP önbelleğiyle sıfır gecikmeli 7 aşamalı otomatik motor.",
            ["Region_TurkeyTitle"] = "Türkiye — BTK Sıfır Gecikme",
            ["Region_TurkeyDesc"] = "Discord, Roblox, Roblox Studio ve Wattpad'i tam hızda açar. Oyun UDP trafiği 0 ms ping için doğrudan kalır.",
            ["Region_RussiaTitle"] = "Rusya — TSPU Tam Erişim",
            ["Region_RussiaDesc"] = "YouTube, Discord (+ Ses UDP), Instagram ve Roblox için TSPU öncelikli split/disorder/sahte-SNI zinciri.",
            ["Region_BrazilTitle"] = "Brezilya — Discord Ekran Paylaşımı & Video",
            ["Region_BrazilDesc"] = "ANPD Coğrafi-IP kilidini aşar: SADECE Discord'u tüneller (oyunlarda 0 ms ping!) — dahili WARP MASQUE veya Özel SOCKS5.",
            ["Region_PickerClose"] = "Devam Et",

            ["Pill_EngineTitle"] = "MOTOR",
            ["Pill_RouterTitle"] = "YÖNLENDİRİCİ",
            ["Pill_DnsTitle"] = "GÜVENLİ DNS",
            ["State_Checking"] = "Kontrol...",
            ["State_Running"] = "Çalışıyor",
            ["State_Stopped"] = "Durduruldu",
            ["State_NotSet"] = "Kurulu Değil",
            ["State_AllApps"] = "Tüm Uyg.",
            ["State_DiscordOnly"] = "Sadece Discord",
            ["State_Paused"] = "Duraklatıldı",
            ["State_Encrypted"] = "Şifreli",
            ["State_NativeDoH"] = "Yerel DoH",
            ["State_Custom"] = "Özel",
            ["State_Default"] = "Doğrudan",

            ["Status_CheckingHeadline"] = "Durum kontrol ediliyor...",
            ["Status_CheckingSubtitle"] = "Windows hizmetleri ve ağ ayarları taranıyor...",
            ["Status_ActiveHeadline"] = "RyanDPI Aktif",
            ["Status_ActiveSubtitle"] = "Tüm engelli uygulamalar ve web siteleri internet hızın düşmeden otomatik olarak açılıyor.",
            ["Status_ActiveSubtitleBrazil"] = "Brezilya Discord Bölünmüş Tüneli aktif — SADECE Discord tünelleniyor, tüm oyunlar 0 ms pingde!",
            ["Status_ActiveCta"] = "RyanDPI'yı Duraklat",
            ["Status_ActiveCaption"] = "Arka planda Windows hizmeti olarak çalışır — her yeniden başlatmada otomatik aktif kalır.",
            ["Status_PartialHeadline"] = "RyanDPI Kısmen Aktif",
            ["Status_PartialSubtitle"] = "Arka plan hizmetlerinden biri durdurulmuş. İkisini birden başlatmak için aşağıya tıkla.",
            ["Status_PausedHeadline"] = "RyanDPI Duraklatıldı",
            ["Status_PausedSubtitle"] = "RyanDPI kurulu ve hazır. Anında erişimi tekrar açmak için aşağıya tıkla.",
            ["Status_PausedCta"] = "RyanDPI'yı Etkinleştir",
            ["Status_PausedCaption"] = "Seçili bölge aşım modunu oyun pingini etkilemeden açar.",
            ["Status_ReadyHeadline"] = "Her Şeyi Açmaya Hazır",
            ["Status_ReadySubtitle"] = "Tek tıkla kurulum Discord, Roblox, YouTube ve engelli tüm uygulama/siteleri gecikmesiz açar.",
            ["Status_ReadySubtitleBrazil"] = "Tek tıkla Brezilya Discord Tüneli Ekran Paylaşımını açar, oyunları 0 ms pingde doğrudan bırakır.",
            ["Status_ReadyLegacySubtitle"] = "Eski DPI aracı tespit edildi ({0}). RyanDPI bunu temizce değiştirecek.",
            ["Status_ReadyCta"] = "RyanDPI'yı Kur ve Etkinleştir",
            ["Status_ReadyCaption"] = "Sıfır oyun ping kaybı — bölgeye özel yerel DPI aşımı ve Discord bölünmüş tüneli.",
            ["Btn_Reinstall"] = "Yeniden Kur / Onar",
            ["Btn_Uninstall"] = "Kaldır",
            ["Btn_RestartPc"] = "PC'yi Yeniden Başlat",

            ["SelfTest_Title"] = "Erişim ve Hız Testi",
            ["SelfTest_Subtitle"] = "Açılan servisleri, çıkış durumunu ve anlık ping değerini şimdi doğrula.",
            ["SelfTest_Run"] = "Testi Çalıştır",
            ["SelfTest_Testing"] = "Test Ediliyor...",
            ["SelfTest_Placeholder"] = "Canlı erişim ve tepki süresini doğrulamak için 'Testi Çalıştır'a tıkla.",
            ["SelfTest_Failed"] = "Başarısız",
            ["SelfTest_LocalEngine"] = "RyanDPI Yerel Motoru",
            ["SelfTest_LocalEngineActive"] = "127.0.0.1:{0} üzerinde aktif (Tüm Uygulamalar)",
            ["SelfTest_BrazilEngineActive"] = "Discord Bölünmüş Tüneli aktif ({0} — Oyunlar %100 Doğrudan)",
            ["SelfTest_LocalEngineLegacy"] = ":1080 portunda eski proxy var — yükseltmek için RyanDPI'yı Kur'a tıkla",
            ["SelfTest_LocalEngineStopped"] = "RyanDPI Motoru çalışmıyor",
            ["SelfTest_LocalRouterStopped"] = "RyanDPI Yönlendirici (ProxiFyreService) çalışmıyor",
            ["SelfTest_LocalPortUnreachable"] = "{0} portuna ulaşılamıyor",
            ["SelfTest_Dns"] = "DNS Çözümleme",
            ["SelfTest_DnsClean"] = "Temiz Şifreli DNS (Engelli alan adları çözüldü)",
            ["SelfTest_DnsEmpty"] = "DNS sorgusu adres döndürmedi",
            ["SelfTest_DnsFailed"] = "DNS hatası: {0}",
            ["SelfTest_Discord"] = "Discord API",
            ["SelfTest_DiscordMedia"] = "Discord Medya CDN",
            ["SelfTest_YouTube"] = "YouTube",
            ["SelfTest_Instagram"] = "Instagram",
            ["SelfTest_RobloxApp"] = "Roblox Uygulaması (Player)",
            ["SelfTest_RobloxStudio"] = "Roblox Studio API",
            ["SelfTest_RobloxWeb"] = "Roblox Web",
            ["SelfTest_Wattpad"] = "Wattpad",
            ["SelfTest_DirectBaseline"] = "Doğrudan Hız Referansı",
            ["SelfTest_DirectGameBaseline"] = "Doğrudan Oyun Pingi (0 ms Kayıp)",
            ["SelfTest_BrazilExitCheck"] = "Discord Tüneli Çıkış IP'si",
            ["SelfTest_BrazilExitNonBr"] = "Brezilya Dışı Çıkış: {0} ({1}) — Ekran Paylaşımı Hazır!",
            ["SelfTest_BrazilExitStillBr"] = "Çıkış BR ({0}) — Yayın açılmazsa Ayarlar'dan Brezilya dışı SOCKS5 gir",
            ["SelfTest_UnlockedFmt"] = "Erişim Açık (HTTP {0} • İlk {1} ms)",
            ["SelfTest_UnexpectedHttp"] = "Beklenmeyen HTTP {0}",
            ["SelfTest_BlockedFmt"] = "Engelli / Zaman Aşımı ({0})",

            ["Settings_RegionTitle"] = "Ülke / Bölge Modu",
            ["Settings_BrazilCardTitle"] = "Brezilya Discord Bölünmüş Tüneli (0 ms Oyun Pingi)",
            ["Settings_BrazilNotice"] = "Discord Brezilya'da ekran paylaşımını İSS DPI ile değil Coğrafi-IP (ANPD) ile kısıtlar. RyanDPI SADECE Discord'u tüneller; Valorant, CS2, Roblox ve tüm oyunlar %100 doğrudan kalır!",
            ["Brazil_TunnelWarp"] = "Dahili Ücretsiz Cloudflare WARP MASQUE (Giriş Yok)",
            ["Brazil_TunnelCustom"] = "Özel Brezilya Dışı SOCKS5 Proxy (ABD / AR / AB — Garantili)",
            ["Brazil_CustomEndpointLabel"] = "Brezilya Dışı SOCKS5 Adresi (host:port):",
            ["Brazil_CustomUserLabel"] = "Kullanıcı Adı (isteğe bağlı):",
            ["Brazil_CustomPassLabel"] = "Şifre (isteğe bağlı):",
            ["Brazil_UdpTitle"] = "Discord UDP'yi de Tünelle (Yayın & Ses)",
            ["Brazil_UdpDesc"] = "AÇIK: tam TCP+UDP. KAPALI: yalnızca API kilidini açar (ses/yayın doğrudan).",
            ["Brazil_BrowsersTitle"] = "Web Tarayıcılarını da Ekle (Discord Web)",
            ["Brazil_BrowsersDesc"] = "Tarayıcıdan ekran paylaşımı yapıyorsan Chrome, Edge, Firefox, Brave ve Opera'yı ekler.",

            ["Settings_ProfileTitle"] = "DPI Aşım Profili",
            ["Profile_AutoUniversal"] = "Otomatik Evrensel (7 Aşamalı — Diğer Ülkeler)",
            ["Profile_TurkeyBtk"] = "Türkiye BTK (Ultra Hafif 3 Aşamalı — En Düşük Ping)",
            ["Profile_RussiaTspu"] = "Rusya TSPU (YouTube, Discord, Instagram — İlk Pakette)",
            ["Profile_Classic"] = "Klasik Mod (Hafif TLS Kayıt Bölme)",
            ["Profile_StrictIsp"] = "Sıkı İSS (Superonline / Mobil / Agresif DPI)",
            ["Profile_Custom"] = "Özel Parametreler",
            ["Profile_Desc_Auto"] = "Listede olmayan ülkeler için 7 aşamalı küresel strateji: 1. aşama 0 ms gecikme verir, 2-6. aşamalar sıkı İSS'lere 24 saat IP önbelleğiyle uyum sağlar.",
            ["Profile_Desc_Turkey"] = "Ultra hafif 3 aşamalı Türkiye BTK motoru: 0 ms 1. aşama (--tlsrec 1+s) + Superonline yedeği, gereksiz Rusya/İran denemeleri olmadan en düşük RAM ve ping.",
            ["Profile_Desc_Russia"] = "Rostelecom, MTS, Beeline, YouTube ve Discord için TSPU kanıtlı split + disorder + TLS kayıt bölme ile hemen başlar.",
            ["Profile_Desc_Classic"] = "Otomatik split/disorder yedeği ve düşük RAM'li tam hızlı tamponlarla hafif, sıfır gecikmeli TLS kayıt bölme (--tlsrec 1+s --mod-http=h,d).",
            ["Profile_Desc_Strict"] = "Doğrudan TLS kayıt + TCP bölme ile başlar, agresif İSS'ler için OOB, disorder ve sahte-SNI düşük TTL paketlerine geçer.",
            ["Profile_Desc_Custom"] = "Aşağıya özel ciadpi.exe parametrelerini gir. Düşük RAM'li tam hızlı soket ayarları (-i 127.0.0.1 -p 1080 -c 2048 -b 65536) otomatik eklenir.",
            ["Settings_CustomArgsLabel"] = "Özel ciadpi.exe strateji parametreleri:",

            ["Settings_DnsTitle"] = "DNS Koruması",
            ["Dns_Encrypted"] = "Şifreli DNS — DNSCrypt-Proxy (TR / RU için Önerilen)",
            ["Dns_NativeDoH"] = "Windows Yerel DoH (Google + Quad9)",
            ["Dns_KeepSystem"] = "Mevcut DNS Ayarlarımı Koru (Brezilya için Önerilen)",
            ["Dns_Desc_Encrypted"] = "8.192 kayıtlık önbellekle Cloudflare, Google ve Quad9 üzerinden yerel şifreli DNS çalıştırır. İSS DNS engellemesini tamamen aşar.",
            ["Dns_Desc_NativeDoH"] = "Yerel DNS proxy çalıştırmadan Windows 11 yerleşik DoH özelliğini Google (8.8.8.8) ve Quad9 (9.9.9.9) ile kullanır.",
            ["Dns_Desc_KeepSystem"] = "Ağ bağdaştırıcının DNS ayarlarına dokunmaz — Brezilya veya kendi özel DNS'in için idealdir.",

            ["Settings_NetworkTitle"] = "Ağ ve Hız Seçenekleri",
            ["Settings_BypassLanTitle"] = "Yerel Ağ (LAN) ve Localhost'u Atla",
            ["Settings_BypassLanDesc"] = "Yerel cihazları, modem arayüzünü ve localhost geliştirici sunucularını tamamen doğrudan tutar.",
            ["Settings_TunnelUdpTitle"] = "UDP'yi de Tünelle (Ses / QUIC)",
            ["Settings_TunnelUdpDesc"] = "Türkiye'de 0 ms oyun pingi için KAPALI tut; Rusya'da Discord sesi engelliyse AÇ.",
            ["Settings_AutoUpdateTitle"] = "Başlangıçta Otomatik Güncelle",
            ["Settings_AutoUpdateDesc"] = "Uygulama açıldığında yeni RyanDPI sürümlerini GitHub'dan otomatik indirip kurar.",

            ["Settings_ExcludesTitle"] = "Hariç Tutulan Uygulamalar (İsteğe Bağlı)",
            ["Settings_ExcludesDesc"] = "Evrensel/Türkiye/Rusya modunda tüm uygulamalar açıktır. Yalnızca atlamak istediğin uygulamaları ekle.",
            ["Settings_ExcludeTooltip"] = "Uygulama adı girin (ör. oyun.exe)",
            ["Btn_Add"] = "Ekle",
            ["Btn_Browse"] = "Gözat...",
            ["Settings_NoExcludes"] = "Özel hariç tutma ayarlanmadı.",
            ["Btn_SaveApply"] = "Ayarları Kaydet ve Uygula",
            ["Btn_Applying"] = "Uygulanıyor...",
            ["BrowseExclude_Title"] = "RyanDPI'dan hariç tutulacak uygulamayı seç",
            ["BrowseExclude_Filter"] = "Çalıştırılabilir dosyalar (*.exe)|*.exe|Tüm dosyalar (*.*)|*.*",

            ["Tools_Title"] = "Sistem Araçları",
            ["Btn_ResetDns"] = "DNS Sıfırla ve Önbelleği Temizle",
            ["Btn_CleanLegacy"] = "Eski DPI Hizmetlerini Temizle",
            ["Btn_CleanLegacyCount"] = "Eski DPI Temizle ({0})",
            ["Btn_OpenLogs"] = "Günlük Klasörünü Aç",
            ["Btn_UninstallFull"] = "RyanDPI'yı Kaldır",
            ["Logs_Title"] = "Canlı Etkinlik Günlüğü",
            ["Btn_Copy"] = "Kopyala",
            ["Btn_Clear"] = "Temizle",
            ["About_TitleFmt"] = "RyanDPI v{0} Unbound — Küresel Sıfır Gecikmeli DPI Aşımı & Bölünmüş Tünel",
            ["About_Subtitle"] = "Türkiye • Rusya • Brezilya • Dünya Geneli. ByeDPI, ProxiFyre, DNSCrypt-Proxy & Usque WARP MASQUE.",
            ["Btn_CheckUpdates"] = "Güncelleme Denetle",
            ["Btn_Checking"] = "Kontrol...",
            ["Btn_Updating"] = "Güncelleniyor...",

            ["Progress_InstallingTitle"] = "RyanDPI Kuruluyor...",
            ["Progress_Starting"] = "Başlatılıyor...",
            ["Progress_Initializing"] = "Kurulum hazırlanıyor...",
            ["Progress_StepFmt"] = "Adım {0} / {1}",
            ["Step1_Title"] = "Sistem hazırlanıyor",
            ["Step1_Detail"] = "Çakışan eski DPI veya VPN hizmetleri durdurulup kaldırılıyor...",
            ["Step2_Title"] = "Gereksinimler kontrol ediliyor",
            ["Step2_Detail"] = "Windows Packet Filter sürücüsü (NDISRD) ve VC++ Runtime doğrulanıyor...",
            ["Step3_Title"] = "RyanDPI motoru yerleştiriliyor",
            ["Step3_Detail"] = "Bölgeye özel yönlendirici, motor dosyaları ve ACL güvenlik izinleri kuruluyor...",
            ["Step4_Title"] = "DNS ve Güvenlik Duvarı ayarlanıyor",
            ["Step4_Detail"] = "DNS koruması ve Windows Güvenlik Duvarı kuralları uygulanıyor...",
            ["Step5_Title"] = "RyanDPI başlatılıyor",
            ["Step5_Detail"] = "Yerel motor ve şeffaf uygulama yönlendirici başlatılıyor...",
            ["Progress_UninstallingTitle"] = "RyanDPI Kaldırılıyor",
            ["Progress_CleaningUp"] = "Temizleniyor...",
            ["Progress_UninstallingDetail"] = "Hizmetler durduruluyor, güvenlik duvarı kuralları kaldırılıyor ve otomatik DNS geri yükleniyor...",

            ["Msg_NdisFailed"] = "Windows Packet Filter sürücüsü (NDISRD) kurulamadı. Ayrıntılar için Günlük sekmesine bak.",
            ["Msg_StageFailed"] = "RyanDPI motor dosyaları kopyalanamadı. Antivirüs karantinasını kontrol et.",
            ["Msg_EngineStartFailed"] = "RyanDPI motor hizmeti başlatılamadı. Ayrıntılar için Günlük sekmesine bak.",
            ["Msg_RebootNeeded"] = "RyanDPI kuruldu! Yeni Paket Filtre sürücüsünün yüklenmesi için PC'yi bir kez yeniden başlat.",
            ["Msg_RouterStartFailed"] = "RyanDPI yönlendirici hizmeti (ProxiFyreService) başlatılamadı. Günlük sekmesine bak.",
            ["Msg_InstallSuccess"] = "RyanDPI aktif! ({0})",
            ["Msg_InstallError"] = "Kurulum hatası: {0}",
            ["Msg_Started"] = "RyanDPI artık aktif.",
            ["Msg_StartEngineFail"] = "RyanDPI motoru başlatılamadı. Günlük sekmesine bak.",
            ["Msg_StartRouterFail"] = "RyanDPI Yönlendirici (ProxiFyreService) başlatılamadı. Günlük sekmesine bak.",
            ["Msg_Paused"] = "RyanDPI duraklatıldı.",
            ["Msg_SettingsSavedNotInstalled"] = "Ayarlar kaydedildi! Etkinleştirmek için Ana Sayfa'dan 'RyanDPI'yı Kur ve Etkinleştir'e tıkla.",
            ["Msg_SettingsApplyFailed"] = "Ayarlar uygulanamadı: {0}",
            ["Msg_CustomSocksInvalid"] = "Lütfen host:port biçiminde geçerli bir SOCKS5 adresi girin (ör. 1.2.3.4:1080).",
            ["Msg_LegacyRemoved"] = "{0} adet eski DPI/tünel hizmeti temizlendi.",
            ["Msg_LegacyNone"] = "Çakışan eski DPI hizmeti bulunamadı — sistemin temiz.",
            ["Msg_UninstallSuccess"] = "RyanDPI, arka plan hizmetleri, güvenlik duvarı kuralları ve DNS değişiklikleri tamamen kaldırıldı.",
            ["Msg_UninstallError"] = "Kaldırma hatası: {0}",
            ["Msg_DnsResetOk"] = "DNS ve DoH ayarları Otomatik (DHCP) olarak sıfırlandı ve DNS önbelleği temizlendi.",
            ["Msg_DnsResetWarn"] = "DNS sıfırlama uyarılarla tamamlandı. Günlükleri kontrol et.",
            ["DnsSummary_Kept"] = "Mevcut sistem DNS ayarları korundu.",
            ["DnsSummary_NativeDoH"] = "Windows Yerel DoH (Google + Quad9) yapılandırıldı.",
            ["DnsSummary_NativeDoHFail"] = "Windows Yerel DoH yapılandırması doğrulanamadı.",
            ["DnsSummary_EncryptedOk"] = "Şifreli DNS (DNSCrypt-Proxy) aktif.",
            ["DnsSummary_FallbackDoH"] = "Yedek aktif: Windows Yerel DoH (Google + Quad9).",
            ["DnsSummary_AllFail"] = "Hem DNSCrypt-Proxy hem Windows DoH başarısız oldu; sistem DNS'i değiştirilmedi.",

            ["Update_FinishingTitle"] = "{0} güncellemesi tamamlanıyor...",
            ["Update_SyncingStep"] = "Motor güncelleniyor",
            ["Update_SyncingDetail"] = "Arka plan RyanDPI motor dosyaları, yapılandırma ve güvenlik duvarı kuralları güncelleniyor...",
            ["Update_UpdatedRefreshed"] = "RyanDPI otomatik olarak {0} sürümüne güncellendi ve arka plan hizmetleri yenilendi!",
            ["Update_UpdatedWarn"] = "{0} sürümüne güncellendi: {1}",
            ["Update_UpdatedSimple"] = "RyanDPI otomatik olarak {0} sürümüne güncellendi!",
            ["Update_CheckFailed"] = "Güncellemeler denetlenemedi: {0}",
            ["Update_UpToDate"] = "En güncel sürümü kullanıyorsun (RyanDPI v{0}).",
            ["Update_UpdatingTitle"] = "RyanDPI v{0} sürümüne güncelleniyor...",
            ["Update_StartingStep"] = "Başlatılıyor",
            ["Update_StartingDetail"] = "Yeni sürüm v{0} bulundu. Güncelleme otomatik indiriliyor...",
            ["Update_DownloadingStep"] = "İndiriliyor",
            ["Update_DownloadingFile"] = "GitHub Releases üzerinden {0} indiriliyor...",
            ["Update_DownloadingPct"] = "{0} indiriliyor ({1:F1}{2})...",
            ["Update_VerifyingStep"] = "Doğrulanıyor",
            ["Update_VerifyingDetail"] = "İndirme bütünlüğü doğrulanıyor (boyut, biçim ve SHA-256)...",
            ["Update_InstallingTitle"] = "RyanDPI v{0} kuruluyor...",
            ["Update_RestartingStep"] = "Yeniden başlatılıyor",
            ["Update_RestartingDetail"] = "Sessiz güncelleme başlatılıyor ve RyanDPI yeniden açılıyor...",
            ["Update_Failed"] = "v{0} otomatik güncellemesi başarısız oldu: {1}",
            ["Update_LaunchFailed"] = "v{0} indirildi ancak kurulum otomatik başlatılamadı.",
            ["Update_BgRefreshFail"] = "Uygulama güncellendi ancak arka plan motor dosyaları yenilenemedi. 'Yeniden Kur / Onar'a tıkla.",
            ["Update_BgRefreshOk"] = "Arka plan hizmetleri temizce yükseltildi.",
            ["Update_BgRefreshRestartFail"] = "Uygulama güncellendi ancak arka plan hizmetleri yeniden başlatılamadı. 'Yeniden Kur / Onar'a tıkla.",
            ["Update_BgRefreshPaused"] = "Arka plan motoru güncellendi (şu an duraklatılmış).",

            ["Confirm_UninstallTitle"] = "RyanDPI'yı Kaldır",
            ["Confirm_UninstallBody"] = "RyanDPI hizmetleri, güvenlik duvarı kuralları tamamen kaldırılsın ve DNS ayarları Otomatik (DHCP) olarak sıfırlansın mı?",
            ["Confirm_RebootTitle"] = "PC'yi Yeniden Başlat",
            ["Confirm_RebootBody"] = "Bilgisayarın şimdi yeniden başlatılsın mı? Açık çalışmalarını kaydettiğinden emin ol.",
            ["Confirm_ResetDnsTitle"] = "DNS Sıfırla ve Önbelleği Temizle",
            ["Confirm_ResetDnsBody"] = "Tüm fiziksel ağ bağdaştırıcılarındaki DNS ve DoH ayarları Otomatik (DHCP) olarak sıfırlansın ve DNS önbelleği temizlensin mi?",
            ["Confirm_CleanLegacyTitle"] = "Eski DPI Hizmetlerini Temizle",
            ["Confirm_CleanLegacyBody"] = "Çakışan üçüncü parti DPI veya tünel hizmetleri (ByeDPI, GoodbyeDPI, Zapret, WinDivert, WireSock) kaldırılsın mı?\n\nAktif RyanDPI kurulumun etkilenmeyecektir.",
            ["Dlg_AlreadyRunningTitle"] = "RyanDPI Zaten Çalışıyor",
            ["Dlg_AlreadyRunningBody"] = "RyanDPI zaten çalışıyor. Görev çubuğunu kontrol et veya Görev Yöneticisi'nden RyanDPI.exe işlemini kapat.",
            ["Dlg_AdminRequiredTitle"] = "Yönetici Yetkisi Gerekli",
            ["Dlg_AdminRequiredBody"] = "RyanDPI'nın Windows ağ hizmetlerini ve DNS'i yönetebilmesi için Yönetici yetkisi gerekir.\n\nLütfen RyanDPI.exe dosyasına sağ tıklayıp 'Yönetici olarak çalıştır'ı seç.",
            ["Dlg_ErrorTitle"] = "RyanDPI Hatası",
            ["Dlg_ErrorBody"] = "Beklenmeyen bir hata oluştu:\n{0}",
            ["Dlg_CriticalTitle"] = "RyanDPI Kritik Hata",
            ["Dlg_CriticalBody"] = "Kritik bir hata oluştu:\n{0}"
        };

        // Brazilian Portuguese (PT-BR) — inherits any non-overridden keys cleanly from En and overrides all core UI
        private static readonly Dictionary<string, string> Pt = BuildLocalizedOverlay(new Dictionary<string, string>
        {
            ["Badge_AllAppsSpeed"] = "GLOBAL • ZERO LAG",
            ["LangButton_Text"] = "PT",
            ["LangButton_Tooltip"] = "Mudar idioma (EN / TR / PT-BR / RU)",
            ["RegionButton_Tooltip"] = "Escolher País / Modo (Global, Turquia, Rússia, Brasil)",
            ["Nav_Home"] = "Início",
            ["Nav_Settings"] = "Configurações",
            ["Nav_Tools"] = "Ferramentas & Logs",
            ["Region_PickerTitle"] = "Escolha o Modo do Seu País",
            ["Region_PickerSubtitle"] = "Selecione seu país para configuração instantânea sem lag nos jogos, ou use o modo Global Universal.",
            ["Region_GlobalTag"] = "GLOBAL",
            ["Region_TurkeyTag"] = "TURQUIA",
            ["Region_RussiaTag"] = "RÚSSIA",
            ["Region_BrazilTag"] = "BRASIL",
            ["Region_GlobalTitle"] = "Global Universal (Recomendado)",
            ["Region_GlobalDesc"] = "Motor DPI sem lag de 7 estágios que se adapta automaticamente na Turquia, Rússia e qualquer país com DPI.",
            ["Region_TurkeyTitle"] = "Turquia — BTK Zero Lag",
            ["Region_TurkeyDesc"] = "Desbloqueia Discord, Roblox, Roblox Studio e Wattpad na velocidade total da internet.",
            ["Region_RussiaTitle"] = "Rússia — TSPU Desbloqueio Total",
            ["Region_RussiaDesc"] = "Estratégia TSPU para YouTube, Discord (+ Voz UDP), Instagram e Roblox.",
            ["Region_BrazilTitle"] = "Brasil — Compartilhar Tela & Vídeo no Discord",
            ["Region_BrazilDesc"] = "Desbloqueio Geo-IP ANPD: tunela APENAS o Discord (0 ms de lag nos jogos!) via WARP MASQUE integrado ou SOCKS5.",
            ["Region_PickerClose"] = "Continuar",
            ["State_DiscordOnly"] = "Só Discord",
            ["State_AllApps"] = "Todos Apps",
            ["State_Running"] = "Ativo",
            ["State_Stopped"] = "Parado",
            ["State_Paused"] = "Pausado",
            ["Status_ActiveHeadline"] = "RyanDPI está Ativo",
            ["Status_ActiveSubtitle"] = "Todos os apps e sites bloqueados estão liberados na velocidade total da sua internet.",
            ["Status_ActiveSubtitleBrazil"] = "Split-Tunnel Brasil ativo — APENAS o Discord passa pelo túnel; todos os seus jogos ficam com 0 ms de lag!",
            ["Status_ActiveCta"] = "Pausar RyanDPI",
            ["Status_PausedHeadline"] = "RyanDPI Pausado",
            ["Status_PausedCta"] = "Ativar RyanDPI",
            ["Status_ReadyHeadline"] = "Pronto para Desbloquear Tudo",
            ["Status_ReadySubtitleBrazil"] = "Desbloqueie o Compartilhamento de Tela e Vídeo do Discord no Brasil mantendo 0 ms de lag nos jogos.",
            ["Status_ReadyCta"] = "Instalar & Ativar RyanDPI",
            ["Btn_Reinstall"] = "Reinstalar / Reparar",
            ["Btn_Uninstall"] = "Desinstalar",
            ["SelfTest_Title"] = "Teste de Desbloqueio & Ping",
            ["SelfTest_Subtitle"] = "Verifique agora o túnel do Discord, país de saída e ping direto dos jogos.",
            ["SelfTest_Run"] = "Testar Agora",
            ["SelfTest_Testing"] = "Testando...",
            ["SelfTest_BrazilEngineActive"] = "Split-Tunnel Discord ativo ({0} — Jogos 100% Diretos)",
            ["SelfTest_DirectGameBaseline"] = "Ping Direto de Jogos (0 ms Lag)",
            ["SelfTest_BrazilExitCheck"] = "IP de Saída do Túnel Discord",
            ["SelfTest_BrazilExitNonBr"] = "Saída fora do BR: {0} ({1}) — Transmissão Liberada!",
            ["SelfTest_BrazilExitStillBr"] = "Saída ainda no BR ({0}) — Se o Go Live continuar bloqueado, use um SOCKS5 fora do BR nas Configurações",
            ["Settings_RegionTitle"] = "País / Modo de Desbloqueio",
            ["Settings_BrazilCardTitle"] = "Split-Tunnel Discord Brasil (0 ms Lag em Jogos)",
            ["Settings_BrazilNotice"] = "No Brasil, o Discord bloqueia Compartilhar Tela e Vídeo por Geo-IP (ordem da ANPD), não por DPI do provedor. O RyanDPI tunela APENAS o Discord — Valorant, CS2, Roblox e todos os jogos ficam 100% diretos!",
            ["Brazil_TunnelWarp"] = "Cloudflare WARP MASQUE Grátis Integrado (Sem Login)",
            ["Brazil_TunnelCustom"] = "Proxy SOCKS5 Personalizado fora do Brasil (EUA / AR / EU — Garantido)",
            ["Brazil_CustomEndpointLabel"] = "Endereço SOCKS5 fora do Brasil (host:porta):",
            ["Brazil_CustomUserLabel"] = "Usuário (opcional):",
            ["Brazil_CustomPassLabel"] = "Senha (opcional):",
            ["Brazil_UdpTitle"] = "Tunelar UDP do Discord (Vídeo Go Live & Voz)",
            ["Brazil_UdpDesc"] = "ATIVADO: TCP+UDP completo. DESATIVADO: apenas desbloqueia a API (voz/vídeo direto).",
            ["Brazil_BrowsersTitle"] = "Incluir Navegadores (Discord Web)",
            ["Brazil_BrowsersDesc"] = "Inclui Chrome, Edge, Firefox, Brave e Opera se você transmite tela pelo navegador.",
            ["Btn_SaveApply"] = "Salvar & Aplicar Configurações",
            ["Btn_Applying"] = "Aplicando...",
            ["Msg_CustomSocksInvalid"] = "Informe um endereço SOCKS5 válido no formato host:porta (ex: 1.2.3.4:1080)."
        });

        // Russian (RU) — inherits complete parity from En and overrides all primary UI strings
        private static readonly Dictionary<string, string> Ru = BuildLocalizedOverlay(new Dictionary<string, string>
        {
            ["Badge_AllAppsSpeed"] = "ГЛОБАЛЬНЫЙ • БЕЗ ЛАГОВ",
            ["LangButton_Text"] = "RU",
            ["LangButton_Tooltip"] = "Сменить язык (EN / TR / PT-BR / RU)",
            ["RegionButton_Tooltip"] = "Выбрать страну / режим (Global, Турция, Россия, Бразилия)",
            ["Nav_Home"] = "Главная",
            ["Nav_Settings"] = "Настройки",
            ["Nav_Tools"] = "Инструменты и Логи",
            ["Region_PickerTitle"] = "Выберите регион",
            ["Region_PickerSubtitle"] = "Выберите вашу страну для оптимальных настроек без пинга или используйте универсальный глобальный режим.",
            ["Region_GlobalTag"] = "ГЛОБАЛЬНЫЙ",
            ["Region_TurkeyTag"] = "ТУРЦИЯ",
            ["Region_RussiaTag"] = "РОССИЯ",
            ["Region_BrazilTag"] = "БРАЗИЛИЯ",
            ["Region_GlobalTitle"] = "Универсальный Глобальный (Рекомендуется)",
            ["Region_GlobalDesc"] = "7-этапный DPI-движок без задержек: автоматически подстраивается под ТСПУ РФ, Турцию и любые провайдеры.",
            ["Region_TurkeyTitle"] = "Турция — BTK Без Задержек",
            ["Region_TurkeyDesc"] = "Открывает Discord, Roblox, Roblox Studio и Wattpad на полной скорости без пинга в играх.",
            ["Region_RussiaTitle"] = "Россия — Полный обход ТСПУ",
            ["Region_RussiaDesc"] = "Мгновенная стратегия под ТСПУ (split + disorder + fake-SNI) + UDP голос для YouTube, Discord, Instagram и Roblox.",
            ["Region_BrazilTitle"] = "Бразилия — Демонстрация экрана Discord",
            ["Region_BrazilDesc"] = "Раздельный туннель ТОЛЬКО для Discord (0 мс пинга в играх!) через WARP MASQUE или свой SOCKS5.",
            ["Region_PickerClose"] = "Продолжить",
            ["State_DiscordOnly"] = "Только Discord",
            ["State_AllApps"] = "Все прогр.",
            ["State_Running"] = "Работает",
            ["State_Stopped"] = "Остановлен",
            ["State_Paused"] = "На паузе",
            ["Status_ActiveHeadline"] = "RyanDPI Активен",
            ["Status_ActiveSubtitle"] = "YouTube, Discord, Roblox и все заблокированные сервисы открыты на полной скорости.",
            ["Status_ActiveCta"] = "Приостановить RyanDPI",
            ["Status_PausedHeadline"] = "RyanDPI на паузе",
            ["Status_PausedCta"] = "Включить RyanDPI",
            ["Status_ReadyHeadline"] = "Готов к разблокировке",
            ["Status_ReadyCta"] = "Установить и включить RyanDPI",
            ["Btn_Reinstall"] = "Переустановить / Починить",
            ["Btn_Uninstall"] = "Удалить",
            ["SelfTest_Title"] = "Тест доступа и пинга",
            ["SelfTest_Subtitle"] = "Проверьте Discord, YouTube, Instagram, Roblox и реальный пинг прямо сейчас.",
            ["SelfTest_Run"] = "Запустить тест",
            ["SelfTest_Testing"] = "Проверка...",
            ["Settings_RegionTitle"] = "Страна / Режим обхода",
            ["Settings_ProfileTitle"] = "Профиль DPI обхода",
            ["Profile_RussiaTspu"] = "Россия ТСПУ (YouTube, Discord, Instagram — с 1-го пакета)",
            ["Btn_SaveApply"] = "Сохранить и применить",
            ["Btn_Applying"] = "Применение..."
        });

        // Persian / Farsi (FA — Iran: Instagram, YouTube, Telegram, WhatsApp, Discord)
        private static readonly Dictionary<string, string> Fa = BuildLocalizedOverlay(new Dictionary<string, string>
        {
            ["Badge_AllAppsSpeed"] = "جهانی • بدون لگ",
            ["LangButton_Text"] = "FA",
            ["LangButton_Tooltip"] = "انتخاب زبان (EN / TR / PT-BR / RU / FA / ID / ZH / AR)",
            ["RegionButton_Tooltip"] = "انتخاب کشور / حالت دور زدن فیلترینگ",
            ["Nav_Home"] = "خانه",
            ["Nav_Settings"] = "تنظیمات",
            ["Nav_Tools"] = "ابزار و گزارش",
            ["Region_PickerTitle"] = "منطقه خود را انتخاب کنید",
            ["Region_PickerSubtitle"] = "برای بهترین سرعت بدون افزایش پینگ کشور خود را انتخاب کنید یا حالت جهانی را برگزینید.",
            ["Region_GlobalTitle"] = "جهانی خودکار (پیشنهادی)",
            ["Region_GlobalDesc"] = "موتور ۷ مرحله‌ای دور زدن DPI بدون افت سرعت برای ایران، ترکیه، روسیه و سراسر جهان.",
            ["Region_TurkeyTitle"] = "ترکیه — BTK بدون لگ",
            ["Region_RussiaTitle"] = "روسیه — عبور کامل از TSPU",
            ["Region_BrazilTitle"] = "برزیل — اشتراک صفحه دیسکورد",
            ["Region_PickerClose"] = "ادامه",
            ["State_AllApps"] = "همه برنامه‌ها",
            ["State_DiscordOnly"] = "فقط دیسکورد",
            ["State_Running"] = "فعال",
            ["State_Stopped"] = "متوقف",
            ["State_Paused"] = "مکث",
            ["Status_ActiveHeadline"] = "RyanDPI فعال است",
            ["Status_ActiveSubtitle"] = "تمام برنامه‌ها و سایت‌های مسدود با سرعت واقعی اینترنت و بدون کاهش سرعت باز هستند.",
            ["Status_ActiveCta"] = "توقف موقت RyanDPI",
            ["Status_PausedHeadline"] = "RyanDPI متوقف شده است",
            ["Status_PausedCta"] = "فعال‌سازی RyanDPI",
            ["Status_ReadyHeadline"] = "آماده برای رفع محدودیت",
            ["Status_ReadyCta"] = "نصب و فعال‌سازی RyanDPI",
            ["Btn_Reinstall"] = "نصب مجدد / تعمیر",
            ["Btn_Uninstall"] = "حذف کامل",
            ["SelfTest_Title"] = "تست دسترسی و پینگ زنده",
            ["SelfTest_Subtitle"] = "دسترسی به دیسکورد، روبلاکس، یوتیوب و پینگ واقعی را بررسی کنید.",
            ["SelfTest_Run"] = "اجرای تست",
            ["SelfTest_Testing"] = "در حال تست...",
            ["Settings_RegionTitle"] = "حالت کشور / منطقه",
            ["Settings_ProfileTitle"] = "پروفایل عبور از DPI",
            ["Settings_DnsTitle"] = "محافظت DNS رمزنگاری‌شده",
            ["Btn_SaveApply"] = "ذخیره و اعمال تنظیمات",
            ["Btn_Applying"] = "در حال اعمال...",
            ["About_TitleFmt"] = "RyanDPI v{0} Unbound — عبور جهانی از DPI بدون افت سرعت"
        });

        // Indonesian (ID — Reddit, Vimeo, DNS/DPI ISP blocks)
        private static readonly Dictionary<string, string> Id = BuildLocalizedOverlay(new Dictionary<string, string>
        {
            ["Badge_AllAppsSpeed"] = "GLOBAL • TANPA LAG",
            ["LangButton_Text"] = "ID",
            ["LangButton_Tooltip"] = "Pilih bahasa (EN / TR / PT-BR / RU / FA / ID / ZH / AR)",
            ["RegionButton_Tooltip"] = "Pilih Negara / Mode Bypass",
            ["Nav_Home"] = "Beranda",
            ["Nav_Settings"] = "Pengaturan",
            ["Nav_Tools"] = "Alat & Log",
            ["Region_PickerTitle"] = "Pilih Mode Wilayah Anda",
            ["Region_PickerSubtitle"] = "Pilih negara Anda untuk pengaturan instan tanpa lag, atau gunakan Global Universal.",
            ["Region_GlobalTitle"] = "Global Universal (Disarankan)",
            ["Region_GlobalDesc"] = "Mesin DPI 7 tahap tanpa lag yang beradaptasi otomatis di Indonesia, Turki, Rusia & seluruh dunia.",
            ["Region_TurkeyTitle"] = "Turki — BTK Tanpa Lag",
            ["Region_RussiaTitle"] = "Rusia — Buka Blokir TSPU",
            ["Region_BrazilTitle"] = "Brasil — Berbagi Layar Discord",
            ["Region_PickerClose"] = "Lanjutkan",
            ["State_AllApps"] = "Semua App",
            ["State_DiscordOnly"] = "Khusus Discord",
            ["State_Running"] = "Aktif",
            ["State_Stopped"] = "Berhenti",
            ["State_Paused"] = "Dijeda",
            ["Status_ActiveHeadline"] = "RyanDPI Aktif",
            ["Status_ActiveSubtitle"] = "Semua aplikasi dan situs yang diblokir terbuka otomatis pada kecepatan penuh internet Anda.",
            ["Status_ActiveCta"] = "Jeda RyanDPI",
            ["Status_PausedHeadline"] = "RyanDPI Dijeda",
            ["Status_PausedCta"] = "Aktifkan RyanDPI",
            ["Status_ReadyHeadline"] = "Siap Membuka Semua Blokir",
            ["Status_ReadyCta"] = "Pasang & Aktifkan RyanDPI",
            ["Btn_Reinstall"] = "Pasang Ulang / Perbaiki",
            ["Btn_Uninstall"] = "Hapus Instalasi",
            ["SelfTest_Title"] = "Tes Akses & Ping Langsung",
            ["SelfTest_Subtitle"] = "Periksa akses situs terblokir, DNS terenkripsi & ping langsung sekarang.",
            ["SelfTest_Run"] = "Jalankan Tes",
            ["SelfTest_Testing"] = "Menguji...",
            ["Settings_RegionTitle"] = "Mode Negara / Wilayah",
            ["Settings_ProfileTitle"] = "Profil Bypass DPI",
            ["Settings_DnsTitle"] = "Perlindungan DNS Terenkripsi",
            ["Btn_SaveApply"] = "Simpan & Terapkan",
            ["Btn_Applying"] = "Menerapkan...",
            ["About_TitleFmt"] = "RyanDPI v{0} Unbound — Bypass DPI Global Tanpa Lag"
        });

        // Simplified Chinese (ZH — China)
        private static readonly Dictionary<string, string> Zh = BuildLocalizedOverlay(new Dictionary<string, string>
        {
            ["Badge_AllAppsSpeed"] = "全球 • 零延迟",
            ["LangButton_Text"] = "ZH",
            ["LangButton_Tooltip"] = "切换语言 (EN / TR / PT-BR / RU / FA / ID / ZH / AR)",
            ["RegionButton_Tooltip"] = "选择国家 / 绕过模式",
            ["Nav_Home"] = "主页",
            ["Nav_Settings"] = "设置",
            ["Nav_Tools"] = "工具与日志",
            ["Region_PickerTitle"] = "选择您的地区模式",
            ["Region_PickerSubtitle"] = "选择您所在的国家以获取零延迟预设，或使用全球通用 DPI 绕过模式。",
            ["Region_GlobalTitle"] = "全球通用模式（推荐）",
            ["Region_GlobalDesc"] = "7 阶段零延迟自适应 DPI 引擎，支持土耳其、俄罗斯及全球常见 DPI 阻断网络。",
            ["Region_TurkeyTitle"] = "土耳其 — BTK 零延迟",
            ["Region_RussiaTitle"] = "俄罗斯 — TSPU 全面解锁",
            ["Region_BrazilTitle"] = "巴西 — Discord 屏幕共享与视频",
            ["Region_PickerClose"] = "继续",
            ["State_AllApps"] = "全部应用",
            ["State_DiscordOnly"] = "仅 Discord",
            ["State_Running"] = "运行中",
            ["State_Stopped"] = "已停止",
            ["State_Paused"] = "已暂停",
            ["Status_ActiveHeadline"] = "RyanDPI 已启用",
            ["Status_ActiveSubtitle"] = "所有受限应用与网站已以原生满速自动解锁，无远程 VPN 限速。",
            ["Status_ActiveCta"] = "暂停 RyanDPI",
            ["Status_PausedHeadline"] = "RyanDPI 已暂停",
            ["Status_PausedCta"] = "启用 RyanDPI",
            ["Status_ReadyHeadline"] = "准备就绪，一键解锁",
            ["Status_ReadyCta"] = "安装并启用 RyanDPI",
            ["Btn_Reinstall"] = "重新安装 / 修复",
            ["Btn_Uninstall"] = "卸载",
            ["SelfTest_Title"] = "解锁与延迟自测",
            ["SelfTest_Subtitle"] = "立即验证解锁服务、加密 DNS 与实时延迟。",
            ["SelfTest_Run"] = "运行测试",
            ["SelfTest_Testing"] = "测试中...",
            ["Settings_RegionTitle"] = "国家 / 地区模式",
            ["Settings_ProfileTitle"] = "DPI 绕过策略",
            ["Settings_DnsTitle"] = "加密 DNS 保护",
            ["Btn_SaveApply"] = "保存并应用设置",
            ["Btn_Applying"] = "应用中...",
            ["About_TitleFmt"] = "RyanDPI v{0} Unbound — 全球零延迟 DPI 绕过与分流引擎"
        });

        // Arabic (AR — Egypt / MENA VoIP & DPI blocks)
        private static readonly Dictionary<string, string> Ar = BuildLocalizedOverlay(new Dictionary<string, string>
        {
            ["Badge_AllAppsSpeed"] = "عالمي • بدون لاق",
            ["LangButton_Text"] = "AR",
            ["LangButton_Tooltip"] = "اختر اللغة (EN / TR / PT-BR / RU / FA / ID / ZH / AR)",
            ["RegionButton_Tooltip"] = "اختر الدولة / وضع فتح الحجب",
            ["Nav_Home"] = "الرئيسية",
            ["Nav_Settings"] = "الإعدادات",
            ["Nav_Tools"] = "الأدوات والسجلات",
            ["Region_PickerTitle"] = "اختر وضع دولتك",
            ["Region_PickerSubtitle"] = "اختر دولتك للحصول على أفضل إعدادات بدون زيادة البنق، أو استخدم الوضع العالمي الشامل.",
            ["Region_GlobalTitle"] = "عالمي شامل (موصى به)",
            ["Region_GlobalDesc"] = "محرك DPI من 7 مراحل بدون تأخير يتكيف تلقائياً في تركيا وروسيا ومصر والشرق الأوسط والعالم.",
            ["Region_TurkeyTitle"] = "تركيا — BTK بدون تأخير",
            ["Region_RussiaTitle"] = "روسيا — فتح شامل لـ TSPU",
            ["Region_BrazilTitle"] = "البرازيل — مشاركة شاشة ديسكورد",
            ["Region_PickerClose"] = "متابعة",
            ["State_AllApps"] = "كل التطبيقات",
            ["State_DiscordOnly"] = "ديسكورد فقط",
            ["State_Running"] = "يعمل",
            ["State_Stopped"] = "متوقف",
            ["State_Paused"] = "متوقف مؤقتاً",
            ["Status_ActiveHeadline"] = "RyanDPI نشط الآن",
            ["Status_ActiveSubtitle"] = "جميع التطبيقات والمواقع المحجوبة مفتوحة تلقائياً بكامل سرعة الإنترنت الأصلية.",
            ["Status_ActiveCta"] = "إيقاف RyanDPI مؤقتاً",
            ["Status_PausedHeadline"] = "RyanDPI متوقف مؤقتاً",
            ["Status_PausedCta"] = "تفعيل RyanDPI",
            ["Status_ReadyHeadline"] = "جاهز لفتح جميع التطبيقات",
            ["Status_ReadyCta"] = "تثبيت وتفعيل RyanDPI",
            ["Btn_Reinstall"] = "إعادة التثبيت / إصلاح",
            ["Btn_Uninstall"] = "إلغاء التثبيت",
            ["SelfTest_Title"] = "اختبار فتح الحجب والبنق",
            ["SelfTest_Subtitle"] = "تحقق الآن من فتح الخدمات وDNS المشفر والبنق المباشر.",
            ["SelfTest_Run"] = "بدء الاختبار",
            ["SelfTest_Testing"] = "جاري الاختبار...",
            ["Settings_RegionTitle"] = "وضع الدولة / المنطقة",
            ["Settings_ProfileTitle"] = "استراتيجية تجاوز DPI",
            ["Settings_DnsTitle"] = "حماية DNS المشفر",
            ["Btn_SaveApply"] = "حفظ وتطبيق الإعدادات",
            ["Btn_Applying"] = "جاري التطبيق...",
            ["About_TitleFmt"] = "RyanDPI v{0} Unbound — تجاوز DPI عالمي بدون لاق"
        });

        private static Dictionary<string, string> BuildLocalizedOverlay(Dictionary<string, string> overrides)
        {
            var merged = new Dictionary<string, string>(En);
            foreach (var kv in overrides)
            {
                merged[kv.Key] = kv.Value;
            }
            return merged;
        }
    }
}
