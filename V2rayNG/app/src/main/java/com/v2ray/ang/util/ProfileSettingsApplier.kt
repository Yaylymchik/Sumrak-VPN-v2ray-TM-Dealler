package com.v2ray.ang.util

import com.v2ray.ang.AppConfig
import com.v2ray.ang.dto.entities.SubscriptionItem
import com.v2ray.ang.handler.MmkvManager

/**
 * Applies per-subscription / per-profile tunnel settings to active MMKV prefs
 * when a profile is selected or before connecting.
 */
object ProfileSettingsApplier {

    fun applyOnSelect(guid: String) {
        if (guid.isBlank()) return
        val profile = MmkvManager.decodeServerConfig(guid) ?: return

        ProfileFinalMaskApplier.applyOnSelect(guid)

        val subId = profile.subscriptionId?.takeIf { it.isNotBlank() }
        if (subId != null) {
            val sub = MmkvManager.decodeSubscription(subId) ?: return
            applySubscriptionSettings(sub)
        }

        ProfileFinalMaskApplier.extract(profile, guid)?.let { extracted ->
            applyExtractedTunnelSettings(extracted)
        }
    }

    fun applyForCurrentSelection() {
        val guid = MmkvManager.getSelectServer().orEmpty()
        if (guid.isNotBlank()) {
            applyOnSelect(guid)
        }
    }

    private fun applySubscriptionSettings(sub: SubscriptionItem) {
        sub.fragmentEnabled?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_FRAGMENT_ENABLED, it)
        }
        sub.fragmentPackets?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_FRAGMENT_PACKETS, it)
        }
        sub.fragmentLength?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_FRAGMENT_LENGTH, it)
        }
        sub.fragmentInterval?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_FRAGMENT_INTERVAL, it)
        }

        sub.muxEnabled?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_MUX_ENABLED, it)
        }
        sub.muxConcurrency?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_MUX_CONCURRENCY, it)
        }
        sub.muxXudpConcurrency?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_MUX_XUDP_CONCURRENCY, it)
        }

        sub.noiseEnabled?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_NOISE_ENABLED, it)
        }
        sub.noiseRand?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_NOISE_RAND, it)
        }
        sub.noiseDelay?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_NOISE_DELAY, it)
        }

        sub.pingType?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_PING_TYPE, it)
        }
        sub.autoConnectType?.takeIf { it.isNotBlank() }?.let {
            MmkvManager.encodeSettings(AppConfig.PREF_AUTO_CONNECT_TYPE, it)
        }
    }

    private fun applyExtractedTunnelSettings(extracted: ProfileFinalMaskApplier.ExtractedSettings) {
        if (extracted.fragmentEnabled) {
            MmkvManager.encodeSettings(AppConfig.PREF_FRAGMENT_ENABLED, true)
            extracted.fragmentPackets?.takeIf { it.isNotBlank() }?.let {
                MmkvManager.encodeSettings(AppConfig.PREF_FRAGMENT_PACKETS, it)
            }
            extracted.fragmentLength?.takeIf { it.isNotBlank() }?.let {
                MmkvManager.encodeSettings(AppConfig.PREF_FRAGMENT_LENGTH, it)
            }
            extracted.fragmentDelay?.takeIf { it.isNotBlank() }?.let {
                MmkvManager.encodeSettings(AppConfig.PREF_FRAGMENT_INTERVAL, it)
            }
        }
        if (extracted.noiseEnabled) {
            MmkvManager.encodeSettings(AppConfig.PREF_NOISE_ENABLED, true)
            extracted.noiseRand?.takeIf { it.isNotBlank() }?.let {
                MmkvManager.encodeSettings(AppConfig.PREF_NOISE_RAND, it)
            }
            extracted.noiseDelay?.takeIf { it.isNotBlank() }?.let {
                MmkvManager.encodeSettings(AppConfig.PREF_NOISE_DELAY, it)
            }
        }
    }
}
