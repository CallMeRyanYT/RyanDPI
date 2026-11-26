package com.ryandpi.android.engine

import android.content.Context
import android.content.SharedPreferences
import java.util.Locale

enum class RegionPreset {
    GLOBAL_UNIVERSAL,
    TURKEY,
    RUSSIA,
    BRAZIL
}

enum class BrazilTunnelMode {
    BUILTIN_WARP_MASQUE,
    CUSTOM_SOCKS5
}

enum class DnsMode {
    ENCRYPTED_DOH,
    DIRECT_NATIVE_DNS
}

enum class DohProvider(val displayName: String, val endpointUrl: String, val plainIp: String) {
    CLOUDFLARE("Cloudflare (1.1.1.1)", "https://1.1.1.1/dns-query", "1.1.1.1"),
    GOOGLE("Google (8.8.8.8)", "https://8.8.8.8/dns-query", "8.8.8.8"),
    QUAD9("Quad9 (9.9.9.9)", "https://9.9.9.9/dns-query", "9.9.9.9"),
    ADGUARD("AdGuard (94.140.14.14)", "https://94.140.14.14/dns-query", "94.140.14.14")
}

enum class BypassProfile {
    AUTO_UNIVERSAL,
    RUSSIA_TSPU,
    CLASSIC_BYEDPI,
    STRICT_ISP,
    CUSTOM
}

data class ParsedSocksEndpoint(val host: String, val port: Int)

