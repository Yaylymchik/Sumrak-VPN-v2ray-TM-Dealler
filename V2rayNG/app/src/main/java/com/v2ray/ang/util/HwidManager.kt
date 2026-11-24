package com.v2ray.ang.util

import android.content.Context
import android.provider.Settings
import com.v2ray.ang.AppConfig
import com.v2ray.ang.handler.MmkvManager
import java.security.MessageDigest

/**
 * HWID header for subscription HTTP requests (X-hwid).
 */
object HwidManager {

    fun isSendHwidEnabled(): Boolean {
        return MmkvManager.decodeSettingsBool(AppConfig.PREF_SEND_HWID_HEADER, true)
    }

    fun setSendHwidEnabled(enabled: Boolean) {
        MmkvManager.encodeSettings(AppConfig.PREF_SEND_HWID_HEADER, enabled)
    }

    fun isUseCustomHwid(): Boolean {
        return MmkvManager.decodeSettingsBool(AppConfig.PREF_USE_CUSTOM_HWID, false)
    }

    fun setUseCustomHwid(enabled: Boolean) {
        MmkvManager.encodeSettings(AppConfig.PREF_USE_CUSTOM_HWID, enabled)
    }

    fun customHwidValue(): String {
        return MmkvManager.decodeSettingsString(AppConfig.PREF_HWID_CUSTOM_VALUE)?.trim().orEmpty()
    }

    fun setCustomHwidValue(value: String) {
        val trimmed = value.trim()
        MmkvManager.encodeSettings(
            AppConfig.PREF_HWID_CUSTOM_VALUE,
            if (trimmed.isEmpty()) null else trimmed
        )
    }

    fun resolveDeviceHwid(context: Context): String {
        val androidId = Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID)
            .orEmpty()
        if (androidId.isEmpty()) return ""
        val digest = MessageDigest.getInstance("SHA-256").digest(androidId.toByteArray(Charsets.UTF_8))
        return digest.joinToString("") { b -> "%02x".format(b) }.take(32)
    }

    fun resolveHwid(context: Context): String {
        if (isUseCustomHwid()) {
            val custom = customHwidValue()
            if (custom.isNotEmpty()) return custom
        }
        return resolveDeviceHwid(context)
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