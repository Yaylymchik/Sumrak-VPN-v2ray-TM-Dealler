package com.v2ray.ang.util

import com.v2ray.ang.dto.entities.ServerAffiliationInfo
import com.v2ray.ang.dto.entities.SubscriptionItem
import com.v2ray.ang.handler.MmkvManager
import java.time.Instant
import java.time.ZoneId
import java.time.temporal.ChronoUnit

/**
 * Decrements remaining-day counters after they are first read from profile remarks.
 * Re-seeds when the provider publishes a new raw day value (subscription update).
 */
object ProfileDaysCountdown {

    fun resolve(remarks: String?, subscriptionId: String? = null, guid: String? = null): Int? {
        val raw = ProfileRemarkParser.parseRemainingDays(remarks) ?: return null
        val now = System.currentTimeMillis()

        val subId = subscriptionId?.takeIf { it.isNotBlank() }
            ?: guid?.takeIf { it.isNotBlank() }?.let { MmkvManager.decodeServerConfig(it)?.subscriptionId }
                ?.takeIf { it.isNotBlank() }

        if (!subId.isNullOrBlank()) {
            val sub = MmkvManager.decodeSubscription(subId) ?: return raw
            return persistAndCompute(
                raw = raw,
                baseline = sub.daysBaseline,
                rawAtCapture = sub.daysRawAtCapture,
                capturedAt = sub.daysCapturedAt,
                now = now,
                save = { baseline, rawCap, captured ->
                    sub.daysBaseline = baseline
                    sub.daysRawAtCapture = rawCap
                    sub.daysCapturedAt = captured
                    MmkvManager.encodeSubscription(subId, sub)
                }
            )
        }

        if (!guid.isNullOrBlank()) {
            val aff = MmkvManager.decodeServerAffiliationInfo(guid) ?: ServerAffiliationInfo()
            return persistAndCompute(
                raw = raw,
                baseline = aff.daysBaseline,
                rawAtCapture = aff.daysRawAtCapture,
                capturedAt = aff.daysCapturedAt,
                now = now,
                save = { baseline, rawCap, captured ->
                    aff.daysBaseline = baseline
                    aff.daysRawAtCapture = rawCap
                    aff.daysCapturedAt = captured
                    MmkvManager.encodeServerAffiliationInfo(guid, aff)
                }
            )
        }

        return raw
    }

    private fun persistAndCompute(
        raw: Int,
        baseline: Int?,
        rawAtCapture: Int?,
        capturedAt: Long,
        now: Long,
        save: (baseline: Int, rawAtCapture: Int, capturedAt: Long) -> Unit
    ): Int {
        val needReset = baseline == null ||
            rawAtCapture == null ||
            capturedAt <= 0L ||
            rawAtCapture != raw

        if (needReset) {
            val start = startOfLocalDay(now)
            save(raw, raw, start)
            return raw
        }

        val elapsed = daysElapsed(capturedAt, now)
        return (baseline - elapsed).coerceAtLeast(0)
    }

    private fun startOfLocalDay(epochMs: Long): Long {
        val zone = ZoneId.systemDefault()
        return Instant.ofEpochMilli(epochMs)
            .atZone(zone)
            .toLocalDate()
            .atStartOfDay(zone)
            .toInstant()
            .toEpochMilli()
    }

    private fun daysElapsed(fromEpochMs: Long, toEpochMs: Long): Int {
        val zone = ZoneId.systemDefault()
        val start = Instant.ofEpochMilli(fromEpochMs).atZone(zone).toLocalDate()
        val end = Instant.ofEpochMilli(toEpochMs).atZone(zone).toLocalDate()
        return ChronoUnit.DAYS.between(start, end).toInt().coerceAtLeast(0)
    }
}
