package com.v2ray.ang.dto

data class CheckUpdateResult(
    val checkSucceeded: Boolean = false,
    val hasUpdate: Boolean = false,
    val currentVersion: String = "",
    val latestVersion: String = "",
    val releaseNotes: String = "",
    val downloadUrl: String = "",
    val preRelease: Boolean = false
)
