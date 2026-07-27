package com.happproxy.util

/**
 * JNI entry points must match liberror-code.so symbol names
 * (Java_com_happproxy_util_ErrorCodeJNIWrapper_*).
 */
class ErrorCodeJNIWrapper {

    companion object {
        init {
            System.loadLibrary("error-code")
        }

        private val instance by lazy { ErrorCodeJNIWrapper() }

        fun decryptWithMode(mode: Int, payload: String): String =
            instance.decryptWithModeInternal(mode, payload)

        fun decryptFromString2(payload: String): String =
            instance.decryptFromString2Internal(payload)
    }

    private external fun jniGetErrorMessageFromString(errorString: String, errorType: Int): ByteArray

    private external fun jniGetErrorMessageFromString2(errorString: String): ByteArray

    private fun decryptWithModeInternal(mode: Int, payload: String): String =
        String(jniGetErrorMessageFromString(payload, mode), Charsets.UTF_8)

    private fun decryptFromString2Internal(payload: String): String =
        String(jniGetErrorMessageFromString2(payload), Charsets.UTF_8)
}
