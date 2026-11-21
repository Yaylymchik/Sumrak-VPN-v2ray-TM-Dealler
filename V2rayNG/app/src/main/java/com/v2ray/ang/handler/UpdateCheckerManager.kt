package com.v2ray.ang.handler

import com.v2ray.ang.AppConfig
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.dto.CheckUpdateResult
import com.v2ray.ang.dto.GitHubRelease
import com.v2ray.ang.dto.UrlContentRequest
import com.v2ray.ang.util.HttpUtil
import com.v2ray.ang.util.JsonUtil
import com.v2ray.ang.util.LogUtil
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

object UpdateCheckerManager {

    suspend fun checkForUpdate(includePreRelease: Boolean = false): CheckUpdateResult =
        withContext(Dispatchers.IO) {
            val currentVersion = BuildConfig.VERSION_NAME.trim()
            try {
                val apiUrl = SettingsManager.getAppUpdateApiUrl()
                if (apiUrl.isBlank()) {
                    return@withContext CheckUpdateResult(currentVersion = currentVersion)
                }
                val response = fetchReleasesJson(apiUrl)
                if (response.isNullOrBlank()) {
                    return@withContext CheckUpdateResult(currentVersion = currentVersion)
                }

                val releases = JsonUtil.fromJsonSafe(response, Array<GitHubRelease>::class.java)
                    ?: return@withContext CheckUpdateResult(currentVersion = currentVersion)

                val latest = releases
                    .filter { includePreRelease || !it.prerelease }
                    .maxWithOrNull { a, b ->
                        compareVersions(releaseVersion(a), releaseVersion(b))
                    }
                    ?: return@withContext CheckUpdateResult(currentVersion = currentVersion)

                val latestVersion = releaseVersion(latest)
                val hasUpdate = compareVersions(latestVersion, currentVersion) > 0
                if (!hasUpdate) {
                    return@withContext CheckUpdateResult(
                        checkSucceeded = true,
                        hasUpdate = false,
                        currentVersion = currentVersion,
                        latestVersion = latestVersion,
                        preRelease = latest.prerelease
                    )
                }

                val apkAsset = pickApkAsset(latest)
                CheckUpdateResult(
                    checkSucceeded = true,
                    hasUpdate = true,
                    currentVersion = currentVersion,
                    latestVersion = latestVersion,
                    releaseNotes = latest.body,
                    downloadUrl = apkAsset?.browserDownloadUrl.orEmpty(),
                    preRelease = latest.prerelease
                )
            } catch (e: Exception) {
                LogUtil.e(AppConfig.TAG, "Failed to check for update", e)
                CheckUpdateResult(currentVersion = currentVersion)
            }
        }

    private fun releaseVersion(release: GitHubRelease): String =
        release.tagName.removePrefix("v").trim()

    private fun pickApkAsset(latest: GitHubRelease): GitHubRelease.Asset? {
        val apks = latest.assets.filter { it.name.endsWith(".apk", ignoreCase = true) }
        return apks.firstOrNull { it.name.contains("universal", ignoreCase = true) }
            ?: apks.firstOrNull { it.name.contains("arm64", ignoreCase = true) }
            ?: apks.firstOrNull()
    }

    private fun fetchReleasesJson(apiUrl: String): String? {
        val httpPort = SettingsManager.getHttpPort()
        val portsToTry = if (httpPort == 0) listOf(0) else listOf(httpPort, 0)
        var lastError: Exception? = null
        for (port in portsToTry) {
            try {
                val response = HttpUtil.getUrlContentWithUserAgent(
                    UrlContentRequest(url = apiUrl, timeout = 12000, httpPort = port)
                )
                if (response.isNotBlank()) {
                    return response
                }
            } catch (e: Exception) {
                lastError = e
                LogUtil.w(AppConfig.TAG, "Update check via HTTP port $port failed: ${e.message}")
            }
        }
        if (lastError != null) {
            LogUtil.e(AppConfig.TAG, "Update check: all fetch attempts failed", lastError)
        }
        return null
    }

    private fun compareVersions(a: String, b: String): Int {
        val pa = a.split('.', '-', '_').mapNotNull { it.toIntOrNull() }
        val pb = b.split('.', '-', '_').mapNotNull { it.toIntOrNull() }
        val size = maxOf(pa.size, pb.size)
        for (i in 0 until size) {
            val x = pa.getOrElse(i) { 0 }
            val y = pb.getOrElse(i) { 0 }
            if (x != y) return x.compareTo(y)
        }
        return 0
    }
}
