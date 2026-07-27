package com.v2ray.ang.dto.entities

data class ServerAffiliationInfo(
    var testDelayMillis: Long = 0L,
    /** Raw days from remarks when countdown started. */
    var daysRawAtCapture: Int? = null,
    /** Remaining days at capture time. */
    var daysBaseline: Int? = null,
    /** Local start-of-day millis when [daysBaseline] was captured. */
    var daysCapturedAt: Long = 0L,
) {
    fun getTestDelayString(): String {
        if (testDelayMillis == 0L) {
            return ""
        }
        return testDelayMillis.toString() + "ms"
    }
}