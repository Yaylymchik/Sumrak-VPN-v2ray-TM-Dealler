package com.v2ray.ang.handler

import com.v2ray.ang.AppConfig
import com.v2ray.ang.dto.OutboundTrafficStat

object TrafficStatsHelper {

    data class Delta(
        val proxyUplink: Long,
        val proxyDownlink: Long,
        val directUplink: Long,
        val directDownlink: Long,
    )

    fun isProxyOutboundTag(tag: String): Boolean {
        if (tag == AppConfig.TAG_DIRECT || tag == AppConfig.TAG_BLOCKED || tag == "dns-out") {
            return false
        }
        if (tag.startsWith("dns")) {
            return false
        }
        return true
    }

    fun aggregate(stats: List<OutboundTrafficStat>): Delta {
        var proxyDownlink = 0L
        var proxyUplink = 0L
        var directDownlink = 0L
        var directUplink = 0L
        stats.forEach { stat ->
            when {
                stat.tag == AppConfig.TAG_DIRECT -> when (stat.direction) {
                    AppConfig.DOWNLINK -> directDownlink += stat.value
                    AppConfig.UPLINK -> directUplink += stat.value
                }

                isProxyOutboundTag(stat.tag) -> when (stat.direction) {
                    AppConfig.DOWNLINK -> proxyDownlink += stat.value
                    AppConfig.UPLINK -> proxyUplink += stat.value
                }
            }
        }
        return Delta(proxyUplink, proxyDownlink, directUplink, directDownlink)
    }
}
