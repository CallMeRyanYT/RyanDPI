package com.ryandpi.android.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.net.VpnService
import com.ryandpi.android.engine.LogRepository
import com.ryandpi.android.engine.RyanDpiSettings

class BootReceiver : BroadcastReceiver() {
    companion object {
        const val ACTION_REVIVE = "com.ryandpi.android.action.REVIVE_PERMANENT"
    }

    override fun onReceive(context: Context, intent: Intent?) {
        val action = intent?.action ?: return
        val isBoot = action == Intent.ACTION_BOOT_COMPLETED ||
            action == Intent.ACTION_LOCKED_BOOT_COMPLETED ||
            action == "android.intent.action.QUICKBOOT_POWERON"
        val isUpdate = action == Intent.ACTION_MY_PACKAGE_REPLACED
        val isRevive = action == ACTION_REVIVE

        if (!isBoot && !isUpdate && !isRevive) return

        val settings = RyanDpiSettings.load(context)
        val userWants = RyanDpiSettings.getUserWantsConnected(context)

        val shouldStart = when {
            isRevive -> settings.permanentAlwaysOn && userWants
            isUpdate -> settings.permanentAlwaysOn || settings.autoStartOnBoot || userWants
            isBoot -> settings.permanentAlwaysOn || settings.autoStartOnBoot
            else -> false
        }
        if (!shouldStart) return

        // Only auto-start if the user has already granted Android VPN permission
        if (VpnService.prepare(context) == null) {
            LogRepository.info("Auto-starting RyanDPI ($action, permanent=${settings.permanentAlwaysOn}).")
            runCatching { RyanDpiVpnService.startService(context) }
        } else {
            LogRepository.warn("Auto-start skipped: Android VPN permission requires user confirmation.")
        }
    }
}
