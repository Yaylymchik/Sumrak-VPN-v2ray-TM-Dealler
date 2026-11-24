package com.v2ray.ang.util.npvs

import android.content.Context
import com.v2ray.ang.util.JsonUtil
import org.json.JSONArray
import org.json.JSONObject
import java.nio.ByteBuffer

data class NpvsV5DecryptResult(
    val v2rayJsonConfigs: List<String>,
    val shareLinks: List<String>,
    val creatorMessage: String,
)

/**
 * Decrypts NPVS v5 (Gen2 AppKey / passphrase).
 */
object NpvsV5Decryptor {

    fun isNpvsV5(data: ByteArray): Boolean =
        data.size >= 9 && data[0] == 'N'.code.toByte() && data[1] == 'P'.code.toByte() &&
            data[2] == 'V'.code.toByte() && data[3] == 'S'.code.toByte() && data[4] == 5.toByte()

    fun decrypt(context: Context, data: ByteArray, password: String = ""): NpvsV5DecryptResult {
        require(isNpvsV5(data)) { "Not NPVS v5" }
        val hdrLen = readUInt32Be(data, 5)
        if (9 + hdrLen > data.size) error("Invalid Gen2 header length")
        val h = data.copyOfRange(9, 9 + hdrLen)
        if (h[0] != 1.toByte()) error("Unsupported Gen2 header compression: ${h[0]}")
        val configId = h.copyOfRange(1, 17)
        val method = h[50].toInt() and 0xFF
        val recipients = readUInt16Be(h, 51)
        var off = 53 + recipients * (32 + 0x5D)
        var iters = 0
        lateinit var salt: ByteArray
        lateinit var wrap: ByteArray
        when (method) {
            2 -> {
                if (off + 78 > h.size) error("AppKey section missing")
                salt = h.copyOfRange(off + 2, off + 18)
                wrap = h.copyOfRange(off + 18, off + 78)
                off += 78
            }
            1 -> {
                if (off + 80 > h.size) error("Passphrase section missing")
                iters = readUInt32Be(h, off)
                salt = h.copyOfRange(off + 4, off + 20)
                wrap = h.copyOfRange(off + 20, off + 80)
                off += 80
            }
            else -> error("Gen2 method $method requires recipient private key")
        }
        val metaLen = readUInt32Be(h, off)
        val metaBlob = h.copyOfRange(off + 4, off + 4 + metaLen)
        val prefix = h.copyOfRange(0, off)
        val nonceOff = 9 + hdrLen
        val nonce = data.copyOfRange(nonceOff, nonceOff + 12)
        val bodyLen = readUInt32Be(data, nonceOff + 12)
        val bodyOff = nonceOff + 16
        if (bodyOff + bodyLen > data.size) error("Body length invalid")
        val body = data.copyOfRange(bodyOff, bodyOff + bodyLen)

        val kdk = when (method) {
            2 -> {
                val tables = NpvsGen2Math.ensureTables(context)
                val a16 = NpvsGen2Math.gen2A16(tables, salt)
                NpvsV5Crypto.sha256("npvtunnel/appkey/v2 ".toByteArray() + a16 + configId)
            }
            else -> {
                if (password.isEmpty()) error("NPVS password required")
                NpvsV5Crypto.pbkdf2Sha256(password, salt, iters)
            }
        }

        val dek = try {
            NpvsV5Crypto.chacha20Poly1305Decrypt(kdk, wrap.copyOfRange(0, 12), wrap.copyOfRange(12, wrap.size), salt)
        } catch (_: Exception) {
            if (method == 1) error("Wrong NPVS password") else error("AppKey unwrap failed")
        }

        var creatorMessage = ""
        try {
            val metaKey = NpvsV5Crypto.hkdfSha256(
                dek,
                nonce,
                "NPVS-v5/metadata".toByteArray(),
                32,
            )
            val metaBytes = NpvsV5Crypto.chacha20Poly1305Decrypt(
                metaKey,
                nonce,
                metaBlob,
                prefix,
            )
            val metadata = JSONObject(String(metaBytes, Charsets.UTF_8))
            creatorMessage = buildCreatorMessage(metadata)
        } catch (_: Exception) {
            // optional metadata
        }

        if (body.size < 38 || body[0] != 'N'.code.toByte() || body[1] != 'P'.code.toByte() ||
            body[2] != 'F'.code.toByte() || body[3] != 1.toByte()
        ) {
            error("Invalid NPF body")
        }
        val contentId = body.copyOfRange(4, 36)
        val count = readUInt16Be(body, 36)
        var bOff = 38
        val fields = mutableMapOf<Int, ByteArray>()
        repeat(count) {
            if (bOff + 6 > body.size) return@repeat
            val seq = readUInt16Be(body, bOff)
            bOff += 2
            bOff += 2 // flags
            val blobLen = readUInt16Be(body, bOff)
            bOff += 2
            val blob = body.copyOfRange(bOff, bOff + blobLen)
            bOff += blobLen
            val fieldKey = NpvsV5Crypto.hkdfSha256(
                dek,
                contentId,
                "NPV-fields-v1/field/".toByteArray() + shortBe(seq),
                32,
            )
            val ptLen = blob.size - 16
            if (ptLen < 0) return@repeat
            val aad = buildFieldAad(contentId, seq, ptLen)
            try {
                val pt = NpvsV5Crypto.chacha20Poly1305Decrypt(
                    fieldKey,
                    ByteArray(12),
                    blob,
                    aad,
                )
                fields[seq] = pt
            } catch (_: Exception) {
                // skip bad field
            }
        }

        val configs = mutableListOf<String>()
        val links = mutableListOf<String>()
        val sentinel = fields[0xFFFF] ?: return NpvsV5DecryptResult(emptyList(), emptyList(), creatorMessage)
        val spec = parseJsonValue(sentinel)
        val specObj = spec as? JSONObject ?: return NpvsV5DecryptResult(emptyList(), emptyList(), creatorMessage)
        val arr = specObj.optJSONArray("configs") ?: JSONArray()
        for (i in 0 until arr.length()) {
            val rawCfg = arr.opt(i) ?: continue
            val cfg = gen2Substitute(rawCfg, fields)
            val v2rayJson = extractV2rayJson(cfg)
            if (v2rayJson != null) {
                configs += v2rayJson
            }
            NpvsV5ShareLinkMapper.linkFromConfig(cfg)?.let { links += it }
        }
        return NpvsV5DecryptResult(configs.distinct(), links.distinct(), creatorMessage)
    }

