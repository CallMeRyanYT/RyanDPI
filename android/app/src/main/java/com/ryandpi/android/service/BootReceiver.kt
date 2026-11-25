package com.ryandpi.android.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.net.VpnService
import com.ryandpi.android.engine.LogRepository
import com.ryandpi.android.engine.RyanDpiSettings

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent?) {
        val action = intent?.action ?: return
        if (action != Intent.ACTION_BOOT_COMPLETED &&
            action != Intent.ACTION_LOCKED_BOOT_COMPLETED &&
            action != "android.intent.action.QUICKBOOT_POWERON" &&
            action != Intent.ACTION_MY_PACKAGE_REPLACED
        ) {
            return
        }

        val settings = RyanDpiSettings.load(context)
        if (!settings.autoStartOnBoot) return

        // Only auto-start if the user has already granted Android VPN permission
        if (VpnService.prepare(context) == null) {
            LogRepository.info("Auto-starting RyanDPI after system boot ($action).")
            runCatching { RyanDpiVpnService.startService(context) }
        } else {
            LogRepository.warn("Boot auto-start skipped: Android VPN permission requires user confirmation.")
        }
    }
}
