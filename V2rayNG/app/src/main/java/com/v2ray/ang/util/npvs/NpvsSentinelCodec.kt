package com.v2ray.ang.util.npvs

import java.util.Base64

internal object NpvsSentinelCodec {

    private const val PREFIX = "npvs1:"
    private const val ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=_-"

    fun decodeSentinels(s: String): String {
        if (!s.contains(PREFIX)) return s
        val out = StringBuilder()
        var rest = s
        while (PREFIX in rest) {
            val before = rest.substringBefore(PREFIX)
            out.append(before)
            rest = rest.substring(before.length + PREFIX.length)
            var j = 0
            while (j < rest.length && rest[j] in ALPHABET) j++
            val tok = rest.substring(0, j)
            rest = rest.substring(j)
            val dec = decodeSentinelToken(tok)
            out.append(dec ?: "$PREFIX$tok")
        }
        out.append(rest)
        return out.toString()
    }

    private fun decodeSentinelToken(tok: String): String? {
        for (pad in listOf("", "=", "==", "===")) {
            val t = tok + pad
            for (decoder in listOf(Base64.getDecoder(), Base64.getUrlDecoder())) {
                runCatching {
                    return String(decoder.decode(t), Charsets.UTF_8)
                }
            }
        }
        return null
    }
}