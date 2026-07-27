package com.v2ray.ang.handler

import com.v2ray.ang.AppConfig
import com.v2ray.ang.util.JsonUtil

/**
 * Favorites and recent server lists for the home picker.
 */
object ServerListsManager {
    private const val MAX_RECENT = 30

    fun getFavoriteGuids(): Set<String> {
        return MmkvManager.decodeSettingsStringSet(AppConfig.PREF_FAVORITE_SERVERS).orEmpty()
    }

    fun isFavorite(guid: String): Boolean = getFavoriteGuids().contains(guid)

    fun toggleFavorite(guid: String): Boolean {
        val set = getFavoriteGuids().toMutableSet()
        val nowFavorite = if (set.contains(guid)) {
            set.remove(guid)
            false
        } else {
            set.add(guid)
            true
        }
        MmkvManager.encodeSettings(AppConfig.PREF_FAVORITE_SERVERS, set)
        return nowFavorite
    }

    fun getRecentGuids(): List<String> {
        val json = MmkvManager.decodeSettingsString(AppConfig.PREF_RECENT_SERVERS).orEmpty()
        if (json.isBlank()) return emptyList()
        return JsonUtil.fromJsonSafe(json, Array<String>::class.java)?.toList().orEmpty()
    }

    fun recordRecent(guid: String) {
        if (guid.isBlank()) return
        val list = getRecentGuids().toMutableList()
        list.remove(guid)
        list.add(0, guid)
        while (list.size > MAX_RECENT) {
            list.removeAt(list.lastIndex)
        }
        MmkvManager.encodeSettings(AppConfig.PREF_RECENT_SERVERS, JsonUtil.toJson(list))
    }
}
