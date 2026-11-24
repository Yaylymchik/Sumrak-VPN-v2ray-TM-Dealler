package com.v2ray.ang.util

import android.content.Context
import com.v2ray.ang.util.npvs.NpvsV5Decryptor
import org.json.JSONArray
import java.util.Base64
import org.json.JSONObject

/**
 * Normalizes NPV Tunnel / NapsternetV containers and related exports into
 * share-link text that [com.v2ray.ang.handler.AngConfigManager] can parse.
 */
object NpvsImportHelper {

    private val shareLinkRegex = Regex(
        """(vmess|vless|trojan|trojan-go|ss|ssr|hysteria2?|hy2|socks5?|socks|tuic|wireguard|wg)://[^\s"'<>]+""",
        RegexOption.IGNORE_CASE
    )

    private val npvs1TokenRegex = Regex("""npvs1:([A-Za-z0-9+/=_-]+)""", RegexOption.IGNORE_CASE)

    fun isLikelyEncryptedNpvsEnvelope(raw: String): Boolean {
        val head = raw.trimStart().take(12)
        if (head.startsWith("NPVT1", ignoreCase = true)) return true
        if (head.startsWith("NPVTSUB1", ignoreCase = true)) return true
        if (head.startsWith("NPVS") && !head.startsWith("NPVO1", ignoreCase = true) &&
            !raw.contains("://", ignoreCase = true)
        ) {
            return true
        }
        return false
    }

    fun isBinaryNpvsV5(data: ByteArray): Boolean = NpvsV5Decryptor.isNpvsV5(data)

  /**
   * Decrypts NPVS v5 binary and returns import payload text (share links and/or full v2ray JSON).
   */
    fun unwrapBinaryNpvs(context: Context, data: ByteArray, password: String = ""): String? {
        if (!isBinaryNpvsV5(data)) return null
        val decrypted = runCatching { NpvsV5Decryptor.decrypt(context, data, password) }.getOrNull()
            ?: return null
        val chunks = LinkedHashSet<String>()
        decrypted.shareLinks.forEach { chunks += it }
        decrypted.v2rayJsonConfigs.forEach { chunks += it }
        return chunks.takeIf { it.isNotEmpty() }?.joinToString("\n")
    }

    /**
     * Returns share-link lines when recognized; otherwise null (caller uses raw payload).
     */
    fun unwrapToShareLinks(raw: String): String? {
        if (raw.isBlank()) return null

        val candidates = LinkedHashSet<String>()
        candidates += collectFromText(raw)

        for (decoded in nestedDecodedBodies(raw)) {
            candidates += collectFromText(decoded)
        }

        return candidates.takeIf { it.isNotEmpty() }?.joinToString("\n")
    }

    private fun collectFromText(text: String): Set<String> {
        val links = LinkedHashSet<String>()
        shareLinkRegex.findAll(text).forEach { links += it.value.trim() }

        npvs1TokenRegex.findAll(text).forEach { match ->
            decodeNpvs1Payload(match.groupValues[1])?.let { links += collectFromText(it) }
        }

        if (text.trimStart().startsWith("{") || text.trimStart().startsWith("[")) {
            runCatching {
                when (val json = org.json.JSONTokener(text.trim()).nextValue()) {
                    is JSONObject -> links += collectFromJson(json)
                    is JSONArray -> links += collectFromJsonArray(json)
                }
            }
        }

        if (text.contains("NPVO1", ignoreCase = true)) {
            val afterMarker = text.substringAfter("NPVO1", text).trim()
            links += collectFromText(afterMarker)
            runCatching {
                val jsonStart = afterMarker.indexOf('{')
                if (jsonStart >= 0) {
                    val jsonText = afterMarker.substring(jsonStart)
                    links += collectFromJson(JSONObject(jsonText))
                }
            }
        }

        return links
    }

    private fun collectFromJson(obj: JSONObject): Set<String> {
        val links = LinkedHashSet<String>()
        for (key in listOf("link", "url", "uri", "shareLink", "share_link", "config")) {
            obj.optString(key).takeIf { it.isNotBlank() }?.let { links += collectFromText(it) }
        }
        obj.optJSONArray("servers")?.let { links += collectFromJsonArray(it) }
        obj.optJSONArray("profiles")?.let { links += collectFromJsonArray(it) }
        obj.optJSONArray("configs")?.let { links += collectFromJsonArray(it) }
        obj.keys().forEach { k ->
            val v = obj.opt(k)
            when (v) {
                is JSONObject -> links += collectFromJson(v)
                is JSONArray -> links += collectFromJsonArray(v)
                is String -> if (v.contains("://")) links += collectFromText(v)
            }
        }
        return links
    }

    private fun collectFromJsonArray(arr: JSONArray): Set<String> {
        val links = LinkedHashSet<String>()
        for (i in 0 until arr.length()) {
            when (val item = arr.opt(i)) {
                is JSONObject -> links += collectFromJson(item)
                is String -> links += collectFromText(item)
            }
        }
        return links
    }

    private fun decodeNpvs1Payload(token: String): String? {
        val padded = token.padBase64()
        val bytes = runCatching { Base64.getDecoder().decode(padded) }.getOrNull() ?: return null
        return bytes.toString(Charsets.UTF_8).takeIf { it.isNotBlank() }
    }

    private fun nestedDecodedBodies(raw: String): List<String> {
        val out = mutableListOf<String>()
        var current = raw.trim()
        repeat(4) {
            if (current.contains("://")) return out
            val decoded = decodeBase64Plain(current)?.trim().orEmpty()
            if (decoded.isBlank() || decoded == current) return out
            out += decoded
            current = decoded
        }
        return out
    }

    private fun decodeBase64Plain(text: String): String? {
        return runCatching {
            val padded = text.padBase64()
            val bytes = Base64.getDecoder().decode(padded)
            bytes.toString(Charsets.UTF_8)
        }.getOrNull()
    }

    private fun String.padBase64(): String {
        val mod = length % 4
        return if (mod == 0) this else this + "=".repeat(4 - mod)
    }
}