    private fun buildCreatorMessage(metadata: JSONObject): String {
        val policy = metadata.optJSONObject("policy") ?: return ""
        val parts = listOf("displayMessage", "customServerMessage")
            .mapNotNull { policy.optString(it).trim().takeIf { s -> s.isNotEmpty() } }
        return parts.joinToString("\n")
    }

    private fun buildFieldAad(contentId: ByteArray, seq: Int, ptLen: Int): ByteArray =
        "NPV-fields-v1/record/".toByteArray() + contentId + shortBe(seq) + intBe(ptLen)

    private fun shortBe(v: Int): ByteArray =
        byteArrayOf((v ushr 8).toByte(), v.toByte())

    private fun intBe(v: Int): ByteArray =
        byteArrayOf((v ushr 24).toByte(), (v ushr 16).toByte(), (v ushr 8).toByte(), v.toByte())

    private fun readUInt16Be(buf: ByteArray, off: Int): Int =
        ((buf[off].toInt() and 0xFF) shl 8) or (buf[off + 1].toInt() and 0xFF)

    private fun readUInt32Be(buf: ByteArray, off: Int): Int =
        ByteBuffer.wrap(buf, off, 4).int

    private fun parseJsonValue(raw: ByteArray): Any? {
        val text = raw.decodeToString().trim()
        val decoded = NpvsSentinelCodec.decodeSentinels(text)
        return when {
            decoded.trimStart().startsWith("{") -> JSONObject(decoded)
            decoded.trimStart().startsWith("[") -> JSONArray(decoded)
            else -> decoded
        }
    }

    private fun gen2FieldText(raw: ByteArray): Any {
        val s = raw.decodeToString().trim()
        if (s.length >= 2 && s.startsWith("\"") && s.endsWith("\"")) {
            return runCatching { JSONObject("{\"v\":$s}").getString("v") }.getOrElse { s.substring(1, s.length - 1) }
        }
        return s
    }

    private fun gen2Substitute(value: Any?, fields: Map<Int, ByteArray>): Any? = when (value) {
        is Int -> {
            val raw = fields[value] ?: return value
            val txt = gen2FieldText(raw)
            if (txt is String) NpvsSentinelCodec.decodeSentinels(txt) else txt
        }
        is Long -> gen2Substitute(value.toInt(), fields)
        is JSONObject -> {
            val out = JSONObject()
            value.keys().forEach { k -> out.put(k, gen2Substitute(value.get(k), fields)) }
            out
        }
        is JSONArray -> {
            val out = JSONArray()
            for (i in 0 until value.length()) out.put(gen2Substitute(value.get(i), fields))
            out
        }
        else -> value
    }

    private fun extractV2rayJson(cfg: Any?): String? {
        if (cfg !is JSONObject) return null
        val profile = cfg.optJSONObject("v2rayProfile") ?: return null
        val v2rayJson = profile.opt("v2rayJson") ?: return null
        return when (v2rayJson) {
            is String -> v2rayJson
            is JSONObject -> v2rayJson.toString()
            else -> JsonUtil.toJson(v2rayJson)
        }
    }
}