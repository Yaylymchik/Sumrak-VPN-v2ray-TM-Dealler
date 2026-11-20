package com.v2ray.ang.service

import android.app.PendingIntent
import android.content.Intent
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.enums.NotificationChannelType
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.handler.PushMessaging
import com.v2ray.ang.ui.MainActivity
import com.v2ray.ang.util.LogUtil
import com.v2ray.ang.util.NotificationHelper

class SumrakFirebaseMessagingService : FirebaseMessagingService() {

    override fun onNewToken(token: String) {
        MmkvManager.encodeSettings(AppConfig.PREF_FCM_TOKEN, token)
        LogUtil.i(AppConfig.TAG, "FCM onNewToken")
    }

    override fun onMessageReceived(message: RemoteMessage) {
        val title = message.notification?.title
            ?: message.data["title"]
            ?: getString(R.string.app_name)
        val body = message.notification?.body
            ?: message.data["body"]
            ?: return

        val openApp = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            openApp,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        NotificationHelper.notifyWithAction(
            NotificationChannelType.PUSH_MESSAGE,
            this,
            title,
            body,
            getString(R.string.notification_push_open),
            pendingIntent,
        )
    }
}