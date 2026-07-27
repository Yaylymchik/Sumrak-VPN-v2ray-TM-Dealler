package com.v2ray.ang.util.encrypt

import android.util.Base64
import com.v2ray.ang.AppConfig
import com.v2ray.ang.util.LogUtil

object EncryptedCryptResolver {

    private val knownSchemes = listOf("happ://", "sumrax://", "v2rayng://")
    private val corruptFragmentSuffixes = listOf("=ff", "#ff", "%23ff")

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
        return cryptModeOf(canonicalize(value)) != null
    }

    /**
     * Decrypts happ/sumrax crypt deeplinks. Returns the original [value] when it is not encrypted.
     * Returns null when decryption fails.
     */
    fun resolve(value: String?): String? {
        if (value.isNullOrBlank()) return value
        val normalized = canonicalize(value)
        val mode = cryptModeOf(normalized) ?: return value
        val payload = extractPayload(normalized, mode) ?: return null
        if (payload.isBlank()) return null

        for (candidate in payloadCandidates(payload)) {
            val decrypted = runCatching { decrypt(mode, candidate) }
                .onFailure { LogUtil.e(AppConfig.TAG, "Encrypted deeplink decrypt failed", it) }
                .getOrNull()
                ?.trim()
                ?.takeIf { it.isNotBlank() && !isEncryptedDeeplink(it) }
            if (!decrypted.isNullOrBlank()) {
                LogUtil.i(AppConfig.TAG, "Encrypted deeplink resolved (${mode.name})")
                return decrypted
            }
        }
        LogUtil.e(AppConfig.TAG, "Encrypted deeplink decrypt produced no valid URL (${mode.name})")
        return null
    }

    /** Normalizes clipboard / intent deeplinks (Happ uses plain prefix stripping, not Uri). */
    fun canonicalize(value: String): String {
        var trimmed = value.trim()
        if (trimmed.contains('#')) {
            trimmed = trimmed.substringBefore('#')
        }
        return trimmed
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

    private fun extractPayload(normalized: String, mode: CryptMode): String? {
        val path = stripScheme(normalized)
        if (!path.startsWith(mode.prefix, ignoreCase = true)) return null
        return path.substring(mode.prefix.length)
    }

    private fun payloadCandidates(payload: String): List<String> {
        val base = payload.trim().replace(' ', '+')
        val out = linkedSetOf<String>()

        // Common paste mistake: trailing "#ff" copied as "=ff"
        for (suffix in corruptFragmentSuffixes) {
            if (base.endsWith(suffix, ignoreCase = true) && base.length > suffix.length) {
                out.add(base.dropLast(suffix.length))
            }
        }

        out.add(base)

        if (base.contains('%')) {
            runCatching {
                java.net.URLDecoder.decode(base, Charsets.UTF_8.name())
                    .trim()
                    .replace(' ', '+')
            }.getOrNull()?.takeIf { it.isNotBlank() }?.let { decoded ->
                out.add(decoded)
                for (suffix in corruptFragmentSuffixes) {
                    if (decoded.endsWith(suffix, ignoreCase = true) && decoded.length > suffix.length) {
                        out.add(decoded.dropLast(suffix.length))
                    }
                }
            }
        }

        return out.toList()
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
        decodeBase64OrNull(input, Base64.NO_WRAP)?.let { return it }
        decodeBase64OrNull(input, Base64.URL_SAFE or Base64.NO_WRAP)?.let { return it }
        if (input.contains('=')) {
            val trimmed = input.trimEnd('=')
            decodeBase64OrNull(trimmed, Base64.NO_WRAP)?.let { return it }
            decodeBase64OrNull(trimmed, Base64.URL_SAFE or Base64.NO_WRAP)?.let { return it }
        }
        return input
    }

    private fun decodeBase64OrNull(input: String, flags: Int): String? {
        return runCatching { String(Base64.decode(input, flags), Charsets.UTF_8) }
            .getOrNull()
            ?.takeIf { it.isNotBlank() }
    }
}
