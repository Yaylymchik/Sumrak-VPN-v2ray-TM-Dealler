package com.v2ray.ang.dto

data class UrlContentResult(
    val body: String,
    val headers: Map<String, String> = emptyMap(),
)
