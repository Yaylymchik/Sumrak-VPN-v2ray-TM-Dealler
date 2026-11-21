package com.v2ray.ang.handler

import android.content.Context
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import java.time.Instant
import java.time.ZoneId

object SubscriptionRenewalReminder {

    data class ToggleRenewal(
        val showInToggle: Boolean,
        val message: String,
    )

    fun resolveExpiredToggle(context: Context, days: Int?): ToggleRenewal? {
        if (days != 0) return null
        return ToggleRenewal(
            showInToggle = true,
            message = context.getString(R.string.sumrax_renew_subscription_toggle),
        )
    }

    fun shouldShowDailyLowDaysReminder(days: Int?, subscriptionId: String?): Boolean {
        if (days == null || days !in 1..AppConfig.SUBSCRIPTION_RENEW_LOW_DAYS_THRESHOLD) return false
        val key = storageKey(subscriptionId)
        val toastDay = MmkvManager.decodeSettingsString(prefDailyToastKey(key)).orEmpty()
        return toastDay != todayLocalKey()
    }

    fun markDailyLowDaysReminderShown(subscriptionId: String?) {
        MmkvManager.encodeSettings(prefDailyToastKey(storageKey(subscriptionId)), todayLocalKey())
    }

    fun shouldHourlyExpiredPulse(subscriptionId: String?): Boolean {
        val key = storageKey(subscriptionId)
        val last = MmkvManager.decodeSettingsLong(prefHourlyKey(key), 0L)
        return System.currentTimeMillis() - last >= AppConfig.SUBSCRIPTION_RENEW_HOURLY_INTERVAL_MS
    }

    fun markHourlyExpiredPulse(subscriptionId: String?) {
        MmkvManager.encodeSettings(prefHourlyKey(storageKey(subscriptionId)), System.currentTimeMillis())
    }

    fun dailyReminderMessage(context: Context, days: Int): String =
        context.getString(R.string.sumrax_renew_subscription_daily, days)

    private fun storageKey(subscriptionId: String?): String =
        subscriptionId?.takeIf { it.isNotBlank() } ?: "_none"

    private fun prefDailyToastKey(subKey: String) = "${AppConfig.PREF_SUB_RENEW_DAILY_TOAST_DAY}_$subKey"

    private fun prefHourlyKey(subKey: String) = "${AppConfig.PREF_SUB_RENEW_HOURLY_REMINDER_AT}_$subKey"

    private fun todayLocalKey(): String {
        val zone = ZoneId.systemDefault()
        return Instant.ofEpochMilli(System.currentTimeMillis()).atZone(zone).toLocalDate().toString()
    }
}