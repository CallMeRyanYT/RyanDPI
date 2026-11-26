package com.ryandpi.android.ui

import android.content.Intent
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.OpenInNew
import androidx.compose.material.icons.rounded.Apps
import androidx.compose.material.icons.rounded.CheckCircle
import androidx.compose.material.icons.rounded.ContentCopy
import androidx.compose.material.icons.rounded.DarkMode
import androidx.compose.material.icons.rounded.DeleteOutline
import androidx.compose.material.icons.rounded.Dns
import androidx.compose.material.icons.rounded.Download
import androidx.compose.material.icons.rounded.ErrorOutline
import androidx.compose.material.icons.rounded.LightMode
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.PowerSettingsNew
import androidx.compose.material.icons.rounded.Public
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.Search
import androidx.compose.material.icons.rounded.Security
import androidx.compose.material.icons.rounded.Speed
import androidx.compose.material.icons.rounded.SystemUpdate
import androidx.compose.material.icons.rounded.Tune
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CheckboxDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.RadioButton
import androidx.compose.material3.RadioButtonDefaults
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.ryandpi.android.BuildConfig
import com.ryandpi.android.engine.BrazilTunnelMode
import com.ryandpi.android.engine.BypassProfile
import com.ryandpi.android.engine.DnsMode
import com.ryandpi.android.engine.DohProvider
import com.ryandpi.android.engine.LogEntry
import com.ryandpi.android.engine.LogLevel
import com.ryandpi.android.engine.RegionPreset
import com.ryandpi.android.engine.RyanDpiSettings
import com.ryandpi.android.engine.VpnStatus
import com.ryandpi.android.engine.VpnUiState
import com.ryandpi.android.network.AndroidUpdateInfo
import com.ryandpi.android.network.SelfTestItem
import kotlinx.coroutines.delay
import java.util.Locale

data class InstalledAppItem(
    val appName: String,
    val packageName: String
)

@Composable
fun RyanDpiMainScreen(
    settings: RyanDpiSettings,
    vpnState: VpnUiState,
    logs: List<LogEntry>,
    selfTestResults: List<SelfTestItem>,
    isTesting: Boolean,
    updateInfo: AndroidUpdateInfo?,
    updateDownloadPct: Int?,
    updateStatusMsg: String?,
    settingsDirtyWhileConnected: Boolean,
    installedApps: List<InstalledAppItem>,
    onToggleConnection: () -> Unit,
    onReconnectApply: () -> Unit,
    onRunSelfTest: () -> Unit,
    onUpdateSettings: (RyanDpiSettings, Boolean) -> Unit,
    onSelectLanguage: (String) -> Unit,
    onToggleTheme: () -> Unit,
    onCheckUpdates: () -> Unit,
    onStartUpdateDownload: (AndroidUpdateInfo) -> Unit,
    onOpenPrivateDnsHelper: () -> Unit,
    onCopyLogs: () -> Unit,
    onClearLogs: () -> Unit,
    onOpenGithub: () -> Unit,
    onLoadInstalledApps: () -> Unit
) {
    val c = LocalRyanDpiColors.current
    val s = remember(settings.language) { StringsRepository.forLanguage(settings.language) }
    var selectedTab by remember { mutableIntStateOf(0) }
    var showExcludedAppsDialog by remember { mutableStateOf(false) }
    var showRegionPickerDialog by remember(settings.regionConfigured) { mutableStateOf(!settings.regionConfigured) }
    var showLanguagePickerDialog by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(c.bgCanvas)
            .statusBarsPadding()
            .navigationBarsPadding()
    ) {
        // Top Brand Bar
        TopHeaderBar(
            settings = settings,
            strings = s,
            onOpenRegionPicker = { showRegionPickerDialog = true },
            onOpenLanguagePicker = { showLanguagePickerDialog = true },
            onToggleTheme = onToggleTheme
        )

        // 3-Tab Segmented Navigation Pill (matching Windows Home | Settings | Tools & Logs)
        SegmentedTabBar(
            selectedTab = selectedTab,
            tabs = listOf(s.tabHome, s.tabSettings, s.tabTools),
            onSelectTab = { selectedTab = it }
        )

        // Tab Body
        Box(modifier = Modifier.weight(1f)) {
            when (selectedTab) {
                0 -> HomeTab(
                    settings = settings,
                    vpnState = vpnState,
                    strings = s,
                    selfTestResults = selfTestResults,
                    isTesting = isTesting,
                    updateInfo = updateInfo,
                    updateDownloadPct = updateDownloadPct,
                    updateStatusMsg = updateStatusMsg,
                    onToggleConnection = onToggleConnection,
                    onRunSelfTest = onRunSelfTest,
                    onStartUpdateDownload = onStartUpdateDownload
                )
                1 -> SettingsTab(
                    settings = settings,
                    vpnState = vpnState,
                    strings = s,
                    settingsDirtyWhileConnected = settingsDirtyWhileConnected,
                    onUpdateSettings = onUpdateSettings,
                    onReconnectApply = onReconnectApply,
                    onOpenPrivateDnsHelper = onOpenPrivateDnsHelper,
                    onOpenExcludedApps = {
                        onLoadInstalledApps()
                        showExcludedAppsDialog = true
                    }
                )
                else -> ToolsTab(
                    strings = s,
                    logs = logs,
                    updateStatusMsg = updateStatusMsg,
                    onCheckUpdates = onCheckUpdates,
                    onCopyLogs = onCopyLogs,
                    onClearLogs = onClearLogs,
                    onOpenGithub = onOpenGithub
                )
            }
        }
    }

    if (showRegionPickerDialog) {
        RegionPickerDialog(
            strings = s,
            selectedRegion = settings.region,
            onSelectRegion = { chosen ->
                onUpdateSettings(settings.withRegionPreset(chosen), true)
                showRegionPickerDialog = false
            },
            onDismiss = {
                if (!settings.regionConfigured) {
                    onUpdateSettings(settings.copy(regionConfigured = true), false)
                }
                showRegionPickerDialog = false
            }
        )
    }

    if (showLanguagePickerDialog) {
        LanguagePickerDialog(
            strings = s,
            selectedLanguage = settings.language,
            onSelectLanguage = { chosenCode ->
                onSelectLanguage(chosenCode)
                showLanguagePickerDialog = false
            },
            onDismiss = { showLanguagePickerDialog = false }
        )
    }

    if (showExcludedAppsDialog) {
        ExcludedAppsDialog(
            strings = s,
            installedApps = installedApps,
            excludedPackages = settings.excludedPackages,
            onToggleApp = { pkg ->
                val updated = settings.excludedPackages.toMutableSet()
                if (updated.contains(pkg)) updated.remove(pkg) else updated.add(pkg)
                onUpdateSettings(settings.copy(excludedPackages = updated), true)
            },
            onDismiss = { showExcludedAppsDialog = false }
        )
    }
}

