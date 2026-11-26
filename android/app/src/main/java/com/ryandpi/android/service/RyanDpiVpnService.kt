package com.ryandpi.android.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.net.VpnService
import android.os.Build
import android.os.ParcelFileDescriptor
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
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.io.File

class RyanDpiVpnService : VpnService() {

    companion object {
        const val ACTION_START = "com.ryandpi.android.action.START"
        const val ACTION_STOP = "com.ryandpi.android.action.STOP"
        const val ACTION_RESTART = "com.ryandpi.android.action.RESTART"

        private const val NOTIFICATION_CHANNEL_ID = "ryandpi_vpn_tunnel"
        private const val NOTIFICATION_ID = 1080

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
            val intent = Intent(context, RyanDpiVpnService::class.java).setAction(ACTION_START)
            ContextCompat.startForegroundService(context, intent)
        }

        fun stopService(context: Context) {
            val intent = Intent(context, RyanDpiVpnService::class.java).setAction(ACTION_STOP)
            context.startService(intent)
        }

        fun restartService(context: Context) {
            val intent = Intent(context, RyanDpiVpnService::class.java).setAction(ACTION_RESTART)
            ContextCompat.startForegroundService(context, intent)
        }
    }

    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val lifecycleLock = Any()

    private var ciadpiManager: CiadpiProcessManager? = null
    private var usqueManager: UsqueProcessManager? = null
    private var socksGateway: RyanDpiSocksGateway? = null
    private var dohResolver: DohDnsResolver? = null
    private var tunInterface: ParcelFileDescriptor? = null
    private var statsJob: Job? = null

    override fun onCreate() {
        super.onCreate()
        ensureNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                serviceScope.launch { stopTunnel(stopSelfService = true) }
            }
            ACTION_RESTART -> {
                promoteToForeground(connecting = true)
                serviceScope.launch {
                    stopTunnel(stopSelfService = false)
                    delay(200)
                    startTunnel()
                }
            }
            ACTION_START, SERVICE_INTERFACE, null -> {
                promoteToForeground(connecting = true)
                serviceScope.launch { startTunnel() }
            }
        }
        return START_STICKY
    }

    override fun onRevoke() {
        LogRepository.warn("VPN permission revoked by system or another VPN app.")
        serviceScope.launch { stopTunnel(stopSelfService = true) }
        super.onRevoke()
    }

    override fun onDestroy() {
        stopTunnelSync()
        serviceScope.cancel()
        super.onDestroy()
    }

    private fun startTunnel() {
        synchronized(lifecycleLock) {
            if (tunInterface != null && VpnStateHolder.state.value.status == VpnStatus.CONNECTED) {
                return
            }
            stopTunnelLocked()

            val settings = RyanDpiSettings.load(this)
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
                        val err = "Invalid Custom SOCKS5 endpoint. Enter host:port in Settings (e.g. 1.2.3.4:1080)."
                        LogRepository.error(err)
                        VpnStateHolder.setError(err)
                        refreshQuickTile()
                        stopSelf()
                        return
                    }
                    socksHost = parsed.host
                    socksPort = parsed.port
                    socksUser = settings.customSocksUsername.takeIf { it.isNotBlank() }
                    socksPass = settings.customSocksPassword
                } else {
                    val uMgr = UsqueProcessManager(this)
                    val res = uMgr.start(RyanDpiSettings.USQUE_SOCKS_PORT)
                    if (res.isFailure) {
                        val err = res.exceptionOrNull()?.message ?: "Failed to start Brazil WARP MASQUE engine."
                        VpnStateHolder.setError(err)
                        refreshQuickTile()
                        stopSelf()
                        return
                    }
                    usqueManager = uMgr
                    socksHost = "127.0.0.1"
                    socksPort = RyanDpiSettings.USQUE_SOCKS_PORT
                }
            } else {
                // Global Universal / Turkey / Russia: local zero-speed-loss ciadpi DPI engine + optional Encrypted DoH
                val ciadpiPort = settings.activeCiadpiPort()
                val manager = CiadpiProcessManager(this)
                val startResult = manager.start(settings, ciadpiPort)
                if (startResult.isFailure) {
                    val msg = startResult.exceptionOrNull()?.message ?: "Failed to start local DPI engine."
                    VpnStateHolder.setError(msg)
                    refreshQuickTile()
                    stopSelf()
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
                        val err = "Encrypted DoH gateway failed to start: ${e.message}"
                        LogRepository.error(err)
                        stopTunnelLocked()
                        VpnStateHolder.setError(err)
                        refreshQuickTile()
                        stopSelf()
                        return
                    }
                }
            }

            // Build Android local VpnService TUN
            val tun = try {
                buildTunInterface(settings)
            } catch (e: Exception) {
                val err = "Failed to create Android VPN tunnel: ${e.message}"
                LogRepository.error(err)
                stopTunnelLocked()
                VpnStateHolder.setError(err)
                refreshQuickTile()
                stopSelf()
                return
            }

            if (tun == null) {
                val err = "VPN permission not granted. Open RyanDPI and tap Connect to approve."
                LogRepository.error(err)
                stopTunnelLocked()
                VpnStateHolder.setError(err)
                refreshQuickTile()
                stopSelf()
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
                val err = "Failed to start native TUN tunnel: ${e.message}"
                LogRepository.error(err)
                stopTunnelLocked()
                VpnStateHolder.setError(err)
                refreshQuickTile()
                stopSelf()
                return
            }

            VpnStateHolder.setConnected(settings.dnsMode, settings.bypassProfile)
            promoteToForeground(connecting = false, settings = settings)
            refreshQuickTile()

            val modeLabel = if (settings.region == RegionPreset.BRAZIL) {
                "Brazil Discord-Only Split-Tunnel ($socksHost:$socksPort — Games 0ms Direct)"
            } else {
                "${settings.region.name} DPI Bypass (${settings.bypassProfile.name})"
            }
            LogRepository.info("RyanDPI active! Mode: $modeLabel")

            startStatsLoop()
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
            // 100% of mobile games and other apps stay completely direct with 0ms added lag!
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
                // If Discord isn't installed yet, fall back to excluding RyanDPI so tunnel still establishes cleanly
                LogRepository.warn("Discord app not detected yet; falling back to standard tunnel exclusion.")
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
            append("  task-stack-size: 81920\n")
            append("  tcp-buffer-size: 65536\n")
            append("  udp-recv-buffer-size: 524288\n")
            append("  connect-timeout: 5000\n")
            append("  tcp-read-write-timeout: 300000\n")
            append("  udp-read-write-timeout: 60000\n")
            append("  log-file: null\n")
            append("  log-level: error\n")
        }
    }

    private fun startStatsLoop() {
        statsJob?.cancel()
        statsJob = serviceScope.launch {
            while (isActive && VpnStateHolder.state.value.status == VpnStatus.CONNECTED) {
                delay(1000)
                val mgr = ciadpiManager
                if (mgr != null && !mgr.isRunning()) {
                    LogRepository.warn("ciadpi process stopped unexpectedly; attempting automatic recovery...")
                    val settings = RyanDpiSettings.load(this@RyanDpiVpnService)
                    val restartRes = mgr.start(settings, settings.activeCiadpiPort())
                    if (restartRes.isFailure) {
                        LogRepository.error("Automatic recovery failed. Disconnecting cleanly.")
                        stopTunnel(stopSelfService = true)
                        break
                    }
                }
                val uMgr = usqueManager
                if (uMgr != null && !uMgr.isRunning()) {
                    LogRepository.warn("WARP MASQUE engine stopped; attempting automatic recovery...")
                    if (uMgr.start(RyanDpiSettings.USQUE_SOCKS_PORT).isFailure) {
                        stopTunnel(stopSelfService = true)
                        break
                    }
                }

                val stats = runCatching { TProxyService.TProxyGetStats() }.getOrNull()
                val txBytes = if (stats != null && stats.size >= 4) stats[1] else 0L
                val rxBytes = if (stats != null && stats.size >= 4) stats[3] else 0L
                val dnsQ = dohResolver?.getTotalQueries() ?: 0L
                val dnsH = dohResolver?.getCacheHits() ?: 0L
                VpnStateHolder.updateStats(txBytes, rxBytes, dnsQ, dnsH)
            }
        }
    }

    private fun stopTunnel(stopSelfService: Boolean) {
        synchronized(lifecycleLock) {
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

    private fun buildNotification(connecting: Boolean, settings: RyanDpiSettings?): Notification {
        val cfg = settings ?: RyanDpiSettings.load(this)
        val lang = cfg.language
        val isBrazil = cfg.region == RegionPreset.BRAZIL

        val title = when {
            connecting -> when (lang) {
                "tr" -> "RyanDPI Bağlanıyor..."
                "pt" -> "RyanDPI Conectando..."
                "ru" -> "RyanDPI Подключение..."
                else -> "RyanDPI Connecting..."
            }
            isBrazil -> when (lang) {
                "pt" -> "RyanDPI Brasil — Só Discord (0 ms nos Jogos)"
                "tr" -> "RyanDPI Brezilya — Sadece Discord"
                else -> "RyanDPI Brazil — Discord Only (0ms Game Lag)"
            }
            else -> when (lang) {
                "tr" -> "RyanDPI Aktif — Tüm Uygulamalar Açık"
                "pt" -> "RyanDPI Ativo — Todos Apps Liberados"
                "ru" -> "RyanDPI Активен — Все приложения открыты"
                else -> "RyanDPI Active — All Apps Unlocked"
            }
        }

        val text = when {
            connecting -> "Starting local zero-lag engine..."
            isBrazil -> "Discord Screen Share Split-Tunnel • Games 100% Direct"
            cfg.dnsMode == DnsMode.ENCRYPTED_DOH -> "Encrypted DoH DNS • Native Full Speed"
            else -> "Direct Native Engine • Native Full Speed"
        }

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

        val disconnectLabel = when (lang) {
            "tr" -> "Bağlantıyı Kes"
            "pt" -> "Desconectar"
            "ru" -> "Отключить"
            else -> "Disconnect"
        }

        return NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_ryandpi)
            .setContentTitle(title)
            .setContentText(text)
            .setOngoing(true)
            .setSilent(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setCategory(NotificationCompat.CATEGORY_SERVICE)
            .setColor(0xFFFF1E3C.toInt())
            .setContentIntent(openIntent)
            .addAction(0, disconnectLabel, stopIntent)
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
