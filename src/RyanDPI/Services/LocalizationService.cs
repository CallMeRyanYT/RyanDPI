namespace RyanDPI.Services
{
    public static class LocalizationService
    {
        public const string English = "en";
        public const string Turkish = "tr";

        private static string _currentLanguage = English;

        public static string CurrentLanguage => _currentLanguage;
        public static bool IsTurkish => _currentLanguage == Turkish;

        public static event Action? LanguageChanged;

        public static void SetLanguage(string? languageCode)
        {
            var normalized = NormalizeLanguage(languageCode);
            if (_currentLanguage == normalized) return;

            _currentLanguage = normalized;
            LanguageChanged?.Invoke();
        }

        public static string NormalizeLanguage(string? languageCode)
        {
            return string.Equals(languageCode?.Trim(), Turkish, StringComparison.OrdinalIgnoreCase)
                ? Turkish
                : English;
        }

        public static string T(string key)
        {
            var dict = _currentLanguage == Turkish ? Tr : En;
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

        private static readonly Dictionary<string, string> En = new()
        {
            // Title bar & Nav
            ["Badge_AllAppsSpeed"] = "ALL APPS • FULL SPEED",
            ["LangButton_Text"] = "TR",
            ["LangButton_Tooltip"] = "Switch language to Turkish (Türkçe'ye geç)",
            ["ThemeButton_Tooltip"] = "Toggle Dark / Light Theme",
            ["Minimize_Tooltip"] = "Minimize",
            ["Close_Tooltip"] = "Close",
            ["Refresh_Tooltip"] = "Refresh Status",
            ["Nav_Home"] = "Home",
            ["Nav_Settings"] = "Settings",
            ["Nav_Tools"] = "Tools & Logs",

            // Health Strip
            ["Pill_EngineTitle"] = "DPI ENGINE",
            ["Pill_RouterTitle"] = "APP ROUTER",
            ["Pill_DnsTitle"] = "SECURE DNS",
            ["State_Checking"] = "Checking",
            ["State_Running"] = "Running",
            ["State_Stopped"] = "Stopped",
            ["State_NotSet"] = "Not Set",
            ["State_AllApps"] = "All Apps",
            ["State_Paused"] = "Paused",
            ["State_Encrypted"] = "Encrypted",
            ["State_NativeDoH"] = "Native DoH",
            ["State_Custom"] = "Custom",
            ["State_Default"] = "Default",

            // Hero Status
            ["Status_CheckingHeadline"] = "Checking status...",
            ["Status_CheckingSubtitle"] = "Scanning Windows services and encrypted DNS...",
            ["Status_ActiveHeadline"] = "RyanDPI is Active",
            ["Status_ActiveSubtitle"] = "Every app & website is unlocked automatically at full native internet speed.",
            ["Status_ActiveCta"] = "Pause RyanDPI",
            ["Status_ActiveCaption"] = "Running in background as a Windows service — stays active automatically after every restart.",
            ["Status_PartialHeadline"] = "RyanDPI Partially Active",
            ["Status_PartialSubtitle"] = "One of the background services is stopped. Click below to start both cleanly.",
            ["Status_PausedHeadline"] = "RyanDPI is Paused",
            ["Status_PausedSubtitle"] = "RyanDPI is installed and ready. Click below to re-enable instant access.",
            ["Status_PausedCta"] = "Enable RyanDPI",
            ["Status_PausedCaption"] = "Enables local DPI bypass and system-wide transparent app routing with zero VPN slowdown.",
            ["Status_ReadyHeadline"] = "Ready to Unlock Everything",
            ["Status_ReadySubtitle"] = "One-click setup unlocks Discord, Roblox, Roblox Studio & every blocked app/site in Turkey.",
            ["Status_ReadyLegacySubtitle"] = "Legacy DPI detected ({0}). RyanDPI will replace it cleanly.",
            ["Status_ReadyCta"] = "Install & Enable RyanDPI",
            ["Status_ReadyCaption"] = "100% local DPI bypass — no VPN servers, no bandwidth throttling, no app whitelisting needed.",
            ["Btn_Reinstall"] = "Reinstall / Repair",
            ["Btn_Uninstall"] = "Uninstall",
            ["Btn_RestartPc"] = "Restart PC",

            // Self-Test Card
            ["SelfTest_Title"] = "Unblock & Speed Self-Test",
            ["SelfTest_Subtitle"] = "Verify Discord, Roblox App, Roblox Studio, Wattpad & live ping right now.",
            ["SelfTest_Run"] = "Run Test",
            ["SelfTest_Testing"] = "Testing...",
            ["SelfTest_Placeholder"] = "Click 'Run Test' to verify live access and response latency.",
            ["SelfTest_Failed"] = "Failed",
            ["SelfTest_LocalEngine"] = "RyanDPI Local Engine",
            ["SelfTest_LocalEngineActive"] = "Active on 127.0.0.1:{0} (All Apps)",
            ["SelfTest_LocalEngineLegacy"] = "Legacy proxy on :1080 — click Install RyanDPI to upgrade",
            ["SelfTest_LocalEngineStopped"] = "RyanDPI Engine is not running",
            ["SelfTest_LocalRouterStopped"] = "RyanDPI Router (ProxiFyreService) is not running",
            ["SelfTest_LocalPortUnreachable"] = "Port 127.0.0.1:{0} unreachable",
            ["SelfTest_Dns"] = "DNS Resolution",
            ["SelfTest_DnsClean"] = "Clean Encrypted DNS (Discord & Roblox resolved)",
            ["SelfTest_DnsEmpty"] = "DNS lookup returned no addresses",
            ["SelfTest_DnsFailed"] = "DNS failed: {0}",
            ["SelfTest_Discord"] = "Discord",
            ["SelfTest_RobloxApp"] = "Roblox App (Player)",
            ["SelfTest_RobloxStudio"] = "Roblox Studio API",
            ["SelfTest_RobloxWeb"] = "Roblox Web",
            ["SelfTest_Wattpad"] = "Wattpad",
            ["SelfTest_DirectBaseline"] = "Direct Speed Baseline",
            ["SelfTest_UnlockedFmt"] = "Unlocked (HTTP {0} • Cold {1} ms)",
            ["SelfTest_UnexpectedHttp"] = "Unexpected HTTP {0}",
            ["SelfTest_BlockedFmt"] = "Blocked / Timeout ({0})",

            // Settings Page
            ["Settings_ProfileTitle"] = "DPI Bypass Profile",
            ["Profile_AutoUniversal"] = "Auto Universal (Recommended — All Turkish ISPs)",
            ["Profile_Classic"] = "Classic Mode (Lightweight TLS Record Split)",
            ["Profile_StrictIsp"] = "Strict ISP (Superonline / Mobile / Aggressive DPI)",
            ["Profile_Custom"] = "Custom Arguments",
            ["Profile_Desc_Auto"] = "6-stage zero-retransmit adaptive strategy built for all Turkish ISPs (Türk Telekom, Superonline, Vodafone, Kablonet, TurkNet) with 128 KB full-speed buffers and 24-hour per-IP caching.",
            ["Profile_Desc_Classic"] = "Uses the lightweight zero-retransmit TLS record split strategy (--tlsrec 1+s --mod-http=h,d) with automatic split/disorder fallback and 128 KB full-speed buffers.",
            ["Profile_Desc_Strict"] = "Starts immediately with TLS record + TCP split, then escalates to OOB, disorder, and low-TTL fake packets for aggressive ISPs like Superonline or mobile carriers.",
            ["Profile_Desc_Custom"] = "Enter custom ciadpi.exe desync flags below. High-speed socket flags (-i 127.0.0.1 -p 1080 -c 4096 -b 131072) are prepended automatically.",
            ["Settings_CustomArgsLabel"] = "Custom ciadpi.exe strategy arguments:",

            ["Settings_DnsTitle"] = "DNS Protection",
            ["Dns_Encrypted"] = "Encrypted DNS — DNSCrypt-Proxy (Recommended)",
            ["Dns_NativeDoH"] = "Windows Native DoH (Google + Quad9)",
            ["Dns_KeepSystem"] = "Keep My Existing DNS Settings",
            ["Dns_Desc_Encrypted"] = "Runs local encrypted DNS (DNSCrypt-Proxy) via Cloudflare, Google & Quad9 (fastest-first) with an 8,192-entry cache. Defeats ISP DNS poisoning on both Windows 10 and 11.",
            ["Dns_Desc_NativeDoH"] = "Uses Windows 11 built-in DNS-over-HTTPS with Google (8.8.8.8) and Quad9 (9.9.9.9) without running a local DNS proxy.",
            ["Dns_Desc_KeepSystem"] = "Leaves your network adapter DNS untouched. Choose this if you already manage your own encrypted DNS (e.g. NextDNS, AdGuard, or router DoH).",

            ["Settings_NetworkTitle"] = "Network & Speed Options",
            ["Settings_BypassLanTitle"] = "Bypass LAN & Localhost",
            ["Settings_BypassLanDesc"] = "Keep local devices, routers, and localhost dev servers completely direct.",
            ["Settings_TunnelUdpTitle"] = "Also Tunnel UDP (Voice / QUIC)",
            ["Settings_TunnelUdpDesc"] = "Keep OFF for 0ms native gaming ping. Turn ON only if your ISP blocks Discord voice or QUIC.",
            ["Settings_AutoUpdateTitle"] = "Auto-Update on Startup",
            ["Settings_AutoUpdateDesc"] = "Automatically download, verify, and install new RyanDPI releases when the app opens.",

            ["Settings_ExcludesTitle"] = "Excluded Apps (Optional)",
            ["Settings_ExcludesDesc"] = "RyanDPI unlocks every app on Windows by default. Only add apps here if you want them to skip RyanDPI. Engine and DNS processes are always excluded automatically.",
            ["Settings_ExcludeTooltip"] = "Enter an executable name (e.g. game.exe)",
            ["Btn_Add"] = "Add",
            ["Btn_Browse"] = "Browse...",
            ["Settings_NoExcludes"] = "No custom app exclusions configured — all user apps are unlocked.",
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
            ["About_TitleFmt"] = "RyanDPI v{0} — System-Wide Zero-Speed-Loss DPI Bypass",
            ["About_Subtitle"] = "100% local DPI desynchronization (no remote VPN servers). Built with ByeDPI, ProxiFyre & DNSCrypt-Proxy.",
            ["Btn_CheckUpdates"] = "Check Updates",
            ["Btn_Checking"] = "Checking...",
            ["Btn_Updating"] = "Updating...",

            // Install / Uninstall / Engine Progress & Messages
            ["Progress_InstallingTitle"] = "Installing RyanDPI...",
            ["Progress_Starting"] = "Starting...",
            ["Progress_Initializing"] = "Initializing installation...",
            ["Progress_StepFmt"] = "Step {0} / {1}",
            ["Step1_Title"] = "Preparing system",
            ["Step1_Detail"] = "Stopping and removing any conflicting legacy DPI or VPN services...",
            ["Step2_Title"] = "Checking prerequisites",
            ["Step2_Detail"] = "Verifying Windows Packet Filter driver (NDISRD) and VC++ Runtime...",
            ["Step3_Title"] = "Deploying RyanDPI engine",
            ["Step3_Detail"] = "Installing system-wide router, full-speed engine, and protected ACL security...",
            ["Step4_Title"] = "Configuring Encrypted DNS & Firewall",
            ["Step4_Detail"] = "Eliminating ISP DNS poisoning and adding Windows Firewall allow rules...",
            ["Step5_Title"] = "Starting RyanDPI",
            ["Step5_Detail"] = "Launching local DPI desync engine and system-wide transparent router...",
            ["Progress_UninstallingTitle"] = "Uninstalling RyanDPI",
            ["Progress_CleaningUp"] = "Cleaning up...",
            ["Progress_UninstallingDetail"] = "Stopping services, removing firewall rules, and restoring automatic DNS...",

            ["Msg_NdisFailed"] = "Failed to install the Windows Packet Filter driver (NDISRD). Check the Logs tab for details.",
            ["Msg_StageFailed"] = "Failed to deploy RyanDPI engine files. Make sure no antivirus quarantined the files.",
            ["Msg_EngineStartFailed"] = "RyanDPI engine service could not start on 127.0.0.1:1080. Check if another app is using port 1080 or check the Logs tab.",
            ["Msg_RebootNeeded"] = "RyanDPI is installed! A quick PC restart is needed so Windows can finish loading the new Packet Filter driver.",
            ["Msg_RouterStartFailed"] = "RyanDPI router service (ProxiFyreService) failed to start. Try restarting your PC or check the Logs tab.",
            ["Msg_InstallSuccess"] = "RyanDPI is active! All apps and websites are unlocked at native internet speed. ({0})",
            ["Msg_InstallError"] = "Installation error: {0}",
            ["Msg_Started"] = "RyanDPI is now active.",
            ["Msg_StartEngineFail"] = "RyanDPI engine failed to start on 127.0.0.1:1080. Check Logs for details.",
            ["Msg_StartRouterFail"] = "RyanDPI Router (ProxiFyreService) could not start. Check Logs for details.",
            ["Msg_Paused"] = "RyanDPI paused.",
            ["Msg_SettingsSavedNotInstalled"] = "Settings saved! Click 'Install & Enable RyanDPI' on Home to activate them.",
            ["Msg_SettingsApplyFailed"] = "Failed to apply settings: {0}",
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
            ["Dlg_AdminRequiredBody"] = "RyanDPI requires Administrator privileges to manage Windows network services and encrypted DNS.\n\nPlease right-click RyanDPI.exe and select 'Run as administrator'.",
            ["Dlg_ErrorTitle"] = "RyanDPI Error",
            ["Dlg_ErrorBody"] = "An unexpected error occurred:\n{0}",
            ["Dlg_CriticalTitle"] = "RyanDPI Critical Error",
            ["Dlg_CriticalBody"] = "A critical error occurred:\n{0}"
        };

        private static readonly Dictionary<string, string> Tr = new()
        {
            // Title bar & Nav
            ["Badge_AllAppsSpeed"] = "TÜM UYGULAMALAR • TAM HIZ",
            ["LangButton_Text"] = "EN",
            ["LangButton_Tooltip"] = "Switch language to English (İngilizce'ye geç)",
            ["ThemeButton_Tooltip"] = "Koyu / Açık Tema Değiştir",
            ["Minimize_Tooltip"] = "Küçült",
            ["Close_Tooltip"] = "Kapat",
            ["Refresh_Tooltip"] = "Durumu Yenile",
            ["Nav_Home"] = "Ana Sayfa",
            ["Nav_Settings"] = "Ayarlar",
            ["Nav_Tools"] = "Araçlar ve Günlük",

            // Health Strip
            ["Pill_EngineTitle"] = "DPI MOTORU",
            ["Pill_RouterTitle"] = "UYG. YÖNLENDİRİCİ",
            ["Pill_DnsTitle"] = "GÜVENLİ DNS",
            ["State_Checking"] = "Kontrol...",
            ["State_Running"] = "Çalışıyor",
            ["State_Stopped"] = "Durduruldu",
            ["State_NotSet"] = "Kurulu Değil",
            ["State_AllApps"] = "Tüm Uyg.",
            ["State_Paused"] = "Duraklatıldı",
            ["State_Encrypted"] = "Şifreli",
            ["State_NativeDoH"] = "Yerel DoH",
            ["State_Custom"] = "Özel",
            ["State_Default"] = "Varsayılan",

            // Hero Status
            ["Status_CheckingHeadline"] = "Durum kontrol ediliyor...",
            ["Status_CheckingSubtitle"] = "Windows hizmetleri ve şifreli DNS taranıyor...",
            ["Status_ActiveHeadline"] = "RyanDPI Aktif",
            ["Status_ActiveSubtitle"] = "Tüm uygulamalar ve web siteleri internet hızın düşmeden otomatik olarak açılıyor.",
            ["Status_ActiveCta"] = "RyanDPI'yı Duraklat",
            ["Status_ActiveCaption"] = "Arka planda Windows hizmeti olarak çalışır — her yeniden başlatmada otomatik aktif kalır.",
            ["Status_PartialHeadline"] = "RyanDPI Kısmen Aktif",
            ["Status_PartialSubtitle"] = "Arka plan hizmetlerinden biri durdurulmuş. İkisini birden başlatmak için aşağıya tıkla.",
            ["Status_PausedHeadline"] = "RyanDPI Duraklatıldı",
            ["Status_PausedSubtitle"] = "RyanDPI kurulu ve hazır. Anında erişimi tekrar açmak için aşağıya tıkla.",
            ["Status_PausedCta"] = "RyanDPI'yı Etkinleştir",
            ["Status_PausedCaption"] = "VPN yavaşlaması olmadan yerel DPI aşımını ve sistem geneli uygulama yönlendirmeyi açar.",
            ["Status_ReadyHeadline"] = "Her Şeyi Açmaya Hazır",
            ["Status_ReadySubtitle"] = "Tek tıkla kurulum Discord, Roblox, Roblox Studio ve Türkiye'de engelli tüm uygulama/siteleri açar.",
            ["Status_ReadyLegacySubtitle"] = "Eski DPI aracı tespit edildi ({0}). RyanDPI bunu temizce değiştirecek.",
            ["Status_ReadyCta"] = "RyanDPI'yı Kur ve Etkinleştir",
            ["Status_ReadyCaption"] = "%100 yerel DPI aşımı — VPN sunucusu yok, hız kaybı yok, uygulama beyaz listesi gerekmez.",
            ["Btn_Reinstall"] = "Yeniden Kur / Onar",
            ["Btn_Uninstall"] = "Kaldır",
            ["Btn_RestartPc"] = "PC'yi Yeniden Başlat",

            // Self-Test Card
            ["SelfTest_Title"] = "Erişim ve Hız Testi",
            ["SelfTest_Subtitle"] = "Discord, Roblox Uygulaması, Roblox Studio, Wattpad ve anlık ping değerini şimdi doğrula.",
            ["SelfTest_Run"] = "Testi Çalıştır",
            ["SelfTest_Testing"] = "Test Ediliyor...",
            ["SelfTest_Placeholder"] = "Canlı erişim ve tepki süresini doğrulamak için 'Testi Çalıştır'a tıkla.",
            ["SelfTest_Failed"] = "Başarısız",
            ["SelfTest_LocalEngine"] = "RyanDPI Yerel Motoru",
            ["SelfTest_LocalEngineActive"] = "127.0.0.1:{0} üzerinde aktif (Tüm Uygulamalar)",
            ["SelfTest_LocalEngineLegacy"] = ":1080 portunda eski proxy var — yükseltmek için RyanDPI'yı Kur'a tıkla",
            ["SelfTest_LocalEngineStopped"] = "RyanDPI Motoru çalışmıyor",
            ["SelfTest_LocalRouterStopped"] = "RyanDPI Yönlendirici (ProxiFyreService) çalışmıyor",
            ["SelfTest_LocalPortUnreachable"] = "127.0.0.1:{0} portuna ulaşılamıyor",
            ["SelfTest_Dns"] = "DNS Çözümleme",
            ["SelfTest_DnsClean"] = "Temiz Şifreli DNS (Discord ve Roblox çözümlendi)",
            ["SelfTest_DnsEmpty"] = "DNS sorgusu adres döndürmedi",
            ["SelfTest_DnsFailed"] = "DNS hatası: {0}",
            ["SelfTest_Discord"] = "Discord",
            ["SelfTest_RobloxApp"] = "Roblox Uygulaması (Player)",
            ["SelfTest_RobloxStudio"] = "Roblox Studio API",
            ["SelfTest_RobloxWeb"] = "Roblox Web",
            ["SelfTest_Wattpad"] = "Wattpad",
            ["SelfTest_DirectBaseline"] = "Doğrudan Hız Referansı",
            ["SelfTest_UnlockedFmt"] = "Erişim Açık (HTTP {0} • İlk Bağlantı {1} ms)",
            ["SelfTest_UnexpectedHttp"] = "Beklenmeyen HTTP {0}",
            ["SelfTest_BlockedFmt"] = "Engelli / Zaman Aşımı ({0})",

            // Settings Page
            ["Settings_ProfileTitle"] = "DPI Aşım Profili",
            ["Profile_AutoUniversal"] = "Otomatik Evrensel (Önerilen — Tüm Türk İSS'leri)",
            ["Profile_Classic"] = "Klasik Mod (Hafif TLS Kayıt Bölme)",
            ["Profile_StrictIsp"] = "Sıkı İSS (Superonline / Mobil / Agresif DPI)",
            ["Profile_Custom"] = "Özel Parametreler",
            ["Profile_Desc_Auto"] = "Tüm Türk İSS'leri (Türk Telekom, Superonline, Vodafone, Kablonet, TurkNet) için 128 KB tam hızlı tamponlar ve 24 saatlik IP önbelleğiyle sıfır gecikmeli 6 aşamalı uyarlanabilir strateji.",
            ["Profile_Desc_Classic"] = "Otomatik split/disorder yedeği ve 128 KB tam hızlı tamponlarla hafif, sıfır gecikmeli TLS kayıt bölme stratejisini (--tlsrec 1+s --mod-http=h,d) kullanır.",
            ["Profile_Desc_Strict"] = "Doğrudan TLS kayıt + TCP bölme ile başlar, Superonline veya mobil hatlar gibi agresif İSS'ler için OOB, disorder ve düşük TTL sahte paketlere otomatik geçer.",
            ["Profile_Desc_Custom"] = "Aşağıya özel ciadpi.exe parametrelerini gir. Yüksek hızlı soket ayarları (-i 127.0.0.1 -p 1080 -c 4096 -b 131072) otomatik eklenir.",
            ["Settings_CustomArgsLabel"] = "Özel ciadpi.exe strateji parametreleri:",

            ["Settings_DnsTitle"] = "DNS Koruması",
            ["Dns_Encrypted"] = "Şifreli DNS — DNSCrypt-Proxy (Önerilen)",
            ["Dns_NativeDoH"] = "Windows Yerel DoH (Google + Quad9)",
            ["Dns_KeepSystem"] = "Mevcut DNS Ayarlarımı Koru",
            ["Dns_Desc_Encrypted"] = "8.192 kayıtlık önbellekle Cloudflare, Google ve Quad9 üzerinden (en hızlı sunucu öncelikli) yerel şifreli DNS çalıştırır. Windows 10 ve 11'de İSS DNS engellemesini tamamen aşar.",
            ["Dns_Desc_NativeDoH"] = "Yerel DNS proxy çalıştırmadan Windows 11'in yerleşik DNS-over-HTTPS özelliğini Google (8.8.8.8) ve Quad9 (9.9.9.9) ile kullanır.",
            ["Dns_Desc_KeepSystem"] = "Ağ bağdaştırıcının DNS ayarlarına dokunmaz. Kendi şifreli DNS'ini (NextDNS, AdGuard veya modem DoH gibi) kullanıyorsan bunu seç.",

            ["Settings_NetworkTitle"] = "Ağ ve Hız Seçenekleri",
            ["Settings_BypassLanTitle"] = "Yerel Ağ (LAN) ve Localhost'u Atla",
            ["Settings_BypassLanDesc"] = "Yerel cihazları, modem arayüzünü ve localhost geliştirici sunucularını tamamen doğrudan tutar.",
            ["Settings_TunnelUdpTitle"] = "UDP'yi de Tünelle (Ses / QUIC)",
            ["Settings_TunnelUdpDesc"] = "0 ms doğal oyun pingi için KAPALI tut. Sadece İSS'n Discord sesini veya QUIC'i engelliyorsa AÇ.",
            ["Settings_AutoUpdateTitle"] = "Başlangıçta Otomatik Güncelle",
            ["Settings_AutoUpdateDesc"] = "Uygulama açıldığında yeni RyanDPI sürümlerini GitHub'dan otomatik indirip doğrulayarak kurar.",

            ["Settings_ExcludesTitle"] = "Hariç Tutulan Uygulamalar (İsteğe Bağlı)",
            ["Settings_ExcludesDesc"] = "RyanDPI varsayılan olarak Windows'taki her uygulamayı açar. Sadece RyanDPI'dan hariç tutmak istediğin uygulamaları ekle. Motor ve DNS süreçleri her zaman otomatik hariç tutulur.",
            ["Settings_ExcludeTooltip"] = "Uygulama adı girin (ör. oyun.exe)",
            ["Btn_Add"] = "Ekle",
            ["Btn_Browse"] = "Gözat...",
            ["Settings_NoExcludes"] = "Özel hariç tutma ayarlanmadı — tüm kullanıcı uygulamalarının engeli açık.",
            ["Btn_SaveApply"] = "Ayarları Kaydet ve Uygula",
            ["Btn_Applying"] = "Uygulanıyor...",
            ["BrowseExclude_Title"] = "RyanDPI'dan hariç tutulacak uygulamayı seç",
            ["BrowseExclude_Filter"] = "Çalıştırılabilir dosyalar (*.exe)|*.exe|Tüm dosyalar (*.*)|*.*",

            // Tools & Logs Page
            ["Tools_Title"] = "Sistem Araçları",
            ["Btn_ResetDns"] = "DNS Sıfırla ve Önbelleği Temizle",
            ["Btn_CleanLegacy"] = "Eski DPI Hizmetlerini Temizle",
            ["Btn_CleanLegacyCount"] = "Eski DPI Temizle ({0})",
            ["Btn_OpenLogs"] = "Günlük Klasörünü Aç",
            ["Btn_UninstallFull"] = "RyanDPI'yı Kaldır",
            ["Logs_Title"] = "Canlı Etkinlik Günlüğü",
            ["Btn_Copy"] = "Kopyala",
            ["Btn_Clear"] = "Temizle",
            ["About_TitleFmt"] = "RyanDPI v{0} — Sistem Geneli Sıfır Hız Kayıplı DPI Aşımı",
            ["About_Subtitle"] = "%100 yerel DPI paket ayrıştırma (uzak VPN sunucusu yok). ByeDPI, ProxiFyre ve DNSCrypt-Proxy ile geliştirildi.",
            ["Btn_CheckUpdates"] = "Güncelleme Denetle",
            ["Btn_Checking"] = "Kontrol...",
            ["Btn_Updating"] = "Güncelleniyor...",

            // Install / Uninstall / Engine Progress & Messages
            ["Progress_InstallingTitle"] = "RyanDPI Kuruluyor...",
            ["Progress_Starting"] = "Başlatılıyor...",
            ["Progress_Initializing"] = "Kurulum hazırlanıyor...",
            ["Progress_StepFmt"] = "Adım {0} / {1}",
            ["Step1_Title"] = "Sistem hazırlanıyor",
            ["Step1_Detail"] = "Çakışan eski DPI veya VPN hizmetleri durdurulup kaldırılıyor...",
            ["Step2_Title"] = "Gereksinimler kontrol ediliyor",
            ["Step2_Detail"] = "Windows Packet Filter sürücüsü (NDISRD) ve VC++ Runtime doğrulanıyor...",
            ["Step3_Title"] = "RyanDPI motoru yerleştiriliyor",
            ["Step3_Detail"] = "Sistem geneli yönlendirici, tam hızlı motor ve ACL güvenlik izinleri kuruluyor...",
            ["Step4_Title"] = "Şifreli DNS ve Güvenlik Duvarı ayarlanıyor",
            ["Step4_Detail"] = "İSS DNS zehirlenmesi engelleniyor ve Windows Güvenlik Duvarı kuralları ekleniyor...",
            ["Step5_Title"] = "RyanDPI başlatılıyor",
            ["Step5_Detail"] = "Yerel DPI motoru ve sistem geneli şeffaf yönlendirici başlatılıyor...",
            ["Progress_UninstallingTitle"] = "RyanDPI Kaldırılıyor",
            ["Progress_CleaningUp"] = "Temizleniyor...",
            ["Progress_UninstallingDetail"] = "Hizmetler durduruluyor, güvenlik duvarı kuralları kaldırılıyor ve otomatik DNS geri yükleniyor...",

            ["Msg_NdisFailed"] = "Windows Packet Filter sürücüsü (NDISRD) kurulamadı. Ayrıntılar için Günlük sekmesini kontrol et.",
            ["Msg_StageFailed"] = "RyanDPI motor dosyaları kopyalanamadı. Antivirüs programının dosyaları karantinaya almadığından emin ol.",
            ["Msg_EngineStartFailed"] = "RyanDPI motor hizmeti 127.0.0.1:1080 üzerinde başlatılamadı. 1080 portunu kullanan başka bir uygulama olup olmadığını kontrol et.",
            ["Msg_RebootNeeded"] = "RyanDPI kuruldu! Windows'un yeni Paket Filtre sürücüsünü yüklemesi için bilgisayarını bir kez yeniden başlatman gerekiyor.",
            ["Msg_RouterStartFailed"] = "RyanDPI yönlendirici hizmeti (ProxiFyreService) başlatılamadı. PC'yi yeniden başlatmayı dene veya Günlük sekmesine bak.",
            ["Msg_InstallSuccess"] = "RyanDPI aktif! Tüm uygulamalar ve web siteleri doğal internet hızında açıldı. ({0})",
            ["Msg_InstallError"] = "Kurulum hatası: {0}",
            ["Msg_Started"] = "RyanDPI artık aktif.",
            ["Msg_StartEngineFail"] = "RyanDPI motoru 127.0.0.1:1080 üzerinde başlatılamadı. Ayrıntılar için Günlük sekmesine bak.",
            ["Msg_StartRouterFail"] = "RyanDPI Yönlendirici (ProxiFyreService) başlatılamadı. Ayrıntılar için Günlük sekmesine bak.",
            ["Msg_Paused"] = "RyanDPI duraklatıldı.",
            ["Msg_SettingsSavedNotInstalled"] = "Ayarlar kaydedildi! Etkinleştirmek için Ana Sayfa'dan 'RyanDPI'yı Kur ve Etkinleştir'e tıkla.",
            ["Msg_SettingsApplyFailed"] = "Ayarlar uygulanamadı: {0}",
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

            // Auto-Updater
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

            // Confirmation & Startup Dialogs
            ["Confirm_UninstallTitle"] = "RyanDPI'yı Kaldır",
            ["Confirm_UninstallBody"] = "RyanDPI hizmetleri, güvenlik duvarı kuralları tamamen kaldırılsın ve DNS ayarları Otomatik (DHCP) olarak sıfırlansın mı?",
            ["Confirm_RebootTitle"] = "PC'yi Yeniden Başlat",
            ["Confirm_RebootBody"] = "Bilgisayarın şimdi yeniden başlatılsın mı? Açık çalışmalarını kaydettiğinden emin ol.",
            ["Confirm_ResetDnsTitle"] = "DNS Sıfırla ve Önbelleği Temizle",
            ["Confirm_ResetDnsBody"] = "Tüm fiziksel ağ bağdaştırıcılarındaki DNS ve DoH ayarları Otomatik (DHCP) olarak sıfırlansın ve DNS önbelleği temizlensin mi?",
            ["Confirm_CleanLegacyTitle"] = "Eski DPI Hizmetlerini Temizle",
            ["Confirm_CleanLegacyBody"] = "Çakışan üçüncü parti DPI veya tünel hizmetleri (ByeDPI, GoodbyeDPI, Zapret, WinDivert, WireSock) ve eski Discord drover kalıntıları kaldırılsın mı?\n\nAktif RyanDPI kurulumun etkilenmeyecektir.",
            ["Dlg_AlreadyRunningTitle"] = "RyanDPI Zaten Çalışıyor",
            ["Dlg_AlreadyRunningBody"] = "RyanDPI zaten çalışıyor. Görev çubuğunu kontrol et veya Görev Yöneticisi'nden RyanDPI.exe işlemini kapat.",
            ["Dlg_AdminRequiredTitle"] = "Yönetici Yetkisi Gerekli",
            ["Dlg_AdminRequiredBody"] = "RyanDPI'nın Windows ağ hizmetlerini ve şifreli DNS'i yönetebilmesi için Yönetici yetkisi gerekir.\n\nLütfen RyanDPI.exe dosyasına sağ tıklayıp 'Yönetici olarak çalıştır'ı seç.",
            ["Dlg_ErrorTitle"] = "RyanDPI Hatası",
            ["Dlg_ErrorBody"] = "Beklenmeyen bir hata oluştu:\n{0}",
            ["Dlg_CriticalTitle"] = "RyanDPI Kritik Hata",
            ["Dlg_CriticalBody"] = "Kritik bir hata oluştu:\n{0}"
        };
    }
}
