package com.v2ray.ang.util

import android.content.Context
import com.v2ray.ang.R
import com.v2ray.ang.handler.MmkvManager

object ProfileRemarkParser {
    private val PATTERNS = listOf(
        Regex("""\]-\[(\d{1,4})"""),
        Regex("""\[\s*(\d{1,4})\s*(?:deys?|дн|d|day|days)?[^\]]*\]""", RegexOption.IGNORE_CASE),
        Regex("""(\d{1,4})\s*deys""", RegexOption.IGNORE_CASE),
        Regex("""(?:^|[^\d])(\d{1,4})\s*дн""", RegexOption.IGNORE_CASE),
        Regex("""[-–—|]\s*(\d{1,4})\s*(?:deys?|дн|d|days?)?(?:\s|$|\]|$)""", RegexOption.IGNORE_CASE),
        Regex("""[-–—](\d{1,4})$""")
    )

    fun parseRemainingDays(remarks: String?): Int? {
        if (remarks.isNullOrBlank()) return null
        val normalized = remarks
            .replace('【', '[')
            .replace('】', ']')
            .replace('［', '[')
            .replace('］', ']')

        for (pattern in PATTERNS) {
            val matches = pattern.findAll(normalized).toList()
            if (matches.isEmpty()) continue
            val days = matches.last().groupValues.getOrNull(1)?.toIntOrNull() ?: continue
            if (days in 1..9999) return days
        }
        return null
    }

    fun formatRemainingDays(context: Context, days: Int): String {
        return context.getString(R.string.profile_days_remaining, days)
    }

    fun formatDaysLine(
        context: Context,
        remarks: String?,
        subscriptionId: String? = null,
        guid: String? = null
    ): String? {
        val days = ProfileDaysCountdown.resolve(remarks, subscriptionId, guid) ?: return null
        return formatRemainingDays(context, days)
    }

    fun resolveLiveRemainingDays(
        remarks: String?,
        subscriptionId: String? = null,
        guid: String? = null
    ): Int? = ProfileDaysCountdown.resolve(remarks, subscriptionId, guid)

    fun resolveRemarksWithDays(vararg candidates: String?): String? {
        return candidates.firstOrNull { parseRemainingDays(it) != null }
            ?: candidates.firstOrNull { !it.isNullOrBlank() }
    }

    fun isBlockServiceProfile(remarks: String?): Boolean {
        return remarks?.contains("block_service", ignoreCase = true) == true
    }
}

object ProfileAutoSelector {
    private data class ProfileCandidate(
        val guid: String,
        val remarks: String,
        val pingMs: Long,
        val days: Int?
    )

    private fun loadCandidates(subId: String): List<ProfileCandidate> {
        val serverList = if (subId.isEmpty()) {
            MmkvManager.decodeAllServerList()
        } else {
            MmkvManager.decodeServerList(subId)
        }

        return serverList.mapNotNull { guid ->
            val profile = MmkvManager.decodeServerConfig(guid) ?: return@mapNotNull null
            val pingMs = MmkvManager.decodeServerAffiliationInfo(guid)?.testDelayMillis ?: 0L
            ProfileCandidate(
                guid = guid,
                remarks = profile.remarks,
                pingMs = pingMs,
                days = ProfileRemarkParser.resolveLiveRemainingDays(
                    remarks = profile.remarks,
                    subscriptionId = profile.subscriptionId,
                    guid = guid
                )
            )
        }
    }

    private fun selectByMinPing(candidates: List<ProfileCandidate>): String? {
        val withPing = candidates.filter { it.pingMs > 0L }
        if (withPing.isEmpty()) return null

        val minPing = withPing.minOf { it.pingMs }
        return withPing
            .filter { it.pingMs == minPing }
            .sortedWith(
                compareByDescending<ProfileCandidate> {
                    ProfileRemarkParser.isBlockServiceProfile(it.remarks)
                }.thenByDescending { it.days ?: 0 }
            )
            .first()
            .guid
    }

    private fun selectByDaysFallback(candidates: List<ProfileCandidate>): String? {
        val blockServiceWithDays = candidates
            .filter { ProfileRemarkParser.isBlockServiceProfile(it.remarks) && it.days != null }

        if (blockServiceWithDays.isNotEmpty()) {
            return blockServiceWithDays.maxByOrNull { it.days ?: 0 }?.guid
        }

        val anyWithDays = candidates.filter { it.days != null }
        if (anyWithDays.isNotEmpty()) {
            return anyWithDays.maxByOrNull { it.days ?: 0 }?.guid
        }

        return candidates
            .firstOrNull { ProfileRemarkParser.isBlockServiceProfile(it.remarks) }
            ?.guid
            ?: candidates.firstOrNull()?.guid
    }

    fun findBestProfileGuid(subId: String = ""): String? {
        val candidates = loadCandidates(subId)
        if (candidates.isEmpty()) return null
        return selectByMinPing(candidates) ?: selectByDaysFallback(candidates)
    }

    /**
     * Best server among those with a positive ping (no days fallback).
     * Used by Smart Connect after a scan.
     */
    fun findBestReachableProfile(subId: String = ""): ReachableProfile? {
        val candidates = loadCandidates(subId)
        val withPing = candidates.filter { it.pingMs > 0L }
        if (withPing.isEmpty()) return null

        val minPing = withPing.minOf { it.pingMs }
        val best = withPing
            .filter { it.pingMs == minPing }
            .sortedWith(
                compareByDescending<ProfileCandidate> {
                    ProfileRemarkParser.isBlockServiceProfile(it.remarks)
                }.thenByDescending { it.days ?: 0 }
            )
            .first()

        return ReachableProfile(best.guid, best.remarks, best.pingMs)
    }

    fun applyBestReachableSelection(subId: String = ""): ReachableProfile? {
        val best = findBestReachableProfile(subId) ?: return null
        MmkvManager.setSelectServer(best.guid)
        return best
    }

    fun applyRandomSelection(subId: String = ""): ReachableProfile? {
        val candidates = loadCandidates(subId)
        if (candidates.isEmpty()) return null
        val pick = candidates.random()
        MmkvManager.setSelectServer(pick.guid)
        return ReachableProfile(pick.guid, pick.remarks, pick.pingMs)
    }

    data class ReachableProfile(
        val guid: String,
        val remarks: String,
        val pingMs: Long
    )

    fun applyBestSelection(subId: String = ""): Boolean {
        val bestGuid = findBestProfileGuid(subId) ?: return false
        val currentGuid = MmkvManager.getSelectServer()
        if (currentGuid == bestGuid) return false
        MmkvManager.setSelectServer(bestGuid)
        return true
    }
}
