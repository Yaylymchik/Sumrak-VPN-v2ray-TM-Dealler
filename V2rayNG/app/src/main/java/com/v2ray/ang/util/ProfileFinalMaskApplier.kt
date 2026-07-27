package com.v2ray.ang.util

import com.google.gson.JsonArray
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import com.v2ray.ang.dto.entities.ProfileItem
import com.v2ray.ang.dto.entities.SubscriptionItem
import com.v2ray.ang.handler.MmkvManager

/**
 * Reads profile `finalmask` raw JSON and auto-applies fragment / noise settings
 * for the selected profile (and its subscription overrides).
 */
object ProfileFinalMaskApplier {

    data class ExtractedSettings(
        val fragmentEnabled: Boolean = false,
        val fragmentPackets: String? = null,
        val fragmentLength: String? = null,
        val fragmentDelay: String? = null,
        val noiseEnabled: Boolean = false,
        val noiseRand: String? = null,
        val noiseDelay: String? = null,
        val normalizedJson: String? = null,
    )

    /**
     * Called after a profile becomes the selected server.
     */
    fun applyOnSelect(guid: String) {
        if (guid.isBlank()) return
        val profile = MmkvManager.decodeServerConfig(guid) ?: return
        val extracted = extract(profile, guid) ?: return

        var profileChanged = false
        if (!extracted.normalizedJson.isNullOrBlank() &&
            extracted.normalizedJson != profile.finalMask
        ) {
            profile.finalMask = extracted.normalizedJson
            profileChanged = true
        }

        if (profileChanged) {
            MmkvManager.encodeServerConfig(guid, profile)
        }

        applyToSubscription(profile.subscriptionId, extracted)
    }

    fun extract(profile: ProfileItem, guid: String? = null): ExtractedSettings? {
        val candidates = mutableListOf<String>()
        profile.finalMask?.takeIf { it.isNotBlank() }?.let { candidates.add(it) }
        guid?.takeIf { it.isNotBlank() }?.let { id ->
            extractFromRawConfig(MmkvManager.decodeServerRaw(id))?.let { candidates.add(it) }
        }

        for (raw in candidates) {
            val normalized = FinalMaskUtil.normalizeToJson(raw) ?: continue
            val parsed = parseSettings(normalized) ?: continue
            return parsed.copy(normalizedJson = normalized)
        }
        return null
    }

    private fun applyToSubscription(subscriptionId: String?, extracted: ExtractedSettings) {
        val subId = subscriptionId?.takeIf { it.isNotBlank() } ?: return
        val sub = MmkvManager.decodeSubscription(subId) ?: SubscriptionItem()

        if (extracted.fragmentEnabled) {
            sub.fragmentEnabled = true
            extracted.fragmentPackets?.let { sub.fragmentPackets = it }
            extracted.fragmentLength?.let { sub.fragmentLength = it }
            extracted.fragmentDelay?.let { sub.fragmentInterval = it }
        }
        if (extracted.noiseEnabled) {
            sub.noiseEnabled = true
            extracted.noiseRand?.let { sub.noiseRand = it }
            extracted.noiseDelay?.let { sub.noiseDelay = it }
        }
        MmkvManager.encodeSubscription(subId, sub)
    }