@Composable
private fun TopHeaderBar(
    settings: RyanDpiSettings,
    strings: AppStrings,
    onOpenRegionPicker: () -> Unit,
    onOpenLanguagePicker: () -> Unit,
    onToggleTheme: () -> Unit
) {
    val c = LocalRyanDpiColors.current
    val regionTag = when (settings.region) {
        RegionPreset.TURKEY -> strings.regionTurkeyTag
        RegionPreset.RUSSIA -> strings.regionRussiaTag
        RegionPreset.BRAZIL -> strings.regionBrazilTag
        RegionPreset.GLOBAL_UNIVERSAL -> strings.regionGlobalTag
    }
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        DpiShieldLogo(size = 38.dp, active = false)
        Spacer(modifier = Modifier.width(10.dp))
        Column(modifier = Modifier.weight(1f)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = "RyanDPI",
                    color = c.textPrimary,
                    fontSize = 19.sp,
                    fontWeight = FontWeight.ExtraBold,
                    maxLines = 1
                )
                Spacer(modifier = Modifier.width(6.dp))
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(6.dp))
                        .background(c.accentRed.copy(alpha = 0.16f))
                        .border(1.dp, c.accentRed.copy(alpha = 0.45f), RoundedCornerShape(6.dp))
                        .padding(horizontal = 5.dp, vertical = 2.dp)
                ) {
                    Text(
                        text = "v${BuildConfig.VERSION_NAME}",
                        color = c.accentRed,
                        fontSize = 9.5.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }
            Text(
                text = strings.badgeAllApps,
                color = c.textMuted,
                fontSize = 9.5.sp,
                fontWeight = FontWeight.SemiBold,
                letterSpacing = 0.5.sp,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }

        // Country / Region Mode Pill
        Surface(
            onClick = onOpenRegionPicker,
            shape = RoundedCornerShape(10.dp),
            color = c.bgCard,
            border = BorderStroke(1.dp, c.borderSubtle)
        ) {
            Text(
                text = regionTag,
                color = c.textPrimary,
                fontSize = 11.sp,
                fontWeight = FontWeight.ExtraBold,
                modifier = Modifier.padding(horizontal = 9.dp, vertical = 7.dp)
            )
        }

        Spacer(modifier = Modifier.width(6.dp))

        // 1-tap 8-language picker pill (EN / TR / PT / RU / FA / ID / ZH / AR)
        Surface(
            onClick = onOpenLanguagePicker,
            shape = RoundedCornerShape(10.dp),
            color = c.bgCard,
            border = BorderStroke(1.dp, c.borderSubtle)
        ) {
            Text(
                text = settings.language.uppercase(Locale.ROOT),
                color = c.accentRed,
                fontSize = 11.5.sp,
                fontWeight = FontWeight.ExtraBold,
                modifier = Modifier.padding(horizontal = 9.dp, vertical = 7.dp)
            )
        }

        Spacer(modifier = Modifier.width(6.dp))

        // Dark / Light toggle
        Surface(
            onClick = onToggleTheme,
            shape = RoundedCornerShape(10.dp),
            color = c.bgCard,
            border = BorderStroke(1.dp, c.borderSubtle)
        ) {
            Box(
                modifier = Modifier.padding(7.dp),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = if (settings.isDarkTheme) Icons.Rounded.LightMode else Icons.Rounded.DarkMode,
                    contentDescription = "Theme",
                    tint = c.textSecondary,
                    modifier = Modifier.size(18.dp)
                )
            }
        }
    }
}

@Composable
private fun SegmentedTabBar(
    selectedTab: Int,
    tabs: List<String>,
    onSelectTab: (Int) -> Unit
) {
    val c = LocalRyanDpiColors.current
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 4.dp)
            .clip(RoundedCornerShape(14.dp))
            .background(c.bgCard)
            .border(1.dp, c.borderSubtle, RoundedCornerShape(14.dp))
            .padding(4.dp),
        horizontalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        tabs.forEachIndexed { idx, label ->
            val selected = idx == selectedTab
            Box(
                modifier = Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(10.dp))
                    .then(
                        if (selected) {
                            Modifier.background(
                                Brush.horizontalGradient(listOf(c.accentRed, c.accentRedDeep))
                            )
                        } else {
                            Modifier
                        }
                    )
                    .clickable { onSelectTab(idx) }
                    .padding(vertical = 10.dp, horizontal = 4.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = label,
                    color = if (selected) Color.White else c.textSecondary,
                    fontSize = 12.5.sp,
                    fontWeight = if (selected) FontWeight.Bold else FontWeight.Medium,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
        }
    }
}

