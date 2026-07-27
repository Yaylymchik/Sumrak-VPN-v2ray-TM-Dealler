package com.v2ray.ang.util

import com.v2ray.ang.AppConfig
import com.v2ray.ang.dto.entities.SubscriptionItem

object SubscriptionLock {
    fun isLocked(item: SubscriptionItem?): Boolean {
        if (item == null) return false
        return item.remarks.trim().equals(AppConfig.SUBSCRIPTION_IMPORT_SUB_REMARKS, ignoreCase = true)
    }
}
