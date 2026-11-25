package com.ryandpi.android.service

import android.app.PendingIntent
import android.content.Intent
import android.net.VpnService
import android.os.Build
import android.service.quicksettings.Tile
import android.service.quicksettings.TileService
import androidx.annotation.RequiresApi
import com.ryandpi.android.MainActivity
import com.ryandpi.android.engine.RyanDpiSettings
import com.ryandpi.android.engine.VpnStateHolder
import com.ryandpi.android.engine.VpnStatus

@RequiresApi(Build.VERSION_CODES.N)
class RyanDpiTileService : TileService() {

    override fun onStartListening() {
        super.onStartListening()
        syncTileState()
    }

    override fun onClick() {
        super.onClick()
        val current = VpnStateHolder.state.value.status
        if (current == VpnStatus.CONNECTED || current == VpnStatus.CONNECTING) {
            RyanDpiVpnService.stopService(this)
            updateTile(Tile.STATE_INACTIVE)
            return
        }

        // Check if Android VPN permission is already granted
        val prepareIntent = VpnService.prepare(this)
        if (prepareIntent != null) {
            val openApp = Intent(this, MainActivity::class.java).apply {
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                putExtra(MainActivity.EXTRA_AUTO_CONNECT, true)
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                val pi = PendingIntent.getActivity(
                    this,
                    42,
                    openApp,
                    PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
                )
                startActivityAndCollapse(pi)
            } else {
                @Suppress("DEPRECATION")
                startActivityAndCollapse(openApp)
            }
            return
        }

        RyanDpiVpnService.startService(this)
        updateTile(Tile.STATE_ACTIVE)
    }

    private fun syncTileState() {
        val status = VpnStateHolder.state.value.status
        val tileState = when (status) {
            VpnStatus.CONNECTED, VpnStatus.CONNECTING -> Tile.STATE_ACTIVE
            else -> Tile.STATE_INACTIVE
        }
        updateTile(tileState)
    }

    private fun updateTile(state: Int) {
        val tile = qsTile ?: return
        val isTr = RyanDpiSettings.load(this).language == "tr"
        tile.state = state
        tile.label = "RyanDPI"
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            tile.subtitle = when {
                state == Tile.STATE_ACTIVE && isTr -> "Aktif"
                state == Tile.STATE_ACTIVE -> "Active"
                isTr -> "Kapalı"
                else -> "Off"
            }
        }
        tile.updateTile()
    }
}