@Composable
private fun HomeTab(
    settings: RyanDpiSettings,
    vpnState: VpnUiState,
    strings: AppStrings,
    selfTestResults: List<SelfTestItem>,
    isTesting: Boolean,
    updateInfo: AndroidUpdateInfo?,
    updateDownloadPct: Int?,
    updateStatusMsg: String?,
    onToggleConnection: () -> Unit,
    onRunSelfTest: () -> Unit,
    onStartUpdateDownload: (AndroidUpdateInfo) -> Unit
) {
    val c = LocalRyanDpiColors.current
    val isConnected = vpnState.status == VpnStatus.CONNECTED
    val isConnecting = vpnState.status == VpnStatus.CONNECTING

    var nowMs by remember { mutableLongStateOf(System.currentTimeMillis()) }
    LaunchedEffect(isConnected) {
        while (isConnected) {
            nowMs = System.currentTimeMillis()
            delay(1000)
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 10.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        // Optional GitHub Update Banner
        if (updateInfo != null && updateInfo.updateAvailable) {
            Surface(
                shape = RoundedCornerShape(16.dp),
                color = c.bgCard,
                border = BorderStroke(1.dp, c.accentRed.copy(alpha = 0.65f))
            ) {
                Column(modifier = Modifier.padding(14.dp)) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Rounded.SystemUpdate,
                            contentDescription = null,
                            tint = c.accentRed
                        )
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = "${strings.updateAvailableTitle}: v${updateInfo.latestVersion}",
                                color = c.textPrimary,
                                fontWeight = FontWeight.Bold,
                                fontSize = 14.sp
                            )
                            if (!updateStatusMsg.isNullOrBlank()) {
                                Text(
                                    text = updateStatusMsg,
                                    color = c.textSecondary,
                                    fontSize = 12.sp
                                )
                            }
                        }
                        if (updateDownloadPct == null) {
                            Button(
                                onClick = { onStartUpdateDownload(updateInfo) },
                                colors = ButtonDefaults.buttonColors(containerColor = c.accentRed),
                                shape = RoundedCornerShape(10.dp),
                                contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp)
                            ) {
                                Text(strings.updateNow, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                    if (updateDownloadPct != null) {
                        Spacer(modifier = Modifier.height(10.dp))
                        LinearProgressIndicator(
                            progress = { updateDownloadPct / 100f },
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(6.dp)
                                .clip(RoundedCornerShape(4.dp)),
                            color = c.accentRed,
                            trackColor = c.bgCardAlt
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = "${strings.updatingPct} $updateDownloadPct%",
                            color = c.textSecondary,
                            fontSize = 11.5.sp
                        )
                    }
                }
            }
        }

        // Hero Connection Card
        Surface(
            shape = RoundedCornerShape(22.dp),
            color = c.bgCard,
            border = BorderStroke(
                width = 1.dp,
                color = if (isConnected) c.success.copy(alpha = 0.55f) else c.borderSubtle
            )
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 20.dp, vertical = 22.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                DpiShieldLogo(
                    size = 94.dp,
                    active = isConnected,
                    pulsing = isConnecting
                )

                Spacer(modifier = Modifier.height(14.dp))

                Text(
                    text = when (vpnState.status) {
                        VpnStatus.CONNECTED -> if (settings.region == RegionPreset.BRAZIL) strings.statusConnectedBrazil else strings.statusConnected
                        VpnStatus.CONNECTING -> strings.statusConnecting
                        VpnStatus.ERROR -> strings.statusError
                        VpnStatus.DISCONNECTED -> strings.statusDisconnected
                    },
                    color = when (vpnState.status) {
                        VpnStatus.CONNECTED -> c.success
                        VpnStatus.ERROR -> c.danger
                        else -> c.textPrimary
                    },
                    fontSize = 18.sp,
                    fontWeight = FontWeight.ExtraBold,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(6.dp))

                Text(
                    text = when (vpnState.status) {
                        VpnStatus.CONNECTED -> if (settings.region == RegionPreset.BRAZIL) strings.subtitleConnectedBrazil else strings.subtitleConnected
                        VpnStatus.CONNECTING -> strings.subtitleConnecting
                        VpnStatus.ERROR -> vpnState.errorMessage ?: strings.subtitleDisconnected
                        VpnStatus.DISCONNECTED -> if (settings.region == RegionPreset.BRAZIL) strings.subtitleDisconnectedBrazil else strings.subtitleDisconnected
                    },
                    color = if (vpnState.status == VpnStatus.ERROR) c.danger else c.textSecondary,
                    fontSize = 12.5.sp,
                    lineHeight = 18.sp,
                    textAlign = TextAlign.Center
                )

                // Active Region & Engine badges
                Spacer(modifier = Modifier.height(14.dp))
                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    InfoPill(
                        label = when (settings.region) {
                            RegionPreset.BRAZIL -> if (settings.brazilTunnel == BrazilTunnelMode.CUSTOM_SOCKS5) "BR • Custom SOCKS5" else "BR • WARP MASQUE"
                            RegionPreset.RUSSIA -> "RU • TSPU"
                            RegionPreset.TURKEY -> "TR • DoH ${settings.dohProvider.plainIp}"
                            RegionPreset.GLOBAL_UNIVERSAL -> "Global • DoH ${settings.dohProvider.plainIp}"
                        },
                        accent = if (isConnected) c.success else c.accentRed
                    )
                    InfoPill(
                        label = if (settings.region == RegionPreset.BRAZIL) {
                            "Discord Only"
                        } else {
                            when (settings.bypassProfile) {
                                BypassProfile.AUTO_UNIVERSAL -> "7-Stage Global"
                                BypassProfile.RUSSIA_TSPU -> "Russia TSPU"
                                BypassProfile.CLASSIC_BYEDPI -> "Classic"
                                BypassProfile.STRICT_ISP -> "Strict ISP"
                                BypassProfile.CUSTOM -> "Custom"
                            }
                        },
                        accent = c.textSecondary
                    )
                }

                // Live Metrics Row when Connected
                AnimatedVisibility(visible = isConnected) {
                    Column {
                        Spacer(modifier = Modifier.height(16.dp))
                        val uptimeSec = ((nowMs - vpnState.connectedSinceMs) / 1000L).coerceAtLeast(0L)
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(14.dp))
                                .background(c.bgCardAlt)
                                .border(1.dp, c.borderSubtle, RoundedCornerShape(14.dp))
                                .padding(vertical = 10.dp, horizontal = 8.dp),
                            horizontalArrangement = Arrangement.SpaceEvenly
                        ) {
                            MetricItem(strings.metricDownload, formatBytes(vpnState.rxBytes), c.success)
                            MetricItem(strings.metricUpload, formatBytes(vpnState.txBytes), c.accentRed)
                            MetricItem(
                                strings.metricDnsQueries,
                                "${vpnState.dnsQueries}",
                                c.textPrimary
                            )
                            MetricItem(strings.metricUptime, formatUptime(uptimeSec), c.textPrimary)
                        }
                    }
                }

                Spacer(modifier = Modifier.height(20.dp))

                // Primary Connect / Disconnect Gradient Button
                val btnColors = when {
                    isConnected -> listOf(Color(0xFF00C864), Color(0xFF008F47))
                    isConnecting -> listOf(c.accentRedDeep, c.accentRedDeep)
                    else -> listOf(c.accentRed, c.accentRedDeep)
                }
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(54.dp)
                        .clip(RoundedCornerShape(15.dp))
                        .background(Brush.horizontalGradient(btnColors))
                        .clickable(enabled = !isConnecting) { onToggleConnection() },
                    contentAlignment = Alignment.Center
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.Center
                    ) {
                        if (isConnecting) {
                            CircularProgressIndicator(
                                color = Color.White,
                                strokeWidth = 2.5.dp,
                                modifier = Modifier.size(20.dp)
                            )
                        } else {
                            Icon(
                                imageVector = Icons.Rounded.PowerSettingsNew,
                                contentDescription = null,
                                tint = Color.White,
                                modifier = Modifier.size(22.dp)
                            )
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(
                            text = when {
                                isConnected -> strings.btnDisconnect
                                isConnecting -> strings.btnConnecting
                                else -> strings.btnConnect
                            },
                            color = Color.White,
                            fontSize = 15.5.sp,
                            fontWeight = FontWeight.ExtraBold
                        )
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))
                Text(
                    text = strings.localOnlyNotice,
                    color = c.textMuted,
                    fontSize = 10.5.sp,
                    textAlign = TextAlign.Center
                )
            }
        }

        // Unblock & Speed Self-Test Card (exact parity with Windows!)
        Surface(
            shape = RoundedCornerShape(20.dp),
            color = c.bgCard,
            border = BorderStroke(1.dp, c.borderSubtle)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(
                                imageVector = Icons.Rounded.Speed,
                                contentDescription = null,
                                tint = c.accentRed,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                text = strings.selfTestTitle,
                                color = c.textPrimary,
                                fontSize = 15.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                        Spacer(modifier = Modifier.height(3.dp))
                        Text(
                            text = strings.selfTestSubtitle,
                            color = c.textSecondary,
                            fontSize = 12.sp,
                            lineHeight = 16.sp
                        )
                    }

                    Spacer(modifier = Modifier.width(10.dp))

                    OutlinedButton(
                        onClick = onRunSelfTest,
                        enabled = !isTesting,
                        shape = RoundedCornerShape(10.dp),
                        border = BorderStroke(1.dp, c.accentRed.copy(alpha = 0.65f)),
                        colors = ButtonDefaults.outlinedButtonColors(
                            containerColor = c.bgCardAlt,
                            contentColor = c.textPrimary
                        ),
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 8.dp)
                    ) {
                        if (isTesting) {
                            CircularProgressIndicator(
                                color = c.accentRed,
                                strokeWidth = 2.dp,
                                modifier = Modifier.size(14.dp)
                            )
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(strings.btnTesting, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        } else {
                            Icon(
                                imageVector = Icons.Rounded.PlayArrow,
                                contentDescription = null,
                                tint = c.accentRed,
                                modifier = Modifier.size(16.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(strings.btnRunTest, fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        }
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))

                if (selfTestResults.isEmpty()) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(12.dp))
                            .background(c.bgCardAlt)
                            .padding(14.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = strings.selfTestEmptyHint,
                            color = c.textMuted,
                            fontSize = 12.sp,
                            textAlign = TextAlign.Center
                        )
                    }
                } else {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        for (item in selfTestResults) {
                            SelfTestRow(item)
                        }
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(8.dp))
    }
}

