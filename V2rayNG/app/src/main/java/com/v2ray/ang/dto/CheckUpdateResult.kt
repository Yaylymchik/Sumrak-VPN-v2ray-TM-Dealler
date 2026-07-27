package com.v2ray.ang.dto

data class CheckUpdateResult(
    val hasUpdate: Boolean = false,
    val latestVersion: String = "",
    val releaseNotes: String = "",
    val downloadUrl: String = "",
    val preRelease: Boolean = false
)
