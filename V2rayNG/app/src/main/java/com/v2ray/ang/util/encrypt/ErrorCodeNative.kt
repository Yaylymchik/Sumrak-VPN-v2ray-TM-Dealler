package com.v2ray.ang.util.encrypt

import com.happproxy.util.ErrorCodeJNIWrapper

internal object ErrorCodeNative {

    private val wrapper by lazy { ErrorCodeJNIWrapper() }

    fun decryptWithMode(mode: Int, payload: String): String =
        wrapper.decryptWithMode(mode, payload)

    fun decryptFromString2(payload: String): String =
        wrapper.decryptFromString2(payload)
}
