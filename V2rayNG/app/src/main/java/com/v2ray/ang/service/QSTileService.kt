package com.v2ray.ang.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.graphics.drawable.Icon
import android.os.Build
import android.service.quicksettings.Tile
import android.service.quicksettings.TileService
import androidx.core.content.ContextCompat
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.core.CoreServiceManager
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.util.LogUtil
import com.v2ray.ang.util.MessageUtil
import com.v2ray.ang.util.Utils
import java.lang.ref.SoftReference

class QSTileService : TileService() {

    fun setState(state: Int) {
        val tile = qsTile ?: return
        tile.icon = Icon.createWithResource(applicationContext, R.mipmap.ic_launcher)
        when (state) {
            Tile.STATE_INACTIVE -> {
                tile.state = Tile.STATE_INACTIVE
                tile.label = getString(R.string.app_tile_connect)
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                    tile.subtitle = getString(R.string.app_name)
                }
            }
            Tile.STATE_ACTIVE -> {
                tile.state = Tile.STATE_ACTIVE
                tile.label = getString(R.string.app_tile_disconnect)
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                    val server = CoreServiceManager.getRunningServerName()
                    tile.subtitle = server.ifBlank { getString(R.string.app_name) }
                }
            }
            else -> {
                tile.state = Tile.STATE_UNAVAILABLE
                tile.label = getString(R.string.app_tile_name)
            }
        }
        tile.updateTile()
    }

    override fun onStartListening() {
        super.onStartListening()
        setState(if (CoreServiceManager.isRunning()) Tile.STATE_ACTIVE else Tile.STATE_INACTIVE)
        mMsgReceive = ReceiveMessageHandler(this)
        val mFilter = IntentFilter(AppConfig.BROADCAST_ACTION_ACTIVITY)
        ContextCompat.registerReceiver(applicationContext, mMsgReceive, mFilter, Utils.receiverFlags())
        MessageUtil.sendMsg2Service(this, AppConfig.MSG_REGISTER_CLIENT, "")
    }

    override fun onStopListening() {
        super.onStopListening()
        try {
            applicationContext.unregisterReceiver(mMsgReceive)
            mMsgReceive = null
        } catch (e: Exception) {
            LogUtil.e(AppConfig.TAG, "Failed to unregister receiver", e)
        }
    }

    override fun onClick() {
        super.onClick()
        when (qsTile?.state) {
            Tile.STATE_INACTIVE -> {
                if (MmkvManager.getSelectServer().isNullOrEmpty()) {
                    setState(Tile.STATE_INACTIVE)
                    return
                }
                CoreServiceManager.startVServiceFromToggle(this)
            }
            Tile.STATE_ACTIVE -> {
                CoreServiceManager.stopVService(this)
            }
        }
    }

    private var mMsgReceive: BroadcastReceiver? = null

    private class ReceiveMessageHandler(context: QSTileService) : BroadcastReceiver() {
        var mReference: SoftReference<QSTileService> = SoftReference(context)
        override fun onReceive(ctx: Context?, intent: Intent?) {
            val context = mReference.get()
            when (intent?.getIntExtra("key", 0)) {
                AppConfig.MSG_STATE_RUNNING,
                AppConfig.MSG_STATE_START_SUCCESS -> context?.setState(Tile.STATE_ACTIVE)

                AppConfig.MSG_STATE_NOT_RUNNING,
                AppConfig.MSG_STATE_START_FAILURE,
                AppConfig.MSG_STATE_STOP_SUCCESS -> context?.setState(Tile.STATE_INACTIVE)
            }
        }
    }
}
