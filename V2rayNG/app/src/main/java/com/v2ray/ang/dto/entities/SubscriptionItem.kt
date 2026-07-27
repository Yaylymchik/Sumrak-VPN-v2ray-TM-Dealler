package com.v2ray.ang.dto.entities

data class SubscriptionItem(
    var remarks: String = "",
    var url: String = "",
    var enabled: Boolean = true,
    val addedTime: Long = System.currentTimeMillis(),
    var lastUpdated: Long = -1,
    var autoUpdate: Boolean = true,
    var updateInterval: Long = 30, // in minutes, default to 30 minutes
    var prevProfile: String? = null,
    var nextProfile: String? = null,
    var filter: String? = null,
    var allowInsecureUrl: Boolean = false,
    var userAgent: String? = null,

    /** null = inherit global [AppConfig.PREF_PING_TYPE] */
    var pingType: String? = null,
    /** null = inherit global auto-connect type */
    var autoConnectType: String? = null,
    /** null = inherit; false disables auto pick for this subscription */
    var autoConnect: Boolean? = null,
    var pingOnOpen: Boolean? = null,

    var fragmentEnabled: Boolean? = null,
    var fragmentPackets: String? = null,
    var fragmentLength: String? = null,
    var fragmentInterval: String? = null,

    var muxEnabled: Boolean? = null,
    var muxConcurrency: String? = null,
    var muxXudpConcurrency: String? = null,

    var noiseEnabled: Boolean? = null,
    var noiseRand: String? = null,
    var noiseDelay: String? = null,

    /** Raw days from profile remarks when countdown started. */
    var daysRawAtCapture: Int? = null,
    /** Remaining days at capture time. */
    var daysBaseline: Int? = null,
    /** Local start-of-day millis when [daysBaseline] was captured. */
    var daysCapturedAt: Long = 0L,
)