@Composable
private fun SelfTestRow(item: SelfTestItem) {
    val c = LocalRyanDpiColors.current
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(c.bgCardAlt)
            .border(
                width = 1.dp,
                color = if (item.passed) c.success.copy(alpha = 0.25f) else c.danger.copy(alpha = 0.28f),
                shape = RoundedCornerShape(12.dp)
            )
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(9.dp)
                .clip(CircleShape)
                .background(if (item.passed) c.success else c.danger)
        )
        Spacer(modifier = Modifier.width(10.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = item.name,
                color = c.textPrimary,
                fontSize = 13.5.sp,
                fontWeight = FontWeight.Bold,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Text(
                text = item.detail,
                color = c.textSecondary,
                fontSize = 11.5.sp,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
        }
        Spacer(modifier = Modifier.width(8.dp))
        Text(
            text = if (item.passed && item.latencyMs > 0) "${item.latencyMs} ms" else if (item.passed) "OK" else "Failed",
            color = if (item.passed) c.success else c.danger,
            fontSize = 12.5.sp,
            fontWeight = FontWeight.ExtraBold
        )
    }
}

@Composable
private fun SettingsTab(
    settings: RyanDpiSettings,
    vpnState: VpnUiState,
    strings: AppStrings,
    settingsDirtyWhileConnected: Boolean,
    onUpdateSettings: (RyanDpiSettings, Boolean) -> Unit,
    onReconnectApply: () -> Unit,
    onOpenPrivateDnsHelper: () -> Unit,
    onOpenExcludedApps: () -> Unit
) {
    val c = LocalRyanDpiColors.current
    var customDraft by remember(settings.customStrategyArgs) { mutableStateOf(settings.customStrategyArgs) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 10.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        // Apply & Reconnect notice if changed while active
        AnimatedVisibility(visible = settingsDirtyWhileConnected && vpnState.status == VpnStatus.CONNECTED) {
            Surface(
                shape = RoundedCornerShape(14.dp),
                color = c.dangerBg,
                border = BorderStroke(1.dp, c.accentRed)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(12.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = strings.reconnectToApplyHint,
                        color = c.textPrimary,
                        fontSize = 12.sp,
                        modifier = Modifier.weight(1f)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Button(
                        onClick = onReconnectApply,
                        colors = ButtonDefaults.buttonColors(containerColor = c.accentRed),
                        shape = RoundedCornerShape(10.dp),
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp)
                    ) {
                        Icon(Icons.Rounded.Refresh, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(strings.btnApplyReconnect, fontSize = 11.5.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }
        }

        // 0. Country / Region Mode Card
        SettingsCard(
            title = strings.settingsRegionTitle,
            subtitle = strings.regionPickerSub,
            icon = Icons.Rounded.Public
        ) {
            RadioOptionRow(
                selected = settings.region == RegionPreset.GLOBAL_UNIVERSAL,
                title = strings.regionGlobalTitle,
                description = strings.regionGlobalDesc,
                onClick = { onUpdateSettings(settings.withRegionPreset(RegionPreset.GLOBAL_UNIVERSAL), true) }
            )
            Spacer(modifier = Modifier.height(6.dp))
            RadioOptionRow(
                selected = settings.region == RegionPreset.TURKEY,
                title = strings.regionTurkeyTitle,
                description = strings.regionTurkeyDesc,
                onClick = { onUpdateSettings(settings.withRegionPreset(RegionPreset.TURKEY), true) }
            )
            Spacer(modifier = Modifier.height(6.dp))
            RadioOptionRow(
                selected = settings.region == RegionPreset.RUSSIA,
                title = strings.regionRussiaTitle,
                description = strings.regionRussiaDesc,
                onClick = { onUpdateSettings(settings.withRegionPreset(RegionPreset.RUSSIA), true) }
            )
            Spacer(modifier = Modifier.height(6.dp))
            RadioOptionRow(
                selected = settings.region == RegionPreset.BRAZIL,
                title = strings.regionBrazilTitle,
                description = strings.regionBrazilDesc,
                onClick = { onUpdateSettings(settings.withRegionPreset(RegionPreset.BRAZIL), true) }
            )
        }

        // Brazil Discord-Only Split-Tunnel Card
        AnimatedVisibility(visible = settings.region == RegionPreset.BRAZIL) {
            var epDraft by remember(settings.customSocksEndpoint) { mutableStateOf(settings.customSocksEndpoint) }
            var userDraft by remember(settings.customSocksUsername) { mutableStateOf(settings.customSocksUsername) }
            var passDraft by remember(settings.customSocksPassword) { mutableStateOf(settings.customSocksPassword) }

            SettingsCard(
                title = strings.settingsBrazilCardTitle,
                subtitle = strings.settingsBrazilNotice,
                icon = Icons.Rounded.Security
            ) {
                RadioOptionRow(
                    selected = settings.brazilTunnel == BrazilTunnelMode.BUILTIN_WARP_MASQUE,
                    title = strings.brazilTunnelWarpTitle,
                    description = strings.brazilTunnelWarpDesc,
                    onClick = { onUpdateSettings(settings.copy(brazilTunnel = BrazilTunnelMode.BUILTIN_WARP_MASQUE), true) }
                )
                Spacer(modifier = Modifier.height(6.dp))
                RadioOptionRow(
                    selected = settings.brazilTunnel == BrazilTunnelMode.CUSTOM_SOCKS5,
                    title = strings.brazilTunnelCustomTitle,
                    description = strings.brazilTunnelCustomDesc,
                    onClick = { onUpdateSettings(settings.copy(brazilTunnel = BrazilTunnelMode.CUSTOM_SOCKS5), true) }
                )

                AnimatedVisibility(visible = settings.brazilTunnel == BrazilTunnelMode.CUSTOM_SOCKS5) {
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.padding(top = 10.dp)) {
                        OutlinedTextField(
                            value = epDraft,
                            onValueChange = {
                                epDraft = it
                                onUpdateSettings(settings.copy(customSocksEndpoint = it.trim()), true)
                            },
                            label = { Text(strings.brazilCustomEndpointLabel, fontSize = 12.sp) },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedBorderColor = c.accentRed,
                                unfocusedBorderColor = c.borderSubtle,
                                focusedContainerColor = c.bgInput,
                                unfocusedContainerColor = c.bgInput,
                                focusedTextColor = c.textPrimary,
                                unfocusedTextColor = c.textPrimary
                            )
                        )
                        OutlinedTextField(
                            value = userDraft,
                            onValueChange = {
                                userDraft = it
                                onUpdateSettings(settings.copy(customSocksUsername = it.trim()), true)
                            },
                            label = { Text(strings.brazilCustomUserLabel, fontSize = 12.sp) },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedBorderColor = c.accentRed,
                                unfocusedBorderColor = c.borderSubtle,
                                focusedContainerColor = c.bgInput,
                                unfocusedContainerColor = c.bgInput,
                                focusedTextColor = c.textPrimary,
                                unfocusedTextColor = c.textPrimary
                            )
                        )
                        OutlinedTextField(
                            value = passDraft,
                            onValueChange = {
                                passDraft = it
                                onUpdateSettings(settings.copy(customSocksPassword = it), true)
                            },
                            label = { Text(strings.brazilCustomPassLabel, fontSize = 12.sp) },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth(),
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedBorderColor = c.accentRed,
                                unfocusedBorderColor = c.borderSubtle,
                                focusedContainerColor = c.bgInput,
                                unfocusedContainerColor = c.bgInput,
                                focusedTextColor = c.textPrimary,
                                unfocusedTextColor = c.textPrimary
                            )
                        )
                    }
                }

                Spacer(modifier = Modifier.height(10.dp))
                ToggleSettingRow(
                    title = strings.brazilBrowsersTitle,
                    description = strings.brazilBrowsersDesc,
                    checked = settings.brazilIncludeBrowsers,
                    onCheckedChange = { onUpdateSettings(settings.copy(brazilIncludeBrowsers = it), true) }
                )
            }
        }

        // 1. DNS Poisoning Protection Card (Global / Turkey / Russia)
        if (settings.region != RegionPreset.BRAZIL) SettingsCard(
            title = strings.settingsDnsCardTitle,
            subtitle = strings.settingsDnsCardSub,
            icon = Icons.Rounded.Dns
        ) {
            RadioOptionRow(
                selected = settings.dnsMode == DnsMode.ENCRYPTED_DOH,
                title = strings.dnsModeEncryptedTitle,
                description = strings.dnsModeEncryptedDesc,
                onClick = { onUpdateSettings(settings.copy(dnsMode = DnsMode.ENCRYPTED_DOH), true) }
            )
            Spacer(modifier = Modifier.height(6.dp))
            RadioOptionRow(
                selected = settings.dnsMode == DnsMode.DIRECT_NATIVE_DNS,
                title = strings.dnsModeDirectTitle,
                description = strings.dnsModeDirectDesc,
                onClick = { onUpdateSettings(settings.copy(dnsMode = DnsMode.DIRECT_NATIVE_DNS), true) }
            )

            Spacer(modifier = Modifier.height(12.dp))
            Text(
                text = strings.dohProviderLabel,
                color = c.textSecondary,
                fontSize = 12.sp,
                fontWeight = FontWeight.SemiBold
            )
            Spacer(modifier = Modifier.height(8.dp))
            Row(
                modifier = Modifier.horizontalScroll(rememberScrollState()),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                DohProvider.entries.forEach { provider ->
                    val active = settings.dohProvider == provider
                    Surface(
                        onClick = { onUpdateSettings(settings.copy(dohProvider = provider), true) },
                        shape = RoundedCornerShape(10.dp),
                        color = if (active) c.accentRed.copy(alpha = 0.18f) else c.bgCardAlt,
                        border = BorderStroke(1.dp, if (active) c.accentRed else c.borderSubtle)
                    ) {
                        Text(
                            text = provider.displayName,
                            color = if (active) c.accentRed else c.textPrimary,
                            fontSize = 12.sp,
                            fontWeight = if (active) FontWeight.Bold else FontWeight.Medium,
                            modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp)
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(12.dp))
            OutlinedButton(
                onClick = onOpenPrivateDnsHelper,
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(10.dp),
                border = BorderStroke(1.dp, c.borderSubtle),
                colors = ButtonDefaults.outlinedButtonColors(
                    containerColor = c.bgCardAlt,
                    contentColor = c.textPrimary
                )
            ) {
                Icon(Icons.Rounded.Security, contentDescription = null, tint = c.accentRed, modifier = Modifier.size(16.dp))
                Spacer(modifier = Modifier.width(8.dp))
                Text(strings.btnPrivateDnsHelper, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
            }
        }

        // 2. DPI Bypass Strategy Card (Global / Turkey / Russia)
        if (settings.region != RegionPreset.BRAZIL) SettingsCard(
            title = strings.settingsProfileTitle,
            subtitle = strings.settingsProfileSub,
            icon = Icons.Rounded.Tune
        ) {
            RadioOptionRow(
                selected = settings.bypassProfile == BypassProfile.AUTO_UNIVERSAL,
                title = strings.profileAutoTitle,
                description = strings.profileAutoDesc,
                onClick = { onUpdateSettings(settings.copy(bypassProfile = BypassProfile.AUTO_UNIVERSAL), true) }
            )
            Spacer(modifier = Modifier.height(6.dp))
            RadioOptionRow(
                selected = settings.bypassProfile == BypassProfile.RUSSIA_TSPU,
                title = strings.profileRussiaTitle,
                description = strings.profileRussiaDesc,
                onClick = { onUpdateSettings(settings.copy(bypassProfile = BypassProfile.RUSSIA_TSPU), true) }
            )
            Spacer(modifier = Modifier.height(6.dp))
            RadioOptionRow(
                selected = settings.bypassProfile == BypassProfile.CLASSIC_BYEDPI,
                title = strings.profileClassicTitle,
                description = strings.profileClassicDesc,
                onClick = { onUpdateSettings(settings.copy(bypassProfile = BypassProfile.CLASSIC_BYEDPI), true) }
            )
            Spacer(modifier = Modifier.height(6.dp))
            RadioOptionRow(
                selected = settings.bypassProfile == BypassProfile.STRICT_ISP,
                title = strings.profileStrictTitle,
                description = strings.profileStrictDesc,
                onClick = { onUpdateSettings(settings.copy(bypassProfile = BypassProfile.STRICT_ISP), true) }
            )
            Spacer(modifier = Modifier.height(6.dp))
            RadioOptionRow(
                selected = settings.bypassProfile == BypassProfile.CUSTOM,
                title = strings.profileCustomTitle,
                description = strings.profileCustomDesc,
                onClick = { onUpdateSettings(settings.copy(bypassProfile = BypassProfile.CUSTOM), true) }
            )

            AnimatedVisibility(visible = settings.bypassProfile == BypassProfile.CUSTOM) {
                Column {
                    Spacer(modifier = Modifier.height(10.dp))
                    OutlinedTextField(
                        value = customDraft,
                        onValueChange = {
                            customDraft = it
                            onUpdateSettings(settings.copy(customStrategyArgs = it), true)
                        },
                        label = { Text(strings.customArgsLabel, fontSize = 12.sp) },
                        modifier = Modifier.fillMaxWidth(),
                        minLines = 2,
                        maxLines = 4,
                        textStyle = androidx.compose.ui.text.TextStyle(
                            fontFamily = FontFamily.Monospace,
                            fontSize = 12.sp,
                            color = c.textPrimary
                        ),
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedBorderColor = c.accentRed,
                            unfocusedBorderColor = c.borderSubtle,
                            focusedContainerColor = c.bgInput,
                            unfocusedContainerColor = c.bgInput
                        )
                    )
                }
            }
        }

        // 3. Network & Behavior Card
        SettingsCard(
            title = strings.settingsNetworkTitle,
            subtitle = null,
            icon = Icons.Rounded.Security
        ) {
            ToggleSettingRow(
                title = strings.toggleUdpTitle,
                description = strings.toggleUdpDesc,
                checked = settings.udpBypass,
                onCheckedChange = { onUpdateSettings(settings.copy(udpBypass = it), true) }
            )
            HorizontalDivider(color = c.borderSubtle, modifier = Modifier.padding(vertical = 8.dp))
            ToggleSettingRow(
                title = strings.toggleIpv6Title,
                description = strings.toggleIpv6Desc,
                checked = settings.ipv6Enabled,
                onCheckedChange = { onUpdateSettings(settings.copy(ipv6Enabled = it), true) }
            )
            HorizontalDivider(color = c.borderSubtle, modifier = Modifier.padding(vertical = 8.dp))
            ToggleSettingRow(
                title = strings.toggleBootTitle,
                description = strings.toggleBootDesc,
                checked = settings.autoStartOnBoot,
                onCheckedChange = { onUpdateSettings(settings.copy(autoStartOnBoot = it), false) }
            )
            HorizontalDivider(color = c.borderSubtle, modifier = Modifier.padding(vertical = 8.dp))
            ToggleSettingRow(
                title = strings.toggleAutoUpdateTitle,
                description = strings.toggleAutoUpdateDesc,
                checked = settings.autoCheckUpdates,
                onCheckedChange = { onUpdateSettings(settings.copy(autoCheckUpdates = it), false) }
            )
        }

        // 4. Excluded Apps (Split Tunnel) Card
        SettingsCard(
            title = strings.excludedAppsTitle,
            subtitle = strings.excludedAppsSub,
            icon = Icons.Rounded.Apps
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                InfoPill(
                    label = "${settings.excludedPackages.size} ${strings.excludedAppsCount}",
                    accent = c.accentRed
                )
                Button(
                    onClick = onOpenExcludedApps,
                    colors = ButtonDefaults.buttonColors(containerColor = c.accentRed),
                    shape = RoundedCornerShape(10.dp),
                    contentPadding = PaddingValues(horizontal = 14.dp, vertical = 8.dp)
                ) {
                    Text(strings.btnManageApps, fontSize = 12.5.sp, fontWeight = FontWeight.Bold)
                }
            }
        }

        Spacer(modifier = Modifier.height(8.dp))
    }
}

