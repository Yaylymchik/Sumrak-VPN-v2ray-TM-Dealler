package com.v2ray.ang.handler

import com.v2ray.ang.AppConfig
import com.v2ray.ang.dto.entities.SubscriptionItem
import com.v2ray.ang.util.Utils
import java.net.URI

/**
 * Applies provider MetaParams (HTTP response headers / subscription URL query)
 * onto a [SubscriptionItem] so fragmentation, mux, noise, ping and auto-connect
 * settings follow the profile after import/update.
 */
object SubscriptionMetaApplier {

    private const val FRAGMENTATION_ENABLE = "fragmentation-enable"
    private const val FRAGMENTATION_PACKETS = "fragmentation-packets"
    private const val FRAGMENTATION_LENGTH = "fragmentation-length"
    private const val FRAGMENTATION_DELAY = "fragmentation-delay"
    private const val FRAGMENTATION_INTERVAL = "fragmentation-interval"
    private const val NOISES_ENABLE = "noises-enable"
    private const val NOISES_RAND = "noises-rand"
    private const val NOISES_DELAY = "noises-delay"
    private const val MUX_ENABLE = "mux-enable"
    private const val MUX_TCP_CONNECTIONS = "mux-tcp-connections"
    private const val MUX_XUDP_CONNECTIONS = "mux-xudp-connections"
    private const val PING_TYPE = "ping-type"
    private const val SUBSCRIPTION_AUTOCONNECT = "subscription-autoconnect"
    private const val SUBSCRIPTION_AUTOCONNECT_TYPE = "subscription-autoconnect-type"
    private const val SUBSCRIPTION_PING_ONOPEN = "subscription-ping-onopen-enabled"
    private const val PROFILE_TITLE = "profile-title"
    private const val PROFILE_UPDATE_INTERVAL = "profile-update-interval"

    fun apply(subItem: SubscriptionItem, headers: Map<String, String>, subscriptionUrl: String? = null) {
        val params = mutableMapOf<String, String>()
        headers.forEach { (k, v) ->
            val key = k.trim().lowercase()
            val value = v.trim()
            if (key.isNotEmpty() && value.isNotEmpty()) {
                params[key] = value
            }
        }
        parseUrlQuery(subscriptionUrl).forEach { (k, v) ->
            params.putIfAbsent(k, v)
        }
        if (params.isEmpty()) return

        params[FRAGMENTATION_ENABLE]?.let { subItem.fragmentEnabled = parseBool(it) }
        params[FRAGMENTATION_PACKETS]?.let { subItem.fragmentPackets = normalizeFragmentPackets(it) }
        params[FRAGMENTATION_LENGTH]?.let { subItem.fragmentLength = it }
        (params[FRAGMENTATION_INTERVAL] ?: params[FRAGMENTATION_DELAY])?.let {
            subItem.fragmentInterval = it
        }

        params[NOISES_ENABLE]?.let { subItem.noiseEnabled = parseBool(it) }
        params[NOISES_RAND]?.let { subItem.noiseRand = it }
        params[NOISES_DELAY]?.let { subItem.noiseDelay = it }

        params[MUX_ENABLE]?.let { subItem.muxEnabled = parseBool(it) }
        params[MUX_TCP_CONNECTIONS]?.let { subItem.muxConcurrency = it }
        params[MUX_XUDP_CONNECTIONS]?.let { subItem.muxXudpConcurrency = it }

        params[PING_TYPE]?.let { subItem.pingType = normalizePingType(it) }
        params[SUBSCRIPTION_AUTOCONNECT]?.let { subItem.autoConnect = parseBool(it) }
        params[SUBSCRIPTION_AUTOCONNECT_TYPE]?.let {
            subItem.autoConnectType = normalizeAutoConnectType(it)
        }
        params[SUBSCRIPTION_PING_ONOPEN]?.let { subItem.pingOnOpen = parseBool(it) }

        params[PROFILE_TITLE]?.takeIf { it.isNotBlank() }?.let { title ->
            if (subItem.remarks.isBlank() ||
                subItem.remarks == AppConfig.SUBSCRIPTION_IMPORT_SUB_REMARKS
            ) {
                subItem.remarks = title
            }
        }
        params[PROFILE_UPDATE_INTERVAL]?.toLongOrNull()?.let { hours ->
            if (hours > 0) {
                val minutes = (hours * 60).coerceAtLeast(AppConfig.SUBSCRIPTION_MIN_INTERVAL_MINUTES)
                subItem.updateInterval = minutes
                subItem.autoUpdate = true
            }
        }
    }

    private fun parseUrlQuery(url: String?): Map<String, String> {
        if (url.isNullOrBlank()) return emptyMap()
        return try {
            val uri = URI(Utils.fixIllegalUrl(url))
            val query = uri.rawQuery ?: return emptyMap()
            query.split('&')
                .mapNotNull { part ->
                    val idx = part.indexOf('=')
                    if (idx <= 0) return@mapNotNull null
                    val key = Utils.urlDecode(part.substring(0, idx)).trim().lowercase()
                    val value = Utils.urlDecode(part.substring(idx + 1)).trim()
                    if (key.isEmpty() || value.isEmpty()) null else key to value
                }
                .toMap()
        } catch (_: Exception) {
            emptyMap()
        }
    }

    private fun parseBool(raw: String): Boolean {
        return when (raw.trim().lowercase()) {
            "1", "true", "yes", "on", "enable", "enabled" -> true
            else -> false
        }
    }

    private fun normalizePingType(raw: String): String {
        return when (raw.trim().lowercase()) {
            "proxy", "via_proxy_get", "proxy-get" -> AppConfig.PING_TYPE_PROXY
            "proxy-head", "via_proxy_head", "proxy_head" -> AppConfig.PING_TYPE_PROXY_HEAD
            "icmp" -> AppConfig.PING_TYPE_ICMP
            "tcp" -> AppConfig.PING_TYPE_TCP
            else -> raw.trim().lowercase()
        }
    }

    private fun normalizeAutoConnectType(raw: String): String {
        return when (raw.trim().lowercase()) {
            "lastused", "last-used", "last_used" -> AppConfig.AUTO_CONNECT_LAST_USED
            "lowestdelay", "lowest-delay", "lowest_delay", "smart" -> AppConfig.AUTO_CONNECT_LOWEST_DELAY
            "random" -> AppConfig.AUTO_CONNECT_RANDOM
            else -> raw.trim().lowercase()
        }
    }

    private fun normalizeFragmentPackets(raw: String): String {
        return when (raw.trim().lowercase()) {
            "tlshello", "tls-hello" -> "tlshello"
            "1-3", "one-three", "onethree" -> "1-3"
            else -> raw.trim()
        }
    }
}
