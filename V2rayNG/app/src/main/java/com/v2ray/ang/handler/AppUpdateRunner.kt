package com.v2ray.ang.handler

import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import androidx.core.content.FileProvider
import com.v2ray.ang.AppConfig
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.R
import com.v2ray.ang.dto.CheckUpdateResult
import com.v2ray.ang.dto.UrlContentRequest
import com.v2ray.ang.enums.NotificationChannelType
import com.v2ray.ang.util.HttpUtil
import com.v2ray.ang.util.LogUtil
import com.v2ray.ang.util.NotificationHelper
import java.io.File

object AppUpdateRunner {

  data class RunResult(
      val checked: Boolean,
      val updateAvailable: Boolean = false,
      val downloaded: Boolean = false,
      val latestVersion: String = "",
  )

  suspend fun checkAndMaybeDownload(context: Context): RunResult {
    if (!SettingsManager.isAutoCheckUpdateEnabled()) {
      return RunResult(checked = false)
    }
    if (SettingsManager.getAppUpdateApiUrl().isBlank()) {
      return RunResult(checked = false)
    }
    val includePre = MmkvManager.decodeSettingsBool(AppConfig.PREF_CHECK_UPDATE_PRE_RELEASE, false)
    val result = UpdateCheckerManager.checkForUpdate(includePre)
    if (!result.hasUpdate) {
      return RunResult(checked = true, updateAvailable = false)
    }
    if (!SettingsManager.isAutoDownloadAppUpdateEnabled()) {
      notifyUpdateAvailable(context, result)
      return RunResult(checked = true, updateAvailable = true, latestVersion = result.latestVersion)
    }
    val downloaded = downloadUpdateApk(context, result)
    if (downloaded) {
      MmkvManager.encodeSettings(AppConfig.PREF_LAST_APP_UPDATE_VERSION, result.latestVersion)
      val apkFile = apkFileForVersion(context, result.latestVersion)
      if (SettingsManager.isAutoInstallAppUpdateEnabled()) {
        try {
          context.startActivity(buildInstallIntent(context, apkFile))
        } catch (e: Exception) {
          LogUtil.e(AppConfig.TAG, "Auto-install intent failed", e)
        }
      }
      notifyInstallReady(context, result.latestVersion, apkFile)
    } else {
      notifyUpdateAvailable(context, result)
    }
    return RunResult(
        checked = true,
        updateAvailable = true,
        downloaded = downloaded,
        latestVersion = result.latestVersion,
    )
  }

  fun apkFileForVersion(context: Context, version: String): File {
    val safe = version.replace(Regex("[^a-zA-Z0-9._-]"), "_")
    return File(context.cacheDir, "SumrakVPN-$safe.apk")
  }

  private fun downloadUpdateApk(context: Context, result: CheckUpdateResult): Boolean {
    val url = result.downloadUrl
    if (url.isBlank()) return false
    val target = apkFileForVersion(context, result.latestVersion)
    val lastStored = MmkvManager.decodeSettingsString(AppConfig.PREF_LAST_APP_UPDATE_VERSION).orEmpty()
    if (lastStored == result.latestVersion && target.exists() && target.length() > 0L) {
      LogUtil.i(AppConfig.TAG, "AppUpdateRunner: APK already cached for ${result.latestVersion}")
      return true
    }
    val ok = HttpUtil.downloadToFile(
        UrlContentRequest(url = url, timeout = 120_000),
        target,
    )
    if (!ok) {
      target.delete()
    }
    return ok && target.exists() && target.length() > 0L
  }

  private fun notifyUpdateAvailable(context: Context, result: CheckUpdateResult) {
    NotificationHelper.notify(
        NotificationChannelType.APP_UPDATE,
        context,
        context.getString(R.string.update_new_version_found, result.latestVersion),
        context.getString(R.string.summary_pref_check_update_now),
    )
  }

  private fun notifyInstallReady(context: Context, version: String, apkFile: File) {
    val installIntent = buildInstallIntent(context, apkFile)
    val pending = PendingIntent.getActivity(
        context,
        0,
        installIntent,
        PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
    )
    NotificationHelper.notifyWithAction(
        channelType = NotificationChannelType.APP_UPDATE,
        context = context,
        title = context.getString(R.string.notification_app_update_ready_title, version),
        content = context.getString(R.string.notification_app_update_ready_body),
        actionTitle = context.getString(R.string.notification_app_update_install),
        actionIntent = pending,
    )
  }

  fun buildInstallIntent(context: Context, apkFile: File): Intent {
    val uri = FileProvider.getUriForFile(context, "${BuildConfig.APPLICATION_ID}.cache", apkFile)
    return Intent(Intent.ACTION_VIEW).apply {
      setDataAndType(uri, "application/vnd.android.package-archive")
      addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
      addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    }
  }
}