package com.v2ray.ang.ui

import com.v2ray.ang.AppConfig
import com.v2ray.ang.dto.entities.ServersCache
import com.v2ray.ang.dto.entities.SubscriptionCache
import com.v2ray.ang.extension.isGroupType
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.util.Utils

sealed class SumrakHomeRow {
    data class GroupHeader(
        val subscriptionId: String,
        val title: String,
        val subtitle: String,
        val expanded: Boolean,
    ) : SumrakHomeRow()

    data class Server(
        val subscriptionId: String,
        val cache: ServersCache,
        val isLastInGroup: Boolean,
    ) : SumrakHomeRow()
}

object SumrakHomeListBuilder {

    internal fun subscriptionSortComparator(pinOrder: Map<String, Int>): Comparator<SubscriptionCache> =
        compareBy<SubscriptionCache> { pinOrder[it.guid] ?: Int.MAX_VALUE }
            .thenBy { it.subscription.remarks.lowercase() }
            .thenBy { it.guid }

    internal fun serverDelayComparator(): Comparator<ServersCache> =
        compareBy<ServersCache> { sc ->
            val delay = MmkvManager.decodeServerAffiliationInfo(sc.guid)?.testDelayMillis ?: Long.MAX_VALUE
            if (delay < 0L) Long.MAX_VALUE else delay
        }.thenBy { it.guid }

    fun build(expandedSubscriptionIds: Set<String>): List<SumrakHomeRow> {
        val pinnedIds = loadPinnedSubscriptionIds()
        val pinOrder = pinnedIds.withIndex().associate { (index, id) -> id to index }
        val subs = MmkvManager.decodeSubscriptions()
            .filter { it.subscription.enabled }
            .sortedWith(subscriptionSortComparator(pinOrder))
        if (subs.isEmpty()) {
            return flatUncategorized(expandedSubscriptionIds)
        }
        val rows = mutableListOf<SumrakHomeRow>()
        val autoSort = MmkvManager.decodeSettingsBool(AppConfig.PREF_AUTO_SORT_AFTER_TEST, true)
        for (sub in subs) {
            val id = sub.guid
            val item = sub.subscription
            val expanded = id in expandedSubscriptionIds
            val subtitle = when {
                item.lastUpdated > 0 -> Utils.formatTimestamp(item.lastUpdated, "dd.MM.yyyy HH:mm")
                else -> ""
            }
            rows += SumrakHomeRow.GroupHeader(
                subscriptionId = id,
                title = item.remarks.ifBlank { id },
                subtitle = subtitle,
                expanded = expanded,
            )
            if (!expanded) continue
            var servers = serversForSubscription(id)
            if (autoSort) {
                servers = servers.sortedWith(serverDelayComparator())
            }
            servers.forEachIndexed { index, sc ->
                rows += SumrakHomeRow.Server(
                    subscriptionId = id,
                    cache = sc,
                    isLastInGroup = index == servers.lastIndex,
                )
            }
        }
        return rows
    }

    private fun flatUncategorized(expandedSubscriptionIds: Set<String>): List<SumrakHomeRow> {
        val id = AppConfig.DEFAULT_SUBSCRIPTION_ID
        val expanded = id in expandedSubscriptionIds
        val guids = MmkvManager.decodeAllServerList()
        val servers = guids.mapNotNull { guid ->
            val profile = MmkvManager.decodeServerConfig(guid) ?: return@mapNotNull null
            if (profile.configType.isGroupType()) return@mapNotNull null
            ServersCache(guid, profile)
        }
        if (servers.isEmpty()) return emptyList()
        val rows = mutableListOf<SumrakHomeRow>(
            SumrakHomeRow.GroupHeader(id, id, "", expanded)
        )
        if (expanded) {
            servers.forEachIndexed { index, sc ->
                rows += SumrakHomeRow.Server(id, sc, index == servers.lastIndex)
            }
        }
        return rows
    }

    private fun serversForSubscription(subscriptionId: String): List<ServersCache> {
        return MmkvManager.decodeServerList(subscriptionId).mapNotNull { guid ->
            val profile = MmkvManager.decodeServerConfig(guid) ?: return@mapNotNull null
            if (profile.configType.isGroupType()) return@mapNotNull null
            ServersCache(guid, profile)
        }
    }

    fun loadPinnedSubscriptionIds(): List<String> {
        val raw = MmkvManager.decodeSettingsString(AppConfig.PREF_HOME_PINNED_SUBS, "").orEmpty()
        if (raw.isBlank()) return emptyList()
        return raw.split(',').filter { it.isNotBlank() }
    }

    fun defaultExpandedIds(): Set<String> {
        val subs = MmkvManager.decodeSubscriptions().map { it.guid }
        return if (subs.isNotEmpty()) subs.toSet() else setOf(AppConfig.DEFAULT_SUBSCRIPTION_ID)
    }
}