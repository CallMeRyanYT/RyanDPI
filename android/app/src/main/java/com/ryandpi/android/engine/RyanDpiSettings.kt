package com.ryandpi.android.engine

import android.content.Context
import android.content.SharedPreferences
import java.util.Locale

enum class DnsMode {
    ENCRYPTED_DOH,     // Built-in HTTPS DoH gateway (1.1.1.1 / 8.8.8.8 / 9.9.9.9) — defeats Turkish ISP UDP/53 blocks!
    DIRECT_NATIVE_DNS  // Direct C tunnel (hev -> ciadpi) using clean IP DNS / Android Private DNS
}

enum class DohProvider(val displayName: String, val endpointUrl: String, val plainIp: String) {
    CLOUDFLARE("Cloudflare (1.1.1.1)", "https://1.1.1.1/dns-query", "1.1.1.1"),
    GOOGLE("Google (8.8.8.8)", "https://8.8.8.8/dns-query", "8.8.8.8"),
    QUAD9("Quad9 (9.9.9.9)", "https://9.9.9.9/dns-query", "9.9.9.9"),
    ADGUARD("AdGuard (94.140.14.14)", "https://94.140.14.14/dns-query", "94.140.14.14")
}

enum class BypassProfile {
    AUTO_UNIVERSAL,
    CLASSIC_BYEDPI,
    STRICT_ISP,
    CUSTOM
}

data class RyanDpiSettings(
    val isDarkTheme: Boolean = true,
    val language: String = "en",
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
        const val VIRTUAL_TUN_IPV4 = "10.10.10.1"
        const val VIRTUAL_DNS_IPV4 = "10.10.10.2"
        const val VIRTUAL_TUN_IPV6 = "fd00::1"
        const val TUN_MTU = 8500

        // Exact zero-retransmit Stage-1 strategy matching RyanDPI Windows v2.3.2+:
        // Unlocks Discord, Roblox, Roblox Studio, Wattpad, and all Turkey-blocked services on the first packet
        // with 0ms added retransmit lag, plus 5 automatic ISP fallback stages.
        const val AUTO_UNIVERSAL_STRATEGY =
            "--tlsrec 1+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --tlsrec 1+s --split 1 --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
                "--auto=torst,redirect,ssl_err --fake -1 --ttl 5 --disorder 3+s " +
                "--auto=torst,redirect,ssl_err"

        const val CLASSIC_BYEDPI_STRATEGY =
            "--tlsrec 1+s --mod-http=h,d --auto=torst --split 1 --disorder 3+s"

        const val STRICT_ISP_STRATEGY =
            "--tlsrec 1+s --split 1 --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err --oob 1+s --tlsrec 1+s " +
                "--auto=torst,redirect,ssl_err --fake -1 --ttl 5 --disorder 3+s " +
                "--auto=torst,redirect,ssl_err --split 1 --disorder 3+s --mod-http=h,d " +
                "--auto=torst,redirect,ssl_err"

        private const val PREFS_NAME = "ryandpi_settings"

        fun normalizeLanguage(code: String?): String {
            return if (code != null && code.trim().lowercase(Locale.ROOT).startsWith("tr")) "tr" else "en"
        }

        fun load(context: Context): RyanDpiSettings {
            val prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            val defaultLang = normalizeLanguage(Locale.getDefault().language)
            val dnsModeName = prefs.getString("dns_mode", DnsMode.ENCRYPTED_DOH.name)
            val dohName = prefs.getString("doh_provider", DohProvider.CLOUDFLARE.name)
            val profileName = prefs.getString("bypass_profile", BypassProfile.AUTO_UNIVERSAL.name)
            val rawCustom = prefs.getString("custom_strategy", AUTO_UNIVERSAL_STRATEGY)
                ?.takeIf { it.isNotBlank() } ?: AUTO_UNIVERSAL_STRATEGY

            return RyanDpiSettings(
                isDarkTheme = prefs.getBoolean("dark_theme", true),
                language = normalizeLanguage(prefs.getString("language", defaultLang)),
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

    fun activeCiadpiPort(): Int {
        return if (dnsMode == DnsMode.ENCRYPTED_DOH) CIADPI_INTERNAL_PORT else GATEWAY_PORT
    }

    fun activeStrategyString(): String {
        return when (bypassProfile) {
            BypassProfile.AUTO_UNIVERSAL -> AUTO_UNIVERSAL_STRATEGY
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
