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
            try {
                val apiUrl = SettingsManager.getAppUpdateApiUrl()
                if (apiUrl.isBlank()) {
                    return@withContext CheckUpdateResult(hasUpdate = false)
                }
                val response = HttpUtil.getUrlContentWithUserAgent(
                    UrlContentRequest(url = apiUrl, timeout = 12000)
                )
                if (response.isBlank()) {
                    return@withContext CheckUpdateResult(hasUpdate = false)
                }

                val releases = JsonUtil.fromJsonSafe(response, Array<GitHubRelease>::class.java)
                    ?: return@withContext CheckUpdateResult(hasUpdate = false)

                val latest = releases
                    .filter { includePreRelease || !it.prerelease }
                    .maxByOrNull { it.publishedAt }
                    ?: return@withContext CheckUpdateResult(hasUpdate = false)

                val latestVersion = latest.tagName.removePrefix("v").trim()
                val currentVersion = BuildConfig.VERSION_NAME.trim()
                val hasUpdate = compareVersions(latestVersion, currentVersion) > 0
                if (!hasUpdate) {
                    return@withContext CheckUpdateResult(hasUpdate = false)
                }

                val apkAsset = latest.assets.firstOrNull {
                    it.name.endsWith(".apk", ignoreCase = true) &&
                        (it.name.contains("universal", true) || it.name.contains("arm64", true) || true)
                }
                CheckUpdateResult(
                    hasUpdate = true,
                    latestVersion = latestVersion,
                    releaseNotes = latest.body,
                    downloadUrl = apkAsset?.browserDownloadUrl.orEmpty(),
                    preRelease = latest.prerelease
                )
            } catch (e: Exception) {
                LogUtil.e(AppConfig.TAG, "Failed to check for update", e)
                CheckUpdateResult(hasUpdate = false)
            }
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
