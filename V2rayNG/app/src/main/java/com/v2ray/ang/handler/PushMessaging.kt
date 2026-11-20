package com.v2ray.ang.handler

import com.google.firebase.FirebaseApp
import com.google.firebase.messaging.FirebaseMessaging
import com.v2ray.ang.AngApplication
import com.v2ray.ang.AppConfig
import com.v2ray.ang.util.LogUtil

/**
 * Registers the device FCM token when Firebase is configured (google-services.json applied at build).
 */
object PushMessaging {

    fun syncToken() {
        val context = AngApplication.application
        if (FirebaseApp.getApps(context).isEmpty()) return
        FirebaseMessaging.getInstance().token.addOnCompleteListener { task ->
            if (!task.isSuccessful) {
                val error = task.exception
                if (error != null) {
                    LogUtil.w(AppConfig.TAG, "FCM token request failed", error)
                } else {
                    LogUtil.w(AppConfig.TAG, "FCM token request failed")
                }
                return@addOnCompleteListener
            }
            val token = task.result ?: return@addOnCompleteListener
            MmkvManager.encodeSettings(AppConfig.PREF_FCM_TOKEN, token)
            LogUtil.i(AppConfig.TAG, "FCM token stored")
        }
    }
}