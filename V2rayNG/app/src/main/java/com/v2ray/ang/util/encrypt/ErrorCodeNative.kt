package com.v2ray.ang.util.encrypt

/**
 * JNI bridge to liberror-code.so (crypt / crypt2–crypt5 payload decode).
 */
internal object ErrorCodeNative {
    init {
        System.loadLibrary("error-code")
    }

    fun decryptWithMode(mode: Int, payload: String): String {
        return String(jniGetErrorMessageFromString(payload, mode), Charsets.UTF_8)
    }

    fun decryptFromString2(payload: String): String {
        return String(jniGetErrorMessageFromString2(payload), Charsets.UTF_8)
    }

    @Suppress("unused")
    private external fun jniGetErrorMessageFromCode(errorCode: Int): String

    private external fun jniGetErrorMessageFromString(errorString: String, errorType: Int): ByteArray

    private external fun jniGetErrorMessageFromString2(errorString: String): ByteArray
}
