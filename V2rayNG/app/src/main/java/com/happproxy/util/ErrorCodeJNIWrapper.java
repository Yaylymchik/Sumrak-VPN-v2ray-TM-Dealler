package com.happproxy.util;

import java.nio.charset.StandardCharsets;

/**
 * JNI bridge — symbol names must match liberror-code.so
 * (Java_com_happproxy_util_ErrorCodeJNIWrapper_*).
 */
public final class ErrorCodeJNIWrapper {

    static {
        System.loadLibrary("error-code");
    }

    public ErrorCodeJNIWrapper() {
    }

    private native byte[] jniGetErrorMessageFromString(String errorString, int errorType);

    private native byte[] jniGetErrorMessageFromString2(String errorString);

    public String decryptWithMode(int mode, String payload) {
        return new String(jniGetErrorMessageFromString(payload, mode), StandardCharsets.UTF_8);
    }

    public String decryptFromString2(String payload) {
        return new String(jniGetErrorMessageFromString2(payload), StandardCharsets.UTF_8);
    }
}
