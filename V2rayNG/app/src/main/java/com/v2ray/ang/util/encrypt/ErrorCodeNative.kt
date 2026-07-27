package com.v2ray.ang.util.encrypt

import com.happproxy.util.ErrorCodeJNIWrapper

/**
 * Delegates to liberror-code.so via [ErrorCodeJNIWrapper] (Happ-compatible JNI names).
 */
internal object ErrorCodeNative {

    fun decryptWithMode(mode: Int, payload: String): String =
        ErrorCodeJNIWrapper.decryptWithMode(mode, payload)

    fun decryptFromString2(payload: String): String =
        ErrorCodeJNIWrapper.decryptFromString2(payload)
}