@Composable
private fun ToolsTab(
    strings: AppStrings,
    logs: List<LogEntry>,
    updateStatusMsg: String?,
    onCheckUpdates: () -> Unit,
    onCopyLogs: () -> Unit,
    onClearLogs: () -> Unit,
    onOpenGithub: () -> Unit
) {
    val c = LocalRyanDpiColors.current

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 10.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        // Quick Actions Card
        SettingsCard(
            title = strings.toolsActionsTitle,
            subtitle = updateStatusMsg,
            icon = Icons.Rounded.Download
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(
                    onClick = onCheckUpdates,
                    modifier = Modifier.weight(1f),
                    colors = ButtonDefaults.buttonColors(containerColor = c.accentRed),
                    shape = RoundedCornerShape(10.dp),
                    contentPadding = PaddingValues(horizontal = 10.dp, vertical = 10.dp)
                ) {
                    Icon(Icons.Rounded.SystemUpdate, contentDescription = null, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(strings.btnCheckUpdates, fontSize = 12.sp, fontWeight = FontWeight.Bold, maxLines = 1)
                }

                OutlinedButton(
                    onClick = onOpenGithub,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(10.dp),
                    border = BorderStroke(1.dp, c.borderSubtle),
                    colors = ButtonDefaults.outlinedButtonColors(
                        containerColor = c.bgCardAlt,
                        contentColor = c.textPrimary
                    ),
                    contentPadding = PaddingValues(horizontal = 10.dp, vertical = 10.dp)
                ) {
                    Icon(Icons.AutoMirrored.Rounded.OpenInNew, contentDescription = null, tint = c.accentRed, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(strings.btnOpenGithub, fontSize = 12.sp, fontWeight = FontWeight.SemiBold, maxLines = 1)
                }
            }
        }

        // Quick Settings Tile Tip Card
        Surface(
            shape = RoundedCornerShape(16.dp),
            color = c.bgCard,
            border = BorderStroke(1.dp, c.borderSubtle)
        ) {
            Column(modifier = Modifier.padding(14.dp)) {
                Text(
                    text = strings.quickTileTipTitle,
                    color = c.accentRed,
                    fontSize = 13.5.sp,
                    fontWeight = FontWeight.Bold
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = strings.quickTileTipDesc,
                    color = c.textSecondary,
                    fontSize = 12.sp,
                    lineHeight = 17.sp
                )
            }
        }

        // Live Activity Log Console
        Surface(
            shape = RoundedCornerShape(18.dp),
            color = c.bgCard,
            border = BorderStroke(1.dp, c.borderSubtle)
        ) {
            Column(modifier = Modifier.padding(14.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = strings.liveLogsTitle,
                        color = c.textPrimary,
                        fontSize = 14.5.sp,
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier.weight(1f)
                    )
                    IconButton(onClick = onCopyLogs, modifier = Modifier.size(34.dp)) {
                        Icon(
                            imageVector = Icons.Rounded.ContentCopy,
                            contentDescription = strings.btnCopyLogs,
                            tint = c.textSecondary,
                            modifier = Modifier.size(18.dp)
                        )
                    }
                    IconButton(onClick = onClearLogs, modifier = Modifier.size(34.dp)) {
                        Icon(
                            imageVector = Icons.Rounded.DeleteOutline,
                            contentDescription = strings.btnClearLogs,
                            tint = c.textSecondary,
                            modifier = Modifier.size(18.dp)
                        )
                    }
                }

                Spacer(modifier = Modifier.height(8.dp))

                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = 220.dp, max = 380.dp)
                        .clip(RoundedCornerShape(12.dp))
                        .background(c.bgInput)
                        .border(1.dp, c.borderSubtle, RoundedCornerShape(12.dp))
                        .padding(10.dp)
                ) {
                    if (logs.isEmpty()) {
                        Text(
                            text = strings.logsEmpty,
                            color = c.textMuted,
                            fontSize = 12.sp,
                            fontFamily = FontFamily.Monospace
                        )
                    } else {
                        LazyColumn(
                            verticalArrangement = Arrangement.spacedBy(4.dp),
                            reverseLayout = true
                        ) {
                            items(logs.asReversed()) { entry ->
                                val tagColor = when (entry.level) {
                                    LogLevel.ERROR -> c.danger
                                    LogLevel.WARN -> c.warning
                                    LogLevel.INFO -> c.success
                                }
                                Row {
                                    Text(
                                        text = "${entry.timestamp} ",
                                        color = c.textMuted,
                                        fontSize = 10.5.sp,
                                        fontFamily = FontFamily.Monospace
                                    )
                                    Text(
                                        text = "[${entry.level.name.first()}] ",
                                        color = tagColor,
                                        fontSize = 10.5.sp,
                                        fontWeight = FontWeight.Bold,
                                        fontFamily = FontFamily.Monospace
                                    )
                                    Text(
                                        text = entry.message,
                                        color = c.textPrimary,
                                        fontSize = 11.sp,
                                        fontFamily = FontFamily.Monospace,
                                        lineHeight = 15.sp
                                    )
                                }
                            }
                        }
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(8.dp))
    }
}

