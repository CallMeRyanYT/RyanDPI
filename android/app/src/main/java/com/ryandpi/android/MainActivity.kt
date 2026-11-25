package com.ryandpi.android

import android.Manifest
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.net.VpnService
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.core.content.ContextCompat
import com.ryandpi.android.engine.LogRepository
import com.ryandpi.android.engine.RyanDpiSettings
import com.ryandpi.android.engine.VpnStateHolder
import com.ryandpi.android.engine.VpnStatus
import com.ryandpi.android.network.AndroidUpdateInfo
import com.ryandpi.android.network.SelfTestEngine
import com.ryandpi.android.network.SelfTestItem
import com.ryandpi.android.network.UpdateChecker
import com.ryandpi.android.service.RyanDpiVpnService
import com.ryandpi.android.ui.InstalledAppItem
import com.ryandpi.android.ui.RyanDpiMainScreen
import com.ryandpi.android.ui.RyanDpiTheme
import com.ryandpi.android.ui.StringsRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity : ComponentActivity() {

    companion object {
        const val EXTRA_AUTO_CONNECT = "com.ryandpi.android.extra.AUTO_CONNECT"
    }

    private val vpnPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == RESULT_OK) {
            RyanDpiVpnService.startService(this)
        } else {
            val s = StringsRepository.forLanguage(RyanDpiSettings.load(this).language)
            LogRepository.warn(s.vpnPermissionDenied)
            Toast.makeText(this, s.vpnPermissionDenied, Toast.LENGTH_LONG).show()
        }
    }

    private val notificationPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) {
        // Non-blocking: VPN works even if notifications are denied, though persistent status notification is helpful.
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val initialSettings = RyanDpiSettings.load(this)
        applySystemBars(initialSettings.isDarkTheme)
        requestNotificationPermissionIfNeeded()

        if (intent?.getBooleanExtra(EXTRA_AUTO_CONNECT, false) == true) {
            requestConnect()
        }

        setContent {
            var settings by remember { mutableStateOf(initialSettings) }
            val vpnState by VpnStateHolder.state.collectAsState()
            val logs by LogRepository.entries.collectAsState()
            val scope = rememberCoroutineScope()

            var selfTestResults by remember { mutableStateOf<List<SelfTestItem>>(emptyList()) }
            var isTesting by remember { mutableStateOf(false) }
            var updateInfo by remember { mutableStateOf<AndroidUpdateInfo?>(null) }
            var updateDownloadPct by remember { mutableStateOf<Int?>(null) }
            var updateStatusMsg by remember { mutableStateOf<String?>(null) }
            var settingsDirtyWhileConnected by remember { mutableStateOf(false) }
            var installedApps by remember { mutableStateOf<List<InstalledAppItem>>(emptyList()) }

            LaunchedEffect(settings.isDarkTheme) {
                applySystemBars(settings.isDarkTheme)
            }

            // Automatic startup GitHub release check
            LaunchedEffect(Unit) {
                if (settings.autoCheckUpdates) {
                    val res = UpdateChecker.checkForUpdate()
                    if (res.success && res.updateAvailable) {
                        updateInfo = res
                        LogRepository.info("New Android update detected: v${res.latestVersion} (${res.assetName})")
                    }
                }
            }

            RyanDpiTheme(darkTheme = settings.isDarkTheme) {
                RyanDpiMainScreen(
                    settings = settings,
                    vpnState = vpnState,
                    logs = logs,
                    selfTestResults = selfTestResults,
                    isTesting = isTesting,
                    updateInfo = updateInfo,
                    updateDownloadPct = updateDownloadPct,
                    updateStatusMsg = updateStatusMsg,
                    settingsDirtyWhileConnected = settingsDirtyWhileConnected,
                    installedApps = installedApps,
                    onToggleConnection = {
                        when (vpnState.status) {
                            VpnStatus.CONNECTED, VpnStatus.CONNECTING -> {
                                settingsDirtyWhileConnected = false
                                RyanDpiVpnService.stopService(this)
                            }
                            else -> {
                                settingsDirtyWhileConnected = false
                                requestConnect()
                            }
                        }
                    },
                    onReconnectApply = {
                        settingsDirtyWhileConnected = false
                        RyanDpiVpnService.restartService(this)
                    },
                    onRunSelfTest = {
                        if (!isTesting) {
                            isTesting = true
                            val strings = StringsRepository.forLanguage(settings.language)
                            scope.launch {
                                try {
                                    LogRepository.info("Running Unblock & Speed Self-Test...")
                                    val results = SelfTestEngine.runAll(settings, strings)
                                    selfTestResults = results
                                    val passedCount = results.count { it.passed }
                                    LogRepository.info("Self-Test completed: $passedCount/${results.size} checks passed.")
                                } finally {
                                    isTesting = false
                                }
                            }
                        }
                    },
                    onUpdateSettings = { updated, requiresReconnect ->
                        settings = updated
                        RyanDpiSettings.save(this, updated)
                        if (requiresReconnect && vpnState.status == VpnStatus.CONNECTED) {
                            settingsDirtyWhileConnected = true
                        }
                    },
                    onToggleLanguage = {
                        val nextLang = if (settings.language == "tr") "en" else "tr"
                        val updated = settings.copy(language = nextLang)
                        settings = updated
                        RyanDpiSettings.save(this, updated)
                    },
                    onToggleTheme = {
                        val updated = settings.copy(isDarkTheme = !settings.isDarkTheme)
                        settings = updated
                        RyanDpiSettings.save(this, updated)
                    },
                    onCheckUpdates = {
                        val strings = StringsRepository.forLanguage(settings.language)
                        scope.launch {
                            updateStatusMsg = "..."
                            val res = UpdateChecker.checkForUpdate()
                            if (res.success && res.updateAvailable) {
                                updateInfo = res
                                updateStatusMsg = "${strings.updateAvailableTitle}: v${res.latestVersion}"
                            } else if (res.success) {
                                updateInfo = null
                                updateStatusMsg = strings.upToDateMsg
                                Toast.makeText(this@MainActivity, strings.upToDateMsg, Toast.LENGTH_SHORT).show()
                            } else {
                                updateStatusMsg = res.errorMessage ?: "Error"
                            }
                        }
                    },
                    onStartUpdateDownload = { info ->
                        if (updateDownloadPct == null) {
                            scope.launch {
                                updateDownloadPct = 0
                                val result = UpdateChecker.downloadAndVerifyApk(this@MainActivity, info) { pct ->
                                    launch(Dispatchers.Main) { updateDownloadPct = pct }
                                }
                                updateDownloadPct = null
                                result.onSuccess { apkFile ->
                                    runCatching {
                                        startActivity(UpdateChecker.buildInstallIntent(this@MainActivity, apkFile))
                                    }.onFailure { err ->
                                        updateStatusMsg = err.message
                                        LogRepository.error("Installer launch failed: ${err.message}")
                                    }
                                }.onFailure { err ->
                                    updateStatusMsg = err.message
                                    LogRepository.error("APK download failed: ${err.message}")
                                    Toast.makeText(this@MainActivity, err.message ?: "Download failed", Toast.LENGTH_LONG).show()
                                }
                            }
                        }
                    },
                    onOpenPrivateDnsHelper = {
                        val strings = StringsRepository.forLanguage(settings.language)
                        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                        cm.setPrimaryClip(ClipData.newPlainText("Private DNS", "one.one.one.one"))
                        Toast.makeText(this, strings.privateDnsCopiedToast, Toast.LENGTH_LONG).show()
                        val wirelessIntent = Intent(Settings.ACTION_WIRELESS_SETTINGS)
                        runCatching { startActivity(wirelessIntent) }
                            .onFailure { runCatching { startActivity(Intent(Settings.ACTION_SETTINGS)) } }
                    },
                    onCopyLogs = {
                        val strings = StringsRepository.forLanguage(settings.language)
                        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                        cm.setPrimaryClip(ClipData.newPlainText("RyanDPI Logs", LogRepository.exportAsText()))
                        Toast.makeText(this, strings.logsCopiedToast, Toast.LENGTH_SHORT).show()
                    },
                    onClearLogs = {
                        LogRepository.clear()
                    },
                    onOpenGithub = {
                        runCatching {
                            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("https://github.com/CallMeRyanYT/RyanDPI")))
                        }
                    },
                    onLoadInstalledApps = {
                        if (installedApps.isEmpty()) {
                            scope.launch {
                                installedApps = withContext(Dispatchers.IO) { queryLaunchableApps() }
                            }
                        }
                    }
                )
            }
        }
    }

    private fun requestConnect() {
        val prepareIntent = VpnService.prepare(this)
        if (prepareIntent != null) {
            vpnPermissionLauncher.launch(prepareIntent)
        } else {
            RyanDpiVpnService.startService(this)
        }
    }

    private fun requestNotificationPermissionIfNeeded() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS)
                != PackageManager.PERMISSION_GRANTED
            ) {
                notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
    }

    private fun applySystemBars(darkTheme: Boolean) {
        val barColor = if (darkTheme) 0xFF060608.toInt() else 0xFFF5F2F3.toInt()
        val style = if (darkTheme) {
            SystemBarStyle.dark(barColor)
        } else {
            SystemBarStyle.light(barColor, barColor)
        }
        enableEdgeToEdge(statusBarStyle = style, navigationBarStyle = style)
    }

    private fun queryLaunchableApps(): List<InstalledAppItem> {
        val pm = packageManager
        val intent = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_LAUNCHER)
        val resolved = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            pm.queryIntentActivities(intent, PackageManager.ResolveInfoFlags.of(0))
        } else {
            @Suppress("DEPRECATION")
            pm.queryIntentActivities(intent, 0)
        }

        val seen = HashSet<String>()
        val list = ArrayList<InstalledAppItem>()
        for (ri in resolved) {
            val pkg = ri.activityInfo?.packageName ?: continue
            if (pkg == packageName || !seen.add(pkg)) continue
            val label = ri.loadLabel(pm)?.toString()?.takeIf { it.isNotBlank() } ?: pkg
            list.add(InstalledAppItem(appName = label, packageName = pkg))
        }
        return list.sortedBy { it.appName.lowercase() }
    }
}
