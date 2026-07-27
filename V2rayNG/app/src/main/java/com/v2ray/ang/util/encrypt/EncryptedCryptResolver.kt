package com.v2ray.ang.util.encrypt

import android.util.Base64
import com.v2ray.ang.AppConfig
import com.v2ray.ang.util.LogUtil
import com.v2ray.ang.util.Utils

object EncryptedCryptResolver {

    private val knownSchemes = listOf("happ://", "sumrax://", "v2rayng://")

    enum class CryptMode(val prefix: String) {
        RSA_1024("crypt/"),
        RSA_4096("crypt2/"),
        RSA_4096_2("crypt3/"),
        RSA_4096_3("crypt4/"),
        CRYPTO_5("crypt5/");

        companion object {
            fun fromPath(path: String): CryptMode? =
                entries.firstOrNull { path.startsWith(it.prefix, ignoreCase = true) }
        }
    }

    fun isEncryptedDeeplink(value: String?): Boolean {
        if (value.isNullOrBlank()) return false
        return cryptModeOf(normalize(value)) != null
    }

    /**
     * Decrypts happ/sumrax crypt deeplinks. Returns the original [value] when it is not encrypted.
     * Returns null when decryption fails.
     */
    fun resolve(value: String?): String? {
        if (value.isNullOrBlank()) return value
        val normalized = normalize(value)
        val mode = cryptModeOf(normalized) ?: return value
        val path = stripScheme(normalized)
        val payload = path.removePrefix(mode.prefix)
        if (payload.isBlank()) return null

        val decrypted = runCatching { decrypt(mode, normalizePayload(payload)) }
            .onFailure { LogUtil.e(AppConfig.TAG, "Encrypted deeplink decrypt failed", it) }
            .getOrNull()
            ?.trim()
            ?.takeIf { it.isNotBlank() && !isEncryptedDeeplink(it) }

        return decrypted
    }

    private fun cryptModeOf(normalized: String): CryptMode? {
        val path = stripScheme(normalized)
        return CryptMode.fromPath(path)
    }

    private fun stripScheme(value: String): String {
        for (scheme in knownSchemes) {
            if (value.startsWith(scheme, ignoreCase = true)) {
                return value.substring(scheme.length)
            }
        }
        return value
    }

    private fun normalize(value: String): String {
        var trimmed = value.trim()
        val hashIndex = trimmed.indexOf('#')
        if (hashIndex >= 0) {
            trimmed = trimmed.substring(0, hashIndex)
        }
        return trimmed
    }

    private fun normalizePayload(payload: String): String {
        var normalized = payload.trim()
        normalized = normalized.replace(' ', '+')
        val urlDecoded = runCatching { Utils.urlDecode(normalized) }.getOrNull()?.trim()
        if (!urlDecoded.isNullOrBlank() && urlDecoded != normalized) {
            normalized = urlDecoded.replace(' ', '+')
        }
        return normalized
    }

    private fun decrypt(mode: CryptMode, payload: String): String {
        return when (mode) {
            CryptMode.RSA_1024,
            CryptMode.RSA_4096,
            CryptMode.RSA_4096_2,
            CryptMode.RSA_4096_3 -> ErrorCodeNative.decryptWithMode(mode.ordinal, payload)

            CryptMode.CRYPTO_5 -> decryptCrypto5(payload)
        }
    }

    private fun decryptCrypto5(payload: String): String {
        val reordered = reorderSixCharChunks(payload)
        val nativeText = ErrorCodeNative.decryptFromString2(reordered)
        return decodeFlexibleBase64(swapAdjacentPairs(nativeText))
    }

    private fun reorderSixCharChunks(input: String): String {
        val chunks = input.chunked(6)
        val builder = StringBuilder()
        for (chunk in chunks) {
            if (chunk.length > 5) {
                builder.append(chunk[1])
                builder.append(chunk[3])
                builder.append(chunk[5])
                builder.append(chunk[0])
                builder.append(chunk[2])
                builder.append(chunk[4])
            } else {
                builder.append(chunk)
            }
        }
        return builder.toString()
    }

    private fun swapAdjacentPairs(input: String): String {
        val chunks = input.chunked(2)
        val builder = StringBuilder()
        for (chunk in chunks) {
            if (chunk.length > 1) {
                builder.append(chunk[1])
                builder.append(chunk[0])
            } else {
                builder.append(chunk)
            }
        }
        return builder.toString()
    }

    private fun decodeFlexibleBase64(input: String): String {
        decodeBase64OrNull(input)?.let { return it }
        if (input.contains('=')) {
            val trimmed = input.trimEnd('=')
            decodeBase64OrNull(trimmed)?.let { return it }
        }
        return input
    }

    private fun decodeBase64OrNull(input: String): String? {
        return runCatching { String(Base64.decode(input, Base64.NO_WRAP), Charsets.UTF_8) }
            .recoverCatching { String(Base64.decode(input, Base64.URL_SAFE), Charsets.UTF_8) }
            .getOrNull()
    }
}
