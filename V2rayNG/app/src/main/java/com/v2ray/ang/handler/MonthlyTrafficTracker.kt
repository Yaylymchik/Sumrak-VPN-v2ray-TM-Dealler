package com.v2ray.ang.handler

import android.app.NotificationChannel
import android.app.NotificationManager as SysNotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.extension.toTrafficString
import com.v2ray.ang.ui.MainActivity
import com.v2ray.ang.util.SumraXNotificationIcons
import java.text.SimpleDateFormat
import java.util.Calendar
import java.util.Locale

object MonthlyTrafficTracker {
    data class TrafficTotals(
        val proxyUp: Long = 0L,
        val proxyDown: Long = 0L,
        val directUp: Long = 0L,
        val directDown: Long = 0L,
    ) {
        val totalUp: Long get() = proxyUp + directUp
        val totalDown: Long get() = proxyDown + directDown
        val totalBytes: Long get() = totalUp + totalDown
    }

    fun currentPeriodKey(): String {
        val calendar = Calendar.getInstance()
        return String.format(
            Locale.US,
            "%04d-%02d",
            calendar.get(Calendar.YEAR),
            calendar.get(Calendar.MONTH) + 1
        )
    }

    fun loadCurrentTotals(): TrafficTotals {
        return TrafficTotals(
            proxyUp = MmkvManager.decodeSettingsLong(AppConfig.PREF_MONTHLY_PROXY_UP, 0L),
            proxyDown = MmkvManager.decodeSettingsLong(AppConfig.PREF_MONTHLY_PROXY_DOWN, 0L),
            directUp = MmkvManager.decodeSettingsLong(AppConfig.PREF_MONTHLY_DIRECT_UP, 0L),
            directDown = MmkvManager.decodeSettingsLong(AppConfig.PREF_MONTHLY_DIRECT_DOWN, 0L),
        )
    }

    fun checkPeriodOnLaunch(context: Context) {
        ensureCurrentPeriod(context)
    }

    fun recordDelta(
        proxyUp: Long,
        proxyDown: Long,
        directUp: Long,
        directDown: Long,
        context: Context?
    ): TrafficTotals {
        ensureCurrentPeriod(context)

        if (proxyUp == 0L && proxyDown == 0L && directUp == 0L && directDown == 0L) {
            return loadCurrentTotals()
        }

        val totals = loadCurrentTotals()
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_PROXY_UP, totals.proxyUp + proxyUp)
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_PROXY_DOWN, totals.proxyDown + proxyDown)
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_DIRECT_UP, totals.directUp + directUp)
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_DIRECT_DOWN, totals.directDown + directDown)
        return loadCurrentTotals()
    }

    fun formatMonthlyLine(context: Context, totals: TrafficTotals): String {
        return context.getString(
            R.string.monthly_traffic_line,
            context.getString(R.string.monthly_traffic_label),
            totals.totalUp.toTrafficString(),
            totals.totalDown.toTrafficString(),
            totals.totalBytes.toTrafficString()
        )
    }

    fun formatPeriodLabel(context: Context, periodKey: String): String {
        val parts = periodKey.split("-")
        if (parts.size != 2) return periodKey
        val year = parts[0].toIntOrNull() ?: return periodKey
        val month = parts[1].toIntOrNull() ?: return periodKey
        val calendar = Calendar.getInstance()
        calendar.set(Calendar.YEAR, year)
        calendar.set(Calendar.MONTH, month - 1)
        calendar.set(Calendar.DAY_OF_MONTH, 1)
        return SimpleDateFormat("LLLL yyyy", Locale.getDefault()).format(calendar.time)
    }

    private fun ensureCurrentPeriod(context: Context?) {
        val currentPeriod = currentPeriodKey()
        val storedPeriod = MmkvManager.decodeSettingsString(AppConfig.PREF_MONTHLY_TRAFFIC_PERIOD)

        if (storedPeriod.isNullOrBlank()) {
            MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_TRAFFIC_PERIOD, currentPeriod)
            return
        }

        if (storedPeriod == currentPeriod) return

        val previousTotals = loadCurrentTotals()
        val lastNotified = MmkvManager.decodeSettingsString(AppConfig.PREF_MONTHLY_LAST_RESET_NOTIFIED)

        resetCounters(currentPeriod)

        if (context != null &&
            previousTotals.totalBytes > 0L &&
            storedPeriod != lastNotified
        ) {
            showMonthlyResetNotification(context, storedPeriod, previousTotals)
            MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_LAST_RESET_NOTIFIED, storedPeriod)
        }
    }

    private fun resetCounters(periodKey: String) {
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_TRAFFIC_PERIOD, periodKey)
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_PROXY_UP, 0L)
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_PROXY_DOWN, 0L)
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_DIRECT_UP, 0L)
        MmkvManager.encodeSettings(AppConfig.PREF_MONTHLY_DIRECT_DOWN, 0L)
    }

    fun resetCurrentMonthCounters() {
        resetCounters(currentPeriodKey())
    }

    private fun showMonthlyResetNotification(
        context: Context,
        periodKey: String,
        totals: TrafficTotals
    ) {
        val channelId = ensureMonthlyChannel(context)
        val flags = PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        val openIntent = Intent(context, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(context, 0, openIntent, flags)
        val monthLabel = formatPeriodLabel(context, periodKey)
        val body = context.getString(
            R.string.monthly_traffic_reset_body,
            monthLabel,
            totals.totalUp.toTrafficString(),
            totals.totalDown.toTrafficString(),
            totals.totalBytes.toTrafficString()
        )

        val notification = NotificationCompat.Builder(context, channelId)
            .setSmallIcon(SumraXNotificationIcons.smallIcon(context))
            .setLargeIcon(SumraXNotificationIcons.largeIcon(context))
            .setContentTitle(context.getString(R.string.monthly_traffic_reset_title))
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setAutoCancel(true)
            .setContentIntent(pendingIntent)
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .build()

        val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as SysNotificationManager
        manager.notify(AppConfig.NOTIFICATION_ID_MONTHLY_RESET, notification)
    }

    private fun ensureMonthlyChannel(context: Context): String {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return AppConfig.RAY_NG_MONTHLY_CHANNEL_ID
        }

        val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as SysNotificationManager
        val channel = NotificationChannel(
            AppConfig.RAY_NG_MONTHLY_CHANNEL_ID,
            AppConfig.RAY_NG_MONTHLY_CHANNEL_NAME,
            SysNotificationManager.IMPORTANCE_DEFAULT
        )
        manager.createNotificationChannel(channel)
        return AppConfig.RAY_NG_MONTHLY_CHANNEL_ID
    }
}
