package com.v2ray.ang.util.npvs

import org.bouncycastle.crypto.generators.HKDFBytesGenerator
import org.bouncycastle.crypto.macs.HMac
import org.bouncycastle.crypto.params.HKDFParameters
import org.bouncycastle.crypto.params.KeyParameter
import org.bouncycastle.jce.provider.BouncyCastleProvider
import java.security.MessageDigest
import java.security.Security
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

internal object NpvsV5Crypto {

    init {
        if (Security.getProvider(BouncyCastleProvider.PROVIDER_NAME) == null) {
            Security.addProvider(BouncyCastleProvider())
        }
    }

    fun sha256(data: ByteArray): ByteArray = MessageDigest.getInstance("SHA-256").digest(data)

    fun hkdfSha256(ikm: ByteArray, salt: ByteArray, info: ByteArray, length: Int): ByteArray {
        val gen = HKDFBytesGenerator(org.bouncycastle.crypto.digests.SHA256Digest())
        gen.init(HKDFParameters(ikm, salt, info))
        val out = ByteArray(length)
        gen.generateBytes(out, 0, length)
        return out
    }

    fun pbkdf2Sha256(password: String, salt: ByteArray, iterations: Int): ByteArray {
        val spec = javax.crypto.spec.PBEKeySpec(
            password.toCharArray(),
            salt,
            iterations,
            256,
        )
        val factory = javax.crypto.SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256", "BC")
        return factory.generateSecret(spec).encoded
    }

    fun chacha20Poly1305Decrypt(
        key: ByteArray,
        nonce: ByteArray,
        cipherTextWithTag: ByteArray,
        aad: ByteArray?,
    ): ByteArray {
        val cipher = Cipher.getInstance("ChaCha20-Poly1305", BouncyCastleProvider.PROVIDER_NAME)
        val keySpec = SecretKeySpec(key, "ChaCha20")
        val paramSpec = GCMParameterSpec(128, nonce)
        cipher.init(Cipher.DECRYPT_MODE, keySpec, paramSpec)
        if (aad != null && aad.isNotEmpty()) {
            cipher.updateAAD(aad)
        }
        return cipher.doFinal(cipherTextWithTag)
    }
}