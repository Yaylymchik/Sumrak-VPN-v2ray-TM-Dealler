package com.v2ray.ang.util

/**
 * Resolves Happ-style User-Agent presets (e.g. chrome-android) to real HTTP UA strings.
 */
object UserAgentPresets {
    fun resolve(raw: String?): String {
        val key = raw?.trim().orEmpty()
        if (key.isEmpty()) return resolve("chrome-android")
        return when (key.lowercase()) {
            "chrome-android", "android" ->
                "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.6478.122 Mobile Safari/537.36"
            "chrome" ->
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"
            "firefox" ->
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:127.0) Gecko/20100101 Firefox/127.0"
            "ios", "safari" ->
                "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1"
            "edge" ->
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0"
            else -> key
        }
    }
}