    private fun parseSettings(normalizedJson: String): ExtractedSettings? {
        val root = runCatching { JsonParser.parseString(normalizedJson).asJsonObject }.getOrNull()
            ?: return null

        var fragmentEnabled = false
        var packets: String? = null
        var length: String? = null
        var delay: String? = null
        var noiseEnabled = false
        var noiseRand: String? = null
        var noiseDelay: String? = null

        fun readMaskArray(arr: JsonArray?) {
            if (arr == null) return
            for (el in arr) {
                if (!el.isJsonObject) continue
                val obj = el.asJsonObject
                val type = obj.get("type")?.asString?.lowercase().orEmpty()
                val settings = obj.getAsJsonObject("settings") ?: continue
                when (type) {
                    "fragment" -> {
                        fragmentEnabled = true
                        packets = settings.get("packets")?.asString ?: packets
                        length = settings.get("length")?.asString ?: length
                        delay = settings.get("delay")?.asString ?: delay
                    }
                    "noise" -> {
                        noiseEnabled = true
                        val noiseArr = settings.getAsJsonArray("noise")
                        val first = noiseArr?.firstOrNull()?.takeIf { it.isJsonObject }?.asJsonObject
                        if (first != null) {
                            noiseRand = first.get("rand")?.asString ?: noiseRand
                            noiseDelay = first.get("delay")?.asString ?: noiseDelay
                        }
                    }
                }
            }
        }

        readMaskArray(root.getAsJsonArray("tcp"))
        readMaskArray(root.getAsJsonArray("udp"))

        // Some providers put a single mask object without tcp/udp wrappers
        if (!fragmentEnabled && !noiseEnabled && root.has("type")) {
            val type = root.get("type")?.asString?.lowercase()
            val settings = root.getAsJsonObject("settings")
            if (type == "fragment" && settings != null) {
                fragmentEnabled = true
                packets = settings.get("packets")?.asString
                length = settings.get("length")?.asString
                delay = settings.get("delay")?.asString
            }
        }

        if (!fragmentEnabled && !noiseEnabled) return null
        return ExtractedSettings(
            fragmentEnabled = fragmentEnabled,
            fragmentPackets = packets,
            fragmentLength = length,
            fragmentDelay = delay,
            noiseEnabled = noiseEnabled,
            noiseRand = noiseRand,
            noiseDelay = noiseDelay,
        )
    }

    private fun extractFromRawConfig(raw: String?): String? {
        if (raw.isNullOrBlank()) return null
        return try {
            val root = JsonParser.parseString(raw).asJsonObject
            // Full Xray config
            val outbounds = root.getAsJsonArray("outbounds")
            if (outbounds != null) {
                for (el in outbounds) {
                    if (!el.isJsonObject) continue
                    val stream = el.asJsonObject.getAsJsonObject("streamSettings") ?: continue
                    val fm = stream.get("finalmask") ?: stream.get("finalMask")
                    if (fm != null) {
                        return if (fm.isJsonPrimitive) fm.asString else fm.toString()
                    }
                }
            }
            // Direct streamSettings / finalmask object
            val stream = root.getAsJsonObject("streamSettings")
            val fm = stream?.get("finalmask")
                ?: stream?.get("finalMask")
                ?: root.get("finalmask")
                ?: root.get("finalMask")
                ?: root.get("fm")
            when {
                fm == null -> null
                fm.isJsonPrimitive -> fm.asString
                else -> fm.toString()
            }
        } catch (_: Exception) {
            null
        }
    }
}

object FinalMaskUtil {

    /**
     * Accepts raw JSON, URL-encoded JSON, or base64 JSON and returns compact JSON text.
     */
    fun normalizeToJson(raw: String?): String? {
        if (raw.isNullOrBlank()) return null
        val trimmed = raw.trim()
        tryParseObject(trimmed)?.let { return it }

        val urlDecoded = runCatching { Utils.decodeURIComponent(trimmed) }.getOrNull()?.trim()
        if (!urlDecoded.isNullOrBlank() && urlDecoded != trimmed) {
            tryParseObject(urlDecoded)?.let { return it }
        }

        val b64 = Utils.decode(trimmed).trim()
        if (b64.isNotBlank() && b64 != trimmed) {
            tryParseObject(b64)?.let { return it }
            val b64Url = runCatching { Utils.decodeURIComponent(b64) }.getOrNull()?.trim()
            if (!b64Url.isNullOrBlank()) {
                tryParseObject(b64Url)?.let { return it }
            }
        }

        // Quoted JSON string payload: "{\"tcp\":[...]}"
        if (trimmed.startsWith("\"") && trimmed.endsWith("\"")) {
            val unquoted = runCatching {
                JsonParser.parseString(trimmed).asString
            }.getOrNull()
            if (!unquoted.isNullOrBlank()) {
                return normalizeToJson(unquoted)
            }
        }
        return null
    }

    private fun tryParseObject(src: String): String? {
        return try {
            val el = JsonParser.parseString(src)
            when {
                el.isJsonObject -> el.asJsonObject.toString()
                el.isJsonArray -> {
                    // Wrap bare array as tcp masks
                    val obj = JsonObject()
                    obj.add("tcp", el.asJsonArray)
                    obj.toString()
                }
                else -> null
            }
        } catch (_: Exception) {
            null
        }
    }
}
