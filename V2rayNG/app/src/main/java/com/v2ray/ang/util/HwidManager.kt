package com.v2ray.ang.util

import android.content.Context
import android.provider.Settings
import com.v2ray.ang.AppConfig
import com.v2ray.ang.handler.MmkvManager
import java.security.MessageDigest

/**
 * Happ-style HWID header for subscription HTTP requests (X-hwid).
 */
object HwidManager {

    fun isSendHwidEnabled(): Boolean {
        return MmkvManager.decodeSettingsBool(AppConfig.PREF_SEND_HWID_HEADER, true)
    }

    fun setSendHwidEnabled(enabled: Boolean) {
        MmkvManager.encodeSettings(AppConfig.PREF_SEND_HWID_HEADER, enabled)
    }

    fun resolveHwid(context: Context): String {
        val custom = MmkvManager.decodeSettingsString(AppConfig.PREF_HWID_CUSTOM_VALUE)?.trim()
        if (!custom.isNullOrEmpty()) return custom
        val androidId = Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID)
            .orEmpty()
        if (androidId.isEmpty()) return ""
        val digest = MessageDigest.getInstance("SHA-256").digest(androidId.toByteArray(Charsets.UTF_8))
        return digest.joinToString("") { b -> "%02x".format(b) }.take(32)
    }

    fun subscriptionRequestHeaders(context: Context): Map<String, String> {
        if (!isSendHwidEnabled()) return emptyMap()
        val hwid = resolveHwid(context)
        if (hwid.isEmpty()) return emptyMap()
        return mapOf(
            "X-hwid" to hwid,
            "X-HWID" to hwid,
        )
    }
}