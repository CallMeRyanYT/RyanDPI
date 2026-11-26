package com.ryandpi.android.service

import android.app.AlarmManager
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.net.VpnService
import android.os.Build
import android.os.ParcelFileDescriptor
import android.os.PowerManager
import android.os.SystemClock
import android.service.quicksettings.TileService
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import com.ryandpi.android.MainActivity
import com.ryandpi.android.R
import com.ryandpi.android.engine.BrazilTunnelMode
import com.ryandpi.android.engine.CiadpiProcessManager
import com.ryandpi.android.engine.DnsMode
import com.ryandpi.android.engine.DohDnsResolver
import com.ryandpi.android.engine.LogRepository
import com.ryandpi.android.engine.RegionPreset
import com.ryandpi.android.engine.RyanDpiSettings
import com.ryandpi.android.engine.RyanDpiSocksGateway
import com.ryandpi.android.engine.SplitTunnelMode
import com.ryandpi.android.engine.UsqueProcessManager
import com.ryandpi.android.engine.VpnStateHolder
import com.ryandpi.android.engine.VpnStatus
import hev.htproxy.TProxyService
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.io.File
import java.util.Locale
import java.util.concurrent.atomic.AtomicBoolean

class RyanDpiVpnService : VpnService() {

    companion object {
        const val ACTION_START = "com.ryandpi.android.action.START"
        const val ACTION_STOP = "com.ryandpi.android.action.STOP"
        const val ACTION_RESTART = "com.ryandpi.android.action.RESTART"

        private const val NOTIFICATION_CHANNEL_ID = "ryandpi_vpn_tunnel"
        private const val NOTIFICATION_ID = 1080
        private const val STATS_INTERVAL_MS = 1500L

        private val DISCORD_PACKAGES = listOf(
            "com.discord",
            "com.discord.beta",
            "com.discord.alpha"
        )
        private val BROWSER_PACKAGES = listOf(
            "com.android.chrome",
            "org.mozilla.firefox",
            "com.brave.browser",
            "com.microsoft.emmx",
            "com.opera.browser",
            "com.opera.gx"
        )

        fun startService(context: Context) {
            RyanDpiSettings.setUserWantsConnected(context, true)
            val intent = Intent(context, RyanDpiVpnService::class.java).setAction(ACTION_START)
            ContextCompat.startForegroundService(context, intent)
        }

        fun stopService(context: Context) {
            RyanDpiSettings.setUserWantsConnected(context, false)
            val intent = Intent(context, RyanDpiVpnService::class.java).setAction(ACTION_STOP)
            context.startService(intent)
        }

        fun restartService(context: Context) {
            RyanDpiSettings.setUserWantsConnected(context, true)
            val intent = Intent(context, RyanDpiVpnService::class.java).setAction(ACTION_RESTART)
            ContextCompat.startForegroundService(context, intent)
        }

        fun formatSpeed(bytesPerSec: Long): String {
            if (bytesPerSec < 1024L) return "$bytesPerSec B/s"
            val kb = bytesPerSec / 1024.0
            if (kb < 1024.0) return String.format(Locale.US, "%.0f KB/s", kb)
            val mb = kb / 1024.0
            return String.format(Locale.US, "%.1f MB/s", mb)
        }

        fun formatCompactBytes(bytes: Long): String {
            if (bytes < 1024L) return "$bytes B"
            val kb = bytes / 1024.0
            if (kb < 1024.0) return String.format(Locale.US, "%.0f KB", kb)
            val mb = kb / 1024.0
            if (mb < 1024.0) return String.format(Locale.US, "%.1f MB", mb)
            return String.format(Locale.US, "%.2f GB", mb / 1024.0)
        }
    }

    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val lifecycleLock = Any()
    private val recoveringChild = AtomicBoolean(false)

    private var ciadpiManager: CiadpiProcessManager? = null
    private var usqueManager: UsqueProcessManager? = null
    private var socksGateway: RyanDpiSocksGateway? = null
    private var dohResolver: DohDnsResolver? = null
    private var tunInterface: ParcelFileDescriptor? = null
    private var statsJob: Job? = null
    private var activeSettings: RyanDpiSettings? = null

    private val screenInteractiveFlow = MutableStateFlow(true)
    private var screenReceiverRegistered: Boolean = false
    private var networkCallback: ConnectivityManager.NetworkCallback? = null
    private var lastNetworkHandle: Long = -1L
    private var lastNotifSummary: String = ""

    private val screenReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            when (intent?.action) {
                Intent.ACTION_SCREEN_ON -> screenInteractiveFlow.value = true
                Intent.ACTION_SCREEN_OFF -> screenInteractiveFlow.value = false
            }
        }
    }

    override fun onCreate() {
        super.onCreate()
        ensureNotificationChannel()
        val pm = getSystemService(PowerManager::class.java)
        screenInteractiveFlow.value = pm?.isInteractive ?: true
        val filter = IntentFilter().apply {
            addAction(Intent.ACTION_SCREEN_ON)
            addAction(Intent.ACTION_SCREEN_OFF)
        }
        ContextCompat.registerReceiver(this, screenReceiver, filter, ContextCompat.RECEIVER_NOT_EXPORTED)
        screenReceiverRegistered = true
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                RyanDpiSettings.setUserWantsConnected(this, false)
                serviceScope.launch { stopTunnel(stopSelfService = true) }
            }
            ACTION_RESTART -> {
                RyanDpiSettings.setUserWantsConnected(this, true)
                promoteToForeground(connecting = true)
                serviceScope.launch {
                    stopTunnel(stopSelfService = false)
                    delay(180)
                    startTunnel()
                }
            }
            ACTION_START, SERVICE_INTERFACE, null -> {
                RyanDpiSettings.setUserWantsConnected(this, true)
                promoteToForeground(connecting = true)
                serviceScope.launch { startTunnel() }
            }
        }
        return START_STICKY
    }

    override fun onTaskRemoved(rootIntent: Intent?) {
        val settings = RyanDpiSettings.load(this)
        if (settings.permanentAlwaysOn && RyanDpiSettings.getUserWantsConnected(this)) {
            LogRepository.info("App swiped from Recents — Permanent Always-On keeping RyanDPI active!")
            schedulePermanentRevive(delayMs = 1200L)
        }
        super.onTaskRemoved(rootIntent)
    }

    override fun onRevoke() {
        LogRepository.warn("VPN permission revoked by system or another VPN app.")
        RyanDpiSettings.setUserWantsConnected(this, false)
        serviceScope.launch { stopTunnel(stopSelfService = true) }
        super.onRevoke()
    }

    override fun onDestroy() {
        val settings = RyanDpiSettings.load(this)
        val shouldRevive = settings.permanentAlwaysOn && RyanDpiSettings.getUserWantsConnected(this)
        unregisterNetworkWatcher()
        if (screenReceiverRegistered) {
            runCatching { unregisterReceiver(screenReceiver) }
            screenReceiverRegistered = false
        }
        stopTunnelSync()
        serviceScope.cancel()
        if (shouldRevive && VpnService.prepare(this) == null) {
            LogRepository.warn("Service destroyed by OS under Permanent Always-On — scheduling immediate revive.")
            schedulePermanentRevive(delayMs = 1500L)
        }
        super.onDestroy()
    }

    private fun schedulePermanentRevive(delayMs: Long) {
        // Rate-limit automatic revives (max 3 per 60s) so a persistent OS restriction never drains battery
        val prefs = getSharedPreferences("ryandpi_revive_guard", Context.MODE_PRIVATE)
        val now = System.currentTimeMillis()
        val windowStart = prefs.getLong("window_start", 0L)
        val count = if (now - windowStart > 60_000L) 0 else prefs.getInt("count", 0)
        if (count >= 3) {
            LogRepository.warn("Permanent Always-On revive throttled (3 attempts in 60s); waiting for user or boot.")
            return
        }
        prefs.edit()
            .putLong("window_start", if (count == 0) now else windowStart)
            .putInt("count", count + 1)
            .apply()

        runCatching {
            val reviveIntent = Intent(this, BootReceiver::class.java).setAction(BootReceiver.ACTION_REVIVE)
            val pi = PendingIntent.getBroadcast(
                this,
                3080,
                reviveIntent,
                PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
            )
            val am = getSystemService(AlarmManager::class.java)
            am?.set(AlarmManager.ELAPSED_REALTIME_WAKEUP, SystemClock.elapsedRealtime() + delayMs, pi)
        }
    }

    private fun failStartupAndStop(err: String) {
        // Clear userWantsConnected on fatal config/binary/permission error so Permanent Always-On never loops
        RyanDpiSettings.setUserWantsConnected(this, false)
        LogRepository.error(err)
        stopTunnelLocked()
        VpnStateHolder.setError(err)
        refreshQuickTile()
        stopForegroundCompat()
        stopSelf()
    }

    private fun startTunnel() {
        synchronized(lifecycleLock) {
            if (tunInterface != null && VpnStateHolder.state.value.status == VpnStatus.CONNECTED) {
                return
            }
            stopTunnelLocked()

            val settings = RyanDpiSettings.load(this)
            activeSettings = settings
            VpnStateHolder.setConnecting(settings.dnsMode, settings.bypassProfile)
            refreshQuickTile()

            var socksHost = "127.0.0.1"
            var socksPort = RyanDpiSettings.GATEWAY_PORT
            var socksUser: String? = null
            var socksPass: String? = null

            if (settings.region == RegionPreset.BRAZIL) {
                if (settings.brazilTunnel == BrazilTunnelMode.CUSTOM_SOCKS5) {
                    val parsed = RyanDpiSettings.parseSocksEndpoint(settings.customSocksEndpoint)
                    if (parsed == null) {
                        failStartupAndStop("Invalid Custom SOCKS5 endpoint. Enter host:port in Settings (e.g. 1.2.3.4:1080).")
                        return
                    }
                    socksHost = parsed.host
                    socksPort = parsed.port
                    socksUser = settings.customSocksUsername.takeIf { it.isNotBlank() }
                    socksPass = settings.customSocksPassword
                } else {
                    val uMgr = UsqueProcessManager(this)
                    val res = uMgr.start(RyanDpiSettings.USQUE_SOCKS_PORT) {
                        handleChildProcessCrash("WARP MASQUE")
                    }
                    if (res.isFailure) {
                        failStartupAndStop(res.exceptionOrNull()?.message ?: "Failed to start Brazil WARP MASQUE engine.")
                        return
                    }
                    usqueManager = uMgr
                    socksHost = "127.0.0.1"
                    socksPort = RyanDpiSettings.USQUE_SOCKS_PORT
                }
            } else {
                // Global / Turkey / Russia: local zero-speed-loss ciadpi DPI engine + optional Encrypted DoH
                val ciadpiPort = settings.activeCiadpiPort()
                val manager = CiadpiProcessManager(this)
                val startResult = manager.start(settings, ciadpiPort) {
                    handleChildProcessCrash("ciadpi")
                }
                if (startResult.isFailure) {
                    failStartupAndStop(startResult.exceptionOrNull()?.message ?: "Failed to start local DPI engine.")
                    return
                }
                ciadpiManager = manager

                if (settings.dnsMode == DnsMode.ENCRYPTED_DOH) {
                    try {
                        val resolver = DohDnsResolver(
                            endpoints = settings.orderedDohEndpoints(),
                            fallbackSocksPort = ciadpiPort
                        )
                        val gateway = RyanDpiSocksGateway(
                            listenPort = RyanDpiSettings.GATEWAY_PORT,
                            upstreamCiadpiPort = ciadpiPort,
                            dohResolver = resolver
                        )
                        gateway.start()
                        dohResolver = resolver
                        socksGateway = gateway
                    } catch (e: Exception) {
                        failStartupAndStop("Encrypted DoH gateway failed to start: ${e.message}")
                        return
                    }
                }
            }

            // Build Android local VpnService TUN
            val tun = try {
                buildTunInterface(settings)
            } catch (e: Exception) {
                failStartupAndStop("Failed to create Android VPN tunnel: ${e.message}")
                return
            }

            if (tun == null) {
                failStartupAndStop("VPN permission not granted. Open RyanDPI and tap Connect to approve.")
                return
            }
            tunInterface = tun

            val configFile = File(cacheDir, "ryandpi-tproxy.yml")
            try {
                configFile.writeText(
                    buildTProxyConfigYaml(settings, socksHost, socksPort, socksUser, socksPass),
                    Charsets.UTF_8
                )
                TProxyService.TProxyStartService(configFile.absolutePath, tun.fd)
            } catch (e: Throwable) {
                failStartupAndStop("Failed to start native TUN tunnel: ${e.message}")
                return
            }

            val netLabel = detectCurrentNetworkLabel()
            lastNotifSummary = ""
            VpnStateHolder.setConnected(settings.dnsMode, settings.bypassProfile, netLabel)
            promoteToForeground(connecting = false, settings = settings)
            refreshQuickTile()
            registerNetworkWatcher()

            val modeLabel = if (settings.region == RegionPreset.BRAZIL) {
                "Brazil Discord-Only Split-Tunnel ($socksHost:$socksPort — Games 0ms Direct)"
            } else if (settings.splitTunnelMode == SplitTunnelMode.ONLY_SELECTED && settings.selectedOnlyPackages.isNotEmpty()) {
                "${settings.region.name} Only-Selected (${settings.selectedOnlyPackages.size} apps)"
            } else {
                "${settings.region.name} DPI Bypass (${settings.bypassProfile.name})"
            }
            LogRepository.info("RyanDPI active! Mode: $modeLabel${if (settings.permanentAlwaysOn) " [Permanent Always-On]" else ""}")

            startScreenAwareStatsLoop()
        }
    }

    private fun handleChildProcessCrash(engineName: String) {
        if (!recoveringChild.compareAndSet(false, true)) return
        serviceScope.launch {
            try {
                if (VpnStateHolder.state.value.status != VpnStatus.CONNECTED) return@launch
                LogRepository.warn("$engineName exited unexpectedly — event-driven instant recovery...")
                delay(250)
                val settings = activeSettings ?: RyanDpiSettings.load(this@RyanDpiVpnService)
                synchronized(lifecycleLock) {
                    if (VpnStateHolder.state.value.status != VpnStatus.CONNECTED) return@synchronized
                    val ok = if (engineName == "WARP MASQUE") {
                        val uMgr = usqueManager ?: return@synchronized
                        uMgr.start(RyanDpiSettings.USQUE_SOCKS_PORT) {
                            handleChildProcessCrash("WARP MASQUE")
                        }.isSuccess
                    } else {
                        val cMgr = ciadpiManager ?: return@synchronized
                        cMgr.start(settings, settings.activeCiadpiPort()) {
                            handleChildProcessCrash("ciadpi")
                        }.isSuccess
                    }
                    if (!ok && !settings.permanentAlwaysOn) {
                        LogRepository.error("$engineName automatic recovery failed. Disconnecting cleanly.")
                        stopTunnelLocked()
                        VpnStateHolder.setError("$engineName engine stopped unexpectedly.")
                        refreshQuickTile()
                        stopForegroundCompat()
                        stopSelf()
                    } else if (!ok) {
                        LogRepository.warn("Permanent Always-On: retrying full tunnel restart in 3s...")
                        schedulePermanentRevive(delayMs = 3000L)
                    } else {
                        LogRepository.info("$engineName recovered seamlessly without dropping VPN!")
                    }
                }
            } finally {
                recoveringChild.set(false)
            }
        }
    }

    private fun detectCurrentNetworkLabel(): String {
        return runCatching {
            val cm = getSystemService(ConnectivityManager::class.java) ?: return ""
            val caps = cm.getNetworkCapabilities(cm.activeNetwork) ?: return ""
            when {
                caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) -> "Wi-Fi"
                caps.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> "5G/4G"
                caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> "Ethernet"
                else -> ""
            }
        }.getOrDefault("")
    }

    private fun registerNetworkWatcher() {
        unregisterNetworkWatcher()
        runCatching {
            val cm = getSystemService(ConnectivityManager::class.java) ?: return
            lastNetworkHandle = cm.activeNetwork?.networkHandle ?: -1L
            val cb = object : ConnectivityManager.NetworkCallback() {
                override fun onAvailable(network: Network) {
                    val handle = network.networkHandle
                    val label = detectCurrentNetworkLabel()
                    VpnStateHolder.updateNetworkLabel(label)
                    if (lastNetworkHandle != -1L && handle != lastNetworkHandle) {
                        lastNetworkHandle = handle
                        LogRepository.info("Network switched ($label) — flushing stale DoH sockets & keeping RyanDPI active.")
                        dohResolver?.onNetworkChanged()
                        val cfg = activeSettings
                        if (cfg?.region == RegionPreset.BRAZIL && cfg.brazilTunnel == BrazilTunnelMode.BUILTIN_WARP_MASQUE) {
                            // Refresh WARP MASQUE UDP/HTTP3 socket binding cleanly on Wi-Fi <-> 5G handoff
                            serviceScope.launch {
                                delay(400)
                                val uMgr = usqueManager
                                if (uMgr != null && !uMgr.isRunning()) {
                                    handleChildProcessCrash("WARP MASQUE")
                                }
                            }
                        }
                    } else {
                        lastNetworkHandle = handle
                    }
                }

                override fun onCapabilitiesChanged(network: Network, caps: NetworkCapabilities) {
                    val label = when {
                        caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) -> "Wi-Fi"
                        caps.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> "5G/4G"
                        caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> "Ethernet"
                        else -> ""
                    }
                    if (label.isNotEmpty()) VpnStateHolder.updateNetworkLabel(label)
                }
            }
            cm.registerDefaultNetworkCallback(cb)
            networkCallback = cb
        }
    }

    private fun unregisterNetworkWatcher() {
        val cb = networkCallback ?: return
        networkCallback = null
        runCatching {
            getSystemService(ConnectivityManager::class.java)?.unregisterNetworkCallback(cb)
        }
    }

    private fun buildTunInterface(settings: RyanDpiSettings): ParcelFileDescriptor? {
        val openAppIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val builder = Builder()
            .setSession("RyanDPI")
            .setConfigureIntent(openAppIntent)
            .setMtu(RyanDpiSettings.TUN_MTU)
            .addAddress(RyanDpiSettings.VIRTUAL_TUN_IPV4, 32)
            .addRoute("0.0.0.0", 0)

        if (settings.ipv6Enabled && settings.region != RegionPreset.BRAZIL) {
            builder.addAddress(RyanDpiSettings.VIRTUAL_TUN_IPV6, 128)
            builder.addRoute("::", 0)
        }

        if (settings.region == RegionPreset.BRAZIL) {
            builder.addDnsServer("1.1.1.1")
            builder.addDnsServer("8.8.8.8")
        } else if (settings.dnsMode == DnsMode.ENCRYPTED_DOH) {
            builder.addDnsServer(RyanDpiSettings.VIRTUAL_DNS_IPV4)
        } else {
            builder.addDnsServer(settings.dohProvider.plainIp)
            val secondary = if (settings.dohProvider.plainIp == "1.1.1.1") "8.8.8.8" else "1.1.1.1"
            builder.addDnsServer(secondary)
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            builder.setMetered(false)
        }

        if (settings.region == RegionPreset.BRAZIL) {
            // Brazil ANPD Mode: ONLY route Discord (+ optional browsers) through the Geo-Unblock tunnel!
            val targets = mutableListOf<String>()
            targets.addAll(DISCORD_PACKAGES)
            if (settings.brazilIncludeBrowsers) {
                targets.addAll(BROWSER_PACKAGES)
            }
            var addedAny = false
            for (pkg in targets) {
                if (isPackageInstalled(pkg)) {
                    runCatching {
                        builder.addAllowedApplication(pkg)
                        addedAny = true
                    }
                }
            }
            if (!addedAny) {
                LogRepository.warn("Discord app not detected yet; falling back to standard tunnel exclusion.")
                runCatching { builder.addDisallowedApplication(packageName) }
            }
        } else if (settings.splitTunnelMode == SplitTunnelMode.ONLY_SELECTED && settings.selectedOnlyPackages.isNotEmpty()) {
            // Ultra-Low Battery / 0ms Gaming Mode: ONLY route user-selected apps!
            var addedAny = false
            for (pkg in settings.selectedOnlyPackages) {
                if (pkg.isNotBlank() && pkg != packageName && isPackageInstalled(pkg)) {
                    runCatching {
                        builder.addAllowedApplication(pkg)
                        addedAny = true
                    }
                }
            }
            if (!addedAny) {
                runCatching { builder.addDisallowedApplication(packageName) }
            }
        } else {
            // MANDATORY: Exclude RyanDPI itself so ciadpi & DoH sockets never loop back into the TUN!
            runCatching { builder.addDisallowedApplication(packageName) }
            for (pkg in settings.excludedPackages) {
                if (pkg.isNotBlank() && pkg != packageName) {
                    runCatching { builder.addDisallowedApplication(pkg) }
                }
            }
        }

        return builder.establish()
    }

    private fun isPackageInstalled(pkg: String): Boolean {
        return try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                packageManager.getPackageInfo(pkg, PackageManager.PackageInfoFlags.of(0))
            } else {
                @Suppress("DEPRECATION")
                packageManager.getPackageInfo(pkg, 0)
            }
            true
        } catch (_: Exception) {
            false
        }
    }

    private fun buildTProxyConfigYaml(
        settings: RyanDpiSettings,
        socksHost: String,
        socksPort: Int,
        socksUser: String?,
        socksPass: String?
    ): String {
        val ipv6Line = if (settings.ipv6Enabled && settings.region != RegionPreset.BRAZIL) {
            "  ipv6: '${RyanDpiSettings.VIRTUAL_TUN_IPV6}'\n"
        } else {
            ""
        }
        val escapedUser = socksUser?.replace("'", "''")
        val escapedPass = socksPass?.replace("'", "''")

        // Lean mobile memory buffers: cuts native RAM footprint while preserving full 5G/Wi-Fi throughput
        return buildString {
            append("tunnel:\n")
            append("  name: tun0\n")
            append("  mtu: ${RyanDpiSettings.TUN_MTU}\n")
            append("  multi-queue: false\n")
            append("  ipv4: ${RyanDpiSettings.VIRTUAL_TUN_IPV4}\n")
            append(ipv6Line)
            append("socks5:\n")
            append("  port: $socksPort\n")
            append("  address: '$socksHost'\n")
            append("  udp: 'udp'\n")
            if (!escapedUser.isNullOrBlank()) {
                append("  username: '$escapedUser'\n")
                append("  password: '${escapedPass ?: ""}'\n")
            }
            append("misc:\n")
            append("  task-stack-size: 65536\n")
            append("  tcp-buffer-size: 32768\n")
            append("  udp-recv-buffer-size: 262144\n")
            append("  connect-timeout: 5000\n")
            append("  tcp-read-write-timeout: 300000\n")
            append("  udp-read-write-timeout: 60000\n")
            append("  log-file: null\n")
            append("  log-level: error\n")
        }
    }

    /**
     * Screen-aware live data-flow sampler:
     * - When screen is ON: updates live ↓/↑ speeds & notification every 1.5s (only calling NotificationManager.notify if text changed).
     * - When screen is OFF: suspends completely (0 CPU wakeups & 0 IPCs while phone is in your pocket!).
     * - Engine health is monitored event-driven via onUnexpectedExit (0% polling overhead).
     */
    private fun startScreenAwareStatsLoop() {
        statsJob?.cancel()
        statsJob = serviceScope.launch {
            var prevTx = 0L
            var prevRx = 0L
            var prevTimeMs = SystemClock.elapsedRealtime()
            val notifManager = getSystemService(NotificationManager::class.java)

            while (isActive && VpnStateHolder.state.value.status == VpnStatus.CONNECTED) {
                if (!screenInteractiveFlow.value) {
                    // Race-free StateFlow sleep with zero CPU wakeups until the user turns the screen back on!
                    screenInteractiveFlow.first { it }
                    prevTimeMs = SystemClock.elapsedRealtime()
                    val snap = runCatching { TProxyService.TProxyGetStats() }.getOrNull()
                    if (snap != null && snap.size >= 4) {
                        prevTx = snap[1]
                        prevRx = snap[3]
                    }
                    continue
                }

                delay(STATS_INTERVAL_MS)
                if (!screenInteractiveFlow.value || VpnStateHolder.state.value.status != VpnStatus.CONNECTED) continue

                val nowMs = SystemClock.elapsedRealtime()
                val elapsedMs = (nowMs - prevTimeMs).coerceAtLeast(250L)
                prevTimeMs = nowMs

                val stats = runCatching { TProxyService.TProxyGetStats() }.getOrNull()
                val txBytes = if (stats != null && stats.size >= 4) stats[1] else prevTx
                val rxBytes = if (stats != null && stats.size >= 4) stats[3] else prevRx
                val txSpeed = ((txBytes - prevTx).coerceAtLeast(0L) * 1000L) / elapsedMs
                val rxSpeed = ((rxBytes - prevRx).coerceAtLeast(0L) * 1000L) / elapsedMs
                prevTx = txBytes
                prevRx = rxBytes

                val dnsQ = dohResolver?.getTotalQueries() ?: 0L
                val dnsH = dohResolver?.getCacheHits() ?: 0L
                VpnStateHolder.updateStats(txBytes, rxBytes, txSpeed, rxSpeed, dnsQ, dnsH)

                val cfg = activeSettings ?: continue
                if (cfg.showLiveTrafficNotification && notifManager != null) {
                    val summaryKey = "${formatSpeed(rxSpeed)}|${formatSpeed(txSpeed)}|${formatCompactBytes(rxBytes)}|${formatCompactBytes(txBytes)}|$dnsQ"
                    if (summaryKey != lastNotifSummary) {
                        lastNotifSummary = summaryKey
                        val notif = buildNotification(
                            connecting = false,
                            settings = cfg,
                            rxSpeed = rxSpeed,
                            txSpeed = txSpeed,
                            rxTotal = rxBytes,
                            txTotal = txBytes,
                            dnsQueries = dnsQ,
                            dnsCacheHits = dnsH
                        )
                        runCatching { notifManager.notify(NOTIFICATION_ID, notif) }
                    }
                }
            }
        }
    }

    private fun stopTunnel(stopSelfService: Boolean) {
        synchronized(lifecycleLock) {
            unregisterNetworkWatcher()
            stopTunnelLocked()
            VpnStateHolder.setDisconnected()
            refreshQuickTile()
            LogRepository.info("RyanDPI disconnected.")
        }
        if (stopSelfService) {
            stopForegroundCompat()
            stopSelf()
        }
    }

    private fun stopTunnelSync() {
        synchronized(lifecycleLock) {
            stopTunnelLocked()
            if (VpnStateHolder.state.value.status != VpnStatus.ERROR) {
                VpnStateHolder.setDisconnected()
            }
            refreshQuickTile()
        }
    }

    private fun stopTunnelLocked() {
        statsJob?.cancel()
        statsJob = null

        runCatching { TProxyService.TProxyStopService() }
        runCatching { tunInterface?.close() }
        tunInterface = null

        runCatching { socksGateway?.stop() }
        socksGateway = null
        dohResolver = null

        runCatching { ciadpiManager?.stop() }
        ciadpiManager = null

        runCatching { usqueManager?.stop() }
        usqueManager = null
    }

    private fun ensureNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val manager = getSystemService(NotificationManager::class.java)
            val channel = NotificationChannel(
                NOTIFICATION_CHANNEL_ID,
                getString(R.string.vpn_channel_name),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = getString(R.string.vpn_channel_desc)
                setShowBadge(false)
                setSound(null, null)
                enableVibration(false)
            }
            manager?.createNotificationChannel(channel)
        }
    }

    private fun promoteToForeground(connecting: Boolean, settings: RyanDpiSettings? = null) {
        val notification = buildNotification(connecting, settings)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            startForeground(
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }
    }

    private fun stopForegroundCompat() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            stopForeground(STOP_FOREGROUND_REMOVE)
        } else {
            @Suppress("DEPRECATION")
            stopForeground(true)
        }
    }

    private fun buildNotification(
        connecting: Boolean,
        settings: RyanDpiSettings?,
        rxSpeed: Long = 0L,
        txSpeed: Long = 0L,
        rxTotal: Long = 0L,
        txTotal: Long = 0L,
        dnsQueries: Long = 0L,
        dnsCacheHits: Long = 0L
    ): Notification {
        val cfg = settings ?: RyanDpiSettings.load(this)
        val lang = cfg.language
        val isBrazil = cfg.region == RegionPreset.BRAZIL
        val regionTag = when (cfg.region) {
            RegionPreset.TURKEY -> "TR"
            RegionPreset.RUSSIA -> "RU"
            RegionPreset.BRAZIL -> "BR"
            RegionPreset.GLOBAL_UNIVERSAL -> "GLOBAL"
        }

        val title = when {
            connecting -> when (lang) {
                "tr" -> "RyanDPI Bağlanıyor..."
                "pt" -> "RyanDPI Conectando..."
                "ru" -> "RyanDPI Подключение..."
                "fa" -> "در حال اتصال RyanDPI..."
                "id" -> "RyanDPI Menghubungkan..."
                "zh" -> "RyanDPI 正在连接..."
                "ar" -> "جاري اتصال RyanDPI..."
                else -> "RyanDPI Connecting..."
            }
            cfg.showLiveTrafficNotification ->
                "RyanDPI [$regionTag]  ↓ ${formatSpeed(rxSpeed)}  ↑ ${formatSpeed(txSpeed)}"
            isBrazil -> when (lang) {
                "pt" -> "RyanDPI Brasil — Só Discord (0 ms Jogos)"
                "tr" -> "RyanDPI Brezilya — Sadece Discord"
                else -> "RyanDPI Brazil — Discord Only (0ms Games)"
            }
            else -> when (lang) {
                "tr" -> "RyanDPI Aktif — Tüm Uygulamalar Açık"
                "pt" -> "RyanDPI Ativo — Todos Apps Liberados"
                "ru" -> "RyanDPI Активен — Все приложения открыты"
                "fa" -> "RyanDPI فعال — همه برنامه‌ها باز"
                "id" -> "RyanDPI Aktif — Semua Aplikasi Terbuka"
                "zh" -> "RyanDPI 已连接 — 全部应用已解锁"
                "ar" -> "RyanDPI نشط — جميع التطبيقات مفتوحة"
                else -> "RyanDPI Active — All Apps Unlocked"
            }
        }

        val text = when {
            connecting -> "Starting zero-lag engine..."
            cfg.showLiveTrafficNotification -> {
                val totalPart = "↓ ${formatCompactBytes(rxTotal)}  ↑ ${formatCompactBytes(txTotal)}"
                when {
                    isBrazil -> "Discord Only (0ms Games) • $totalPart"
                    cfg.dnsMode == DnsMode.ENCRYPTED_DOH && dnsQueries > 0 -> {
                        val hitPct = ((dnsCacheHits * 100L) / dnsQueries).coerceIn(0L, 100L)
                        "$totalPart • DoH $dnsQueries (${hitPct}%)"
                    }
                    else -> "$totalPart • Zero-Lag DPI"
                }
            }
            isBrazil -> "Discord Screen Share Split-Tunnel • Games 100% Direct"
            cfg.dnsMode == DnsMode.ENCRYPTED_DOH -> "Encrypted DoH DNS • Native Full Speed"
            else -> "Direct Native Engine • Native Full Speed"
        }

        val subText = buildList {
            if (cfg.permanentAlwaysOn) add("Always-On")
            val net = VpnStateHolder.state.value.networkLabel
            if (net.isNotBlank()) add(net)
        }.joinToString(" • ").ifBlank { null }

        val openIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val stopIntent = PendingIntent.getService(
            this,
            1,
            Intent(this, RyanDpiVpnService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val reconnectIntent = PendingIntent.getService(
            this,
            2,
            Intent(this, RyanDpiVpnService::class.java).setAction(ACTION_RESTART),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val disconnectLabel = when (lang) {
            "tr" -> "Kes"
            "pt" -> "Desconectar"
            "ru" -> "Отключить"
            "fa" -> "قطع"
            "id" -> "Putuskan"
            "zh" -> "断开"
            "ar" -> "قطع"
            else -> "Disconnect"
        }

        val reconnectLabel = when (lang) {
            "tr" -> "Yenile"
            "pt" -> "Reconectar"
            "ru" -> "Обновить"
            "fa" -> "اتصال مجدد"
            "id" -> "Muat Ulang"
            "zh" -> "重连"
            "ar" -> "إعادة اتصال"
            else -> "Reconnect"
        }

        return NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_ryandpi)
            .setContentTitle(title)
            .setContentText(text)
            .setSubText(subText)
            .setOngoing(true)
            .setSilent(true)
            .setOnlyAlertOnce(true)
            .setShowWhen(false)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setColor(0xFFFF1E3C.toInt())
            .setContentIntent(openIntent)
            .addAction(0, disconnectLabel, stopIntent)
            .addAction(0, reconnectLabel, reconnectIntent)
            .build()
    }

    private fun refreshQuickTile() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            runCatching {
                TileService.requestListeningState(
                    this,
                    ComponentName(this, RyanDpiTileService::class.java)
                )
            }
        }
    }
}
