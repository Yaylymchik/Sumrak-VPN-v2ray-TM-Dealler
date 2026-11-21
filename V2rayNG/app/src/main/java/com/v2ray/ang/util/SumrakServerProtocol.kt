package com.v2ray.ang.util

import com.v2ray.ang.dto.entities.ProfileItem
import com.v2ray.ang.extension.isComplexType

object SumrakServerProtocol {

    fun format(profile: ProfileItem): String {
        if (profile.configType.isComplexType()) {
            return profile.configType.name
        }
        val parts = mutableListOf(profile.configType.name)
        profile.network?.let { net ->
            if (net.isNotBlank() && !net.equals("tcp", ignoreCase = true)) {
                parts.add(net)
            }
        }
        profile.security?.let { sec ->
            if (sec.isNotBlank()) parts.add(sec)
        }
        return parts.joinToString(" / ")
    }
}