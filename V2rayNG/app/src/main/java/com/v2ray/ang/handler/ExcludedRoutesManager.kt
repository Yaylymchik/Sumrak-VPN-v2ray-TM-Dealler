package com.v2ray.ang.handler

import com.v2ray.ang.AppConfig
import java.util.regex.Pattern

object ExcludedRoutesManager {

    private val cidrPattern = Pattern.compile(
        "^((25[0-5]|2[0-4]\\d|[01]?\\d?\\d)\\.){3}(25[0-5]|2[0-4]\\d|[01]?\\d?\\d)(/(\\d|[12]\\d|3[0-2]))?$"
    )

    fun getRoutes(): List<String> {
        return MmkvManager.decodeSettingsStringSet(AppConfig.PREF_EXCLUDED_ROUTES)
            ?.filter { it.isNotBlank() }
            ?.sorted()
            ?: emptyList()
    }

    fun setRoutes(routes: Collection<String>) {
        MmkvManager.encodeSettings(
            AppConfig.PREF_EXCLUDED_ROUTES,
            routes.map { it.trim() }.filter { it.isNotBlank() }.toSortedSet().toMutableSet()
        )
    }

    fun addRoute(raw: String): Boolean {
        val route = normalize(raw) ?: return false
        val routes = getRoutes().toMutableSet()
        if (!routes.add(route)) return true
        setRoutes(routes)
        return true
    }

    fun removeRoute(route: String) {
        val routes = getRoutes().toMutableSet()
        routes.remove(route)
        setRoutes(routes)
    }

    fun clearAll() {
        MmkvManager.encodeSettings(AppConfig.PREF_EXCLUDED_ROUTES, mutableSetOf<String>())
    }

    fun normalize(raw: String): String? {
        val value = raw.trim()
        if (value.isEmpty()) return null
        val withMask = if (value.contains('/')) value else "$value/32"
        return if (cidrPattern.matcher(withMask).matches()) withMask else null
    }

    fun parseRoute(route: String): Pair<String, Int>? {
        val parts = route.split('/')
        if (parts.size != 2) return null
        val prefix = parts[1].toIntOrNull() ?: return null
        if (prefix !in 0..32) return null
        return parts[0] to prefix
    }
}