data class RyanDpiSettings(
    val isDarkTheme: Boolean = true,
    val language: String = "en",
    val regionConfigured: Boolean = false,
    val region: RegionPreset = RegionPreset.GLOBAL_UNIVERSAL,
    val brazilTunnel: BrazilTunnelMode = BrazilTunnelMode.BUILTIN_WARP_MASQUE,
    val brazilIncludeBrowsers: Boolean = false,
    val customSocksEndpoint: String = "",
    val customSocksUsername: String = "",
    val customSocksPassword: String = "",
    val dnsMode: DnsMode = DnsMode.ENCRYPTED_DOH,
    val dohProvider: DohProvider = DohProvider.CLOUDFLARE,
    val bypassProfile: BypassProfile = BypassProfile.AUTO_UNIVERSAL,
    val customStrategyArgs: String = AUTO_UNIVERSAL_STRATEGY,
    val udpBypass: Boolean = true,
    val ipv6Enabled: Boolean = false,
    val autoStartOnBoot: Boolean = false,
    val autoCheckUpdates: Boolean = true,
    val excludedPackages: Set<String> = emptySet()
) {
    companion object {
        const val GATEWAY_PORT = 1080
        const val CIADPI_INTERNAL_PORT = 1081
        const val USQUE_SOCKS_PORT = 1082
        const val VIRTUAL_TUN_IPV4 = "10.10.10.1"
        const val VIRTUAL_DNS_IPV4 = "10.10.10.2"
        const val VIRTUAL_TUN_IPV6 = "fd00::1"
        const val TUN_MTU = 8500

        val SUPPORTED_LANGUAGES = listOf("en", "tr", "pt", "ru", "fa", "id", "zh", "ar")

        // 7-stage Global Zero-Lag strategy (matches RyanDPI Windows v3.0.0 Unbound):
        // Stage 1 (--tlsrec 1+s) gives 0ms added lag in Turkey / light DPI, while Stages 2-6
        // auto-adapt for Russia TSPU (YouTube/Discord/Instagram), Superonline & mobile ISPs.
        const val AUTO_UNIVERSAL_STRATEGY =
            "--tlsrec 1+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --tlsrec 1+s --split 1 --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --split 2 --disorder -1+s --tlsrec 1+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
                "--auto=torst,redirect,ssl_err --fake -1 --ttl 5 --fake-sni www.google.com --disorder 3+s " +
                "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err"

        const val RUSSIA_TSPU_STRATEGY =
            "--split 2 --disorder -1+s --tlsrec 1+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
                "--auto=torst,redirect,ssl_err --fake -1 --ttl 5 --fake-sni www.google.com --disorder 3+s " +
                "--auto=torst,redirect,ssl_err --tlsrec 1+s --split 1 --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err"

        const val CLASSIC_BYEDPI_STRATEGY =
            "--tlsrec 1+s --mod-http=h,d --auto=torst --split 1 --disorder 3+s"

        const val STRICT_ISP_STRATEGY =
            "--tlsrec 1+s --split 1 --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
                "--auto=torst,redirect,ssl_err --fake -1 --ttl 5 --fake-sni www.google.com --disorder 3+s " +
                "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err"

        private const val PREFS_NAME = "ryandpi_settings"

        fun normalizeLanguage(code: String?): String {
            val raw = code?.trim()?.lowercase(Locale.ROOT).orEmpty()
            return when {
                raw.startsWith("tr") -> "tr"
                raw.startsWith("pt") -> "pt"
                raw.startsWith("ru") -> "ru"
                raw.startsWith("fa") || raw.startsWith("pr") -> "fa"
                raw.startsWith("id") || raw.startsWith("in") -> "id"
                raw.startsWith("zh") || raw.startsWith("cn") -> "zh"
                raw.startsWith("ar") -> "ar"
                else -> "en"
            }
        }

        fun nextLanguage(current: String): String {
            val norm = normalizeLanguage(current)
            val idx = SUPPORTED_LANGUAGES.indexOf(norm).coerceAtLeast(0)
            return SUPPORTED_LANGUAGES[(idx + 1) % SUPPORTED_LANGUAGES.size]
        }

        fun parseSocksEndpoint(raw: String?): ParsedSocksEndpoint? {
            if (raw.isNullOrBlank()) return null
            val cleaned = raw.trim()
                .removePrefix("socks5://")
                .removePrefix("socks5h://")
                .trimEnd('/')
            val lastColon = cleaned.lastIndexOf(':')
            if (lastColon <= 0 || lastColon >= cleaned.length - 1) return null
            val host = cleaned.substring(0, lastColon).trim().trim('[', ']')
            val port = cleaned.substring(lastColon + 1).trim().toIntOrNull() ?: return null
            if (host.isBlank() || port !in 1..65535) return null
            return ParsedSocksEndpoint(host, port)
        }

        fun load(context: Context): RyanDpiSettings {
            val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            val defaultLang = normalizeLanguage(Locale.getDefault().language)
            val regionName = prefs.getString("region_preset", RegionPreset.GLOBAL_UNIVERSAL.name)
            val brTunnelName = prefs.getString("brazil_tunnel", BrazilTunnelMode.BUILTIN_WARP_MASQUE.name)
            val dnsModeName = prefs.getString("dns_mode", DnsMode.ENCRYPTED_DOH.name)
            val dohName = prefs.getString("doh_provider", DohProvider.CLOUDFLARE.name)
            val profileName = prefs.getString("bypass_profile", BypassProfile.AUTO_UNIVERSAL.name)
            val rawCustom = prefs.getString("custom_strategy", AUTO_UNIVERSAL_STRATEGY)
                ?.takeIf { it.isNotBlank() } ?: AUTO_UNIVERSAL_STRATEGY

            return RyanDpiSettings(
                isDarkTheme = prefs.getBoolean("dark_theme", true),
                language = normalizeLanguage(prefs.getString("language", defaultLang)),
                regionConfigured = prefs.getBoolean("region_configured", false),
                region = runCatching { RegionPreset.valueOf(regionName ?: "") }.getOrDefault(RegionPreset.GLOBAL_UNIVERSAL),
                brazilTunnel = runCatching { BrazilTunnelMode.valueOf(brTunnelName ?: "") }.getOrDefault(BrazilTunnelMode.BUILTIN_WARP_MASQUE),
                brazilIncludeBrowsers = prefs.getBoolean("brazil_include_browsers", false),
                customSocksEndpoint = prefs.getString("custom_socks_endpoint", "") ?: "",
                customSocksUsername = prefs.getString("custom_socks_user", "") ?: "",
                customSocksPassword = prefs.getString("custom_socks_pass", "") ?: "",
                dnsMode = runCatching { DnsMode.valueOf(dnsModeName ?: "") }.getOrDefault(DnsMode.ENCRYPTED_DOH),
                dohProvider = runCatching { DohProvider.valueOf(dohName ?: "") }.getOrDefault(DohProvider.CLOUDFLARE),
                bypassProfile = runCatching { BypassProfile.valueOf(profileName ?: "") }.getOrDefault(BypassProfile.AUTO_UNIVERSAL),
                customStrategyArgs = rawCustom,
                udpBypass = prefs.getBoolean("udp_bypass", true),
                ipv6Enabled = prefs.getBoolean("ipv6_enabled", false),
                autoStartOnBoot = prefs.getBoolean("auto_start_boot", false),
                autoCheckUpdates = prefs.getBoolean("auto_check_updates", true),
                excludedPackages = prefs.getStringSet("excluded_packages", emptySet())?.toSet() ?: emptySet()
            )
        }

        fun save(context: Context, settings: RyanDpiSettings) {
            val prefs: SharedPreferences = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            prefs.edit()
                .putBoolean("dark_theme", settings.isDarkTheme)
                .putString("language", normalizeLanguage(settings.language))
                .putBoolean("region_configured", settings.regionConfigured)
                .putString("region_preset", settings.region.name)
                .putString("brazil_tunnel", settings.brazilTunnel.name)
                .putBoolean("brazil_include_browsers", settings.brazilIncludeBrowsers)
                .putString("custom_socks_endpoint", settings.customSocksEndpoint)
                .putString("custom_socks_user", settings.customSocksUsername)
                .putString("custom_socks_pass", settings.customSocksPassword)
                .putString("dns_mode", settings.dnsMode.name)
                .putString("doh_provider", settings.dohProvider.name)
                .putString("bypass_profile", settings.bypassProfile.name)
                .putString("custom_strategy", settings.customStrategyArgs)
                .putBoolean("udp_bypass", settings.udpBypass)
                .putBoolean("ipv6_enabled", settings.ipv6Enabled)
                .putBoolean("auto_start_boot", settings.autoStartOnBoot)
                .putBoolean("auto_check_updates", settings.autoCheckUpdates)
                .putStringSet("excluded_packages", settings.excludedPackages)
                .apply()
        }
    }

    fun withRegionPreset(selected: RegionPreset): RyanDpiSettings {
        return when (selected) {
            RegionPreset.TURKEY -> copy(
                region = selected,
                regionConfigured = true,
                bypassProfile = BypassProfile.AUTO_UNIVERSAL,
                dnsMode = DnsMode.ENCRYPTED_DOH,
                udpBypass = true
            )
            RegionPreset.RUSSIA -> copy(
                region = selected,
                regionConfigured = true,
                bypassProfile = BypassProfile.RUSSIA_TSPU,
                dnsMode = DnsMode.ENCRYPTED_DOH,
                udpBypass = true
            )
            RegionPreset.BRAZIL -> copy(
                region = selected,
                regionConfigured = true,
                dnsMode = DnsMode.DIRECT_NATIVE_DNS
            )
            RegionPreset.GLOBAL_UNIVERSAL -> copy(
                region = selected,
                regionConfigured = true,
                bypassProfile = BypassProfile.AUTO_UNIVERSAL,
                dnsMode = DnsMode.ENCRYPTED_DOH,
                udpBypass = true
            )
        }
    }

    fun activeCiadpiPort(): Int {
        return if (dnsMode == DnsMode.ENCRYPTED_DOH) CIADPI_INTERNAL_PORT else GATEWAY_PORT
    }

    fun activeStrategyString(): String {
        return when (bypassProfile) {
            BypassProfile.AUTO_UNIVERSAL -> AUTO_UNIVERSAL_STRATEGY
            BypassProfile.RUSSIA_TSPU -> RUSSIA_TSPU_STRATEGY
            BypassProfile.CLASSIC_BYEDPI -> CLASSIC_BYEDPI_STRATEGY
            BypassProfile.STRICT_ISP -> STRICT_ISP_STRATEGY
            BypassProfile.CUSTOM -> customStrategyArgs.takeIf { it.isNotBlank() } ?: AUTO_UNIVERSAL_STRATEGY
        }
    }

    fun buildCiadpiArgList(listenPort: Int = activeCiadpiPort()): List<String> {
        val args = mutableListOf(
            "-i", "127.0.0.1",
            "-p", listenPort.toString(),
            "-c", "2048",
            "-b", "131072",
            "-u", "86400",
            "-T", "2",
            "-L", "3"
        )
        if (udpBypass) {
            args.add("-a")
            args.add("1")
        }
        val tokens = activeStrategyString()
            .replace('\r', ' ')
            .replace('\n', ' ')
            .trim()
            .split(Regex("\\s+"))
            .filter { it.isNotBlank() }
        args.addAll(tokens)
        return args
    }

    fun orderedDohEndpoints(): List<String> {
        val primary = dohProvider.endpointUrl
        val fallbacks = listOf(
            DohProvider.CLOUDFLARE.endpointUrl,
            DohProvider.GOOGLE.endpointUrl,
            DohProvider.QUAD9.endpointUrl,
            DohProvider.ADGUARD.endpointUrl
        )
        return (listOf(primary) + fallbacks).distinct()
    }
}