@Composable
private fun RegionPickerDialog(
    strings: AppStrings,
    selectedRegion: RegionPreset,
    onSelectRegion: (RegionPreset) -> Unit,
    onDismiss: () -> Unit
) {
    val c = LocalRyanDpiColors.current
    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Surface(
            modifier = Modifier
                .fillMaxWidth(0.92f)
                .heightIn(max = 620.dp),
            shape = RoundedCornerShape(22.dp),
            color = c.bgCard,
            border = BorderStroke(1.2.dp, c.accentRed.copy(alpha = 0.7f))
        ) {
            Column(
                modifier = Modifier
                    .verticalScroll(rememberScrollState())
                    .padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Text(
                    text = strings.regionPickerTitle,
                    color = c.textPrimary,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.ExtraBold
                )
                Text(
                    text = strings.regionPickerSub,
                    color = c.textSecondary,
                    fontSize = 12.sp,
                    lineHeight = 16.5.sp
                )
                Spacer(modifier = Modifier.height(2.dp))
                RadioOptionRow(
                    selected = selectedRegion == RegionPreset.GLOBAL_UNIVERSAL,
                    title = strings.regionGlobalTitle,
                    description = strings.regionGlobalDesc,
                    onClick = { onSelectRegion(RegionPreset.GLOBAL_UNIVERSAL) }
                )
                RadioOptionRow(
                    selected = selectedRegion == RegionPreset.TURKEY,
                    title = strings.regionTurkeyTitle,
                    description = strings.regionTurkeyDesc,
                    onClick = { onSelectRegion(RegionPreset.TURKEY) }
                )
                RadioOptionRow(
                    selected = selectedRegion == RegionPreset.RUSSIA,
                    title = strings.regionRussiaTitle,
                    description = strings.regionRussiaDesc,
                    onClick = { onSelectRegion(RegionPreset.RUSSIA) }
                )
                RadioOptionRow(
                    selected = selectedRegion == RegionPreset.BRAZIL,
                    title = strings.regionBrazilTitle,
                    description = strings.regionBrazilDesc,
                    onClick = { onSelectRegion(RegionPreset.BRAZIL) }
                )
                Spacer(modifier = Modifier.height(4.dp))
                Button(
                    onClick = onDismiss,
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(containerColor = c.accentRed),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Text(strings.btnDone, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@Composable
private fun LanguagePickerDialog(
    strings: AppStrings,
    selectedLanguage: String,
    onSelectLanguage: (String) -> Unit,
    onDismiss: () -> Unit
) {
    val c = LocalRyanDpiColors.current
    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Surface(
            modifier = Modifier
                .fillMaxWidth(0.92f)
                .heightIn(max = 580.dp),
            shape = RoundedCornerShape(22.dp),
            color = c.bgCard,
            border = BorderStroke(1.2.dp, c.accentRed.copy(alpha = 0.7f))
        ) {
            Column(
                modifier = Modifier
                    .verticalScroll(rememberScrollState())
                    .padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Text(
                    text = strings.languagePickerTitle,
                    color = c.textPrimary,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.ExtraBold
                )
                Spacer(modifier = Modifier.height(2.dp))
                StringsRepository.allLanguages.forEach { lang ->
                    val isSelected = lang.code == selectedLanguage
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(12.dp))
                            .background(if (isSelected) c.accentRed.copy(alpha = 0.14f) else c.bgCardAlt)
                            .border(
                                width = 1.dp,
                                color = if (isSelected) c.accentRed else c.borderSubtle,
                                shape = RoundedCornerShape(12.dp)
                            )
                            .clickable { onSelectLanguage(lang.code) }
                            .padding(horizontal = 12.dp, vertical = 11.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Box(
                            modifier = Modifier
                                .clip(RoundedCornerShape(6.dp))
                                .background(if (isSelected) c.accentRed else c.bgCanvas)
                                .padding(horizontal = 7.dp, vertical = 3.dp)
                        ) {
                            Text(
                                text = lang.shortLabel,
                                color = if (isSelected) Color.White else c.accentRed,
                                fontSize = 11.sp,
                                fontWeight = FontWeight.ExtraBold
                            )
                        }
                        Spacer(modifier = Modifier.width(12.dp))
                        Text(
                            text = lang.nativeName,
                            color = c.textPrimary,
                            fontSize = 14.sp,
                            fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Medium,
                            modifier = Modifier.weight(1f)
                        )
                        if (isSelected) {
                            Icon(
                                imageVector = Icons.Rounded.CheckCircle,
                                contentDescription = null,
                                tint = c.accentRed,
                                modifier = Modifier.size(18.dp)
                            )
                        }
                    }
                }
                Spacer(modifier = Modifier.height(4.dp))
                OutlinedButton(
                    onClick = onDismiss,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp),
                    border = BorderStroke(1.dp, c.borderSubtle)
                ) {
                    Text(strings.btnDone, color = c.textPrimary, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@Composable
private fun ExcludedAppsDialog(
    strings: AppStrings,
    installedApps: List<InstalledAppItem>,
    excludedPackages: Set<String>,
    onToggleApp: (String) -> Unit,
    onDismiss: () -> Unit
) {
    val c = LocalRyanDpiColors.current
    var query by remember { mutableStateOf("") }
    val filtered = remember(installedApps, query, excludedPackages) {
        val q = query.trim().lowercase(Locale.ROOT)
        installedApps
            .filter {
                q.isEmpty() ||
                    it.appName.lowercase(Locale.ROOT).contains(q) ||
                    it.packageName.lowercase(Locale.ROOT).contains(q)
            }
            .sortedWith(
                compareByDescending<InstalledAppItem> { excludedPackages.contains(it.packageName) }
                    .thenBy { it.appName.lowercase(Locale.ROOT) }
            )
    }

    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Surface(
            modifier = Modifier
                .fillMaxWidth(0.92f)
                .heightIn(max = 580.dp),
            shape = RoundedCornerShape(20.dp),
            color = c.bgCard,
            border = BorderStroke(1.dp, c.borderSubtle)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(
                    text = strings.excludedAppsTitle,
                    color = c.textPrimary,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold
                )
                Spacer(modifier = Modifier.height(10.dp))

                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    placeholder = { Text(strings.searchAppsHint, fontSize = 13.sp, color = c.textMuted) },
                    leadingIcon = { Icon(Icons.Rounded.Search, contentDescription = null, tint = c.textMuted) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = c.accentRed,
                        unfocusedBorderColor = c.borderSubtle,
                        focusedContainerColor = c.bgInput,
                        unfocusedContainerColor = c.bgInput,
                        focusedTextColor = c.textPrimary,
                        unfocusedTextColor = c.textPrimary
                    )
                )

                Spacer(modifier = Modifier.height(10.dp))

                LazyColumn(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(4.dp)
                ) {
                    items(filtered, key = { it.packageName }) { app ->
                        val checked = excludedPackages.contains(app.packageName)
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(10.dp))
                                .background(if (checked) c.accentRed.copy(alpha = 0.10f) else c.bgCardAlt)
                                .clickable { onToggleApp(app.packageName) }
                                .padding(horizontal = 10.dp, vertical = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Checkbox(
                                checked = checked,
                                onCheckedChange = { onToggleApp(app.packageName) },
                                colors = CheckboxDefaults.colors(checkedColor = c.accentRed)
                            )
                            Spacer(modifier = Modifier.width(6.dp))
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = app.appName,
                                    color = c.textPrimary,
                                    fontSize = 13.5.sp,
                                    fontWeight = FontWeight.SemiBold,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                                Text(
                                    text = app.packageName,
                                    color = c.textMuted,
                                    fontSize = 11.sp,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))
                Button(
                    onClick = onDismiss,
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(containerColor = c.accentRed),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Text(strings.btnDone, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@Composable
private fun SettingsCard(
    title: String,
    subtitle: String?,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    content: @Composable () -> Unit
) {
    val c = LocalRyanDpiColors.current
    Surface(
        shape = RoundedCornerShape(18.dp),
        color = c.bgCard,
        border = BorderStroke(1.dp, c.borderSubtle)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(icon, contentDescription = null, tint = c.accentRed, modifier = Modifier.size(18.dp))
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = title,
                    color = c.textPrimary,
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Bold
                )
            }
            if (!subtitle.isNullOrBlank()) {
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = subtitle,
                    color = c.textSecondary,
                    fontSize = 12.sp,
                    lineHeight = 16.sp
                )
            }
            Spacer(modifier = Modifier.height(12.dp))
            content()
        }
    }
}

@Composable
private fun RadioOptionRow(
    selected: Boolean,
    title: String,
    description: String,
    onClick: () -> Unit
) {
    val c = LocalRyanDpiColors.current
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(if (selected) c.accentRed.copy(alpha = 0.10f) else c.bgCardAlt)
            .border(
                width = 1.dp,
                color = if (selected) c.accentRed.copy(alpha = 0.7f) else c.borderSubtle,
                shape = RoundedCornerShape(12.dp)
            )
            .clickable { onClick() }
            .padding(horizontal = 10.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        RadioButton(
            selected = selected,
            onClick = onClick,
            colors = RadioButtonDefaults.colors(
                selectedColor = c.accentRed,
                unselectedColor = c.textMuted
            )
        )
        Spacer(modifier = Modifier.width(6.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                color = c.textPrimary,
                fontSize = 13.5.sp,
                fontWeight = FontWeight.Bold
            )
            Text(
                text = description,
                color = c.textSecondary,
                fontSize = 11.5.sp,
                lineHeight = 15.5.sp
            )
        }
    }
}

@Composable
private fun ToggleSettingRow(
    title: String,
    description: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit
) {
    val c = LocalRyanDpiColors.current
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                color = c.textPrimary,
                fontSize = 13.5.sp,
                fontWeight = FontWeight.SemiBold
            )
            Text(
                text = description,
                color = c.textSecondary,
                fontSize = 11.5.sp,
                lineHeight = 15.sp
            )
        }
        Spacer(modifier = Modifier.width(10.dp))
        Switch(
            checked = checked,
            onCheckedChange = onCheckedChange,
            colors = SwitchDefaults.colors(
                checkedThumbColor = Color.White,
                checkedTrackColor = c.accentRed,
                uncheckedThumbColor = c.textSecondary,
                uncheckedTrackColor = c.bgCardAlt
            )
        )
    }
}

@Composable
private fun InfoPill(label: String, accent: Color) {
    val c = LocalRyanDpiColors.current
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(8.dp))
            .background(c.bgCardAlt)
            .border(1.dp, accent.copy(alpha = 0.4f), RoundedCornerShape(8.dp))
            .padding(horizontal = 9.dp, vertical = 4.dp)
    ) {
        Text(
            text = label,
            color = accent,
            fontSize = 11.sp,
            fontWeight = FontWeight.Bold,
            maxLines = 1
        )
    }
}

@Composable
private fun MetricItem(label: String, value: String, valueColor: Color) {
    val c = LocalRyanDpiColors.current
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(
            text = label,
            color = c.textMuted,
            fontSize = 9.5.sp,
            fontWeight = FontWeight.Bold,
            letterSpacing = 0.4.sp
        )
        Spacer(modifier = Modifier.height(2.dp))
        Text(
            text = value,
            color = valueColor,
            fontSize = 13.sp,
            fontWeight = FontWeight.ExtraBold
        )
    }
}

private fun formatBytes(bytes: Long): String {
    if (bytes < 1024L) return "$bytes B"
    val kb = bytes / 1024.0
    if (kb < 1024.0) return String.format(Locale.US, "%.1f KB", kb)
    val mb = kb / 1024.0
    if (mb < 1024.0) return String.format(Locale.US, "%.1f MB", mb)
    return String.format(Locale.US, "%.2f GB", mb / 1024.0)
}

private fun formatUptime(seconds: Long): String {
    val h = seconds / 3600
    val m = (seconds % 3600) / 60
    val s = seconds % 60
    return if (h > 0) String.format(Locale.US, "%d:%02d:%02d", h, m, s)
    else String.format(Locale.US, "%02d:%02d", m, s)
}
