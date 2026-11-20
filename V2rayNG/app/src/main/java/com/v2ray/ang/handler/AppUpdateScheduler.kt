package com.v2ray.ang.handler

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import androidx.work.multiprocess.RemoteWorkManager
import com.v2ray.ang.AngApplication
import com.v2ray.ang.AppConfig
import com.v2ray.ang.util.LogUtil
import java.util.concurrent.TimeUnit

object AppUpdateScheduler {

  private const val PERIODIC_TASK_NAME = "${AppConfig.APP_UPDATE_TASK_NAME}_periodic"
  private const val ONE_SHOT_TASK_NAME = "${AppConfig.APP_UPDATE_TASK_NAME}_once"
  private const val INTERVAL_HOURS = 6L

  fun sync(context: Context = AngApplication.application, forceReschedule: Boolean = false) {
    val enabled = SettingsManager.isAutoCheckUpdateEnabled() &&
        SettingsManager.getAppUpdateApiUrl().isNotBlank()
    val rw = RemoteWorkManager.getInstance(context)
    if (!enabled) {
      rw.cancelUniqueWork(PERIODIC_TASK_NAME)
      LogUtil.i(AppConfig.TAG, "AppUpdateScheduler: cancelled")
      return
    }
    val policy = if (forceReschedule) {
      ExistingPeriodicWorkPolicy.UPDATE
    } else {
      ExistingPeriodicWorkPolicy.KEEP
    }
    val request = PeriodicWorkRequestBuilder<AppUpdateWorker>(INTERVAL_HOURS, TimeUnit.HOURS)
        .setConstraints(
            Constraints.Builder()
                .setRequiredNetworkType(NetworkType.CONNECTED)
                .build()
        )
        .addTag(AppConfig.APP_UPDATE_TASK_NAME)
        .build()
    rw.enqueueUniquePeriodicWork(PERIODIC_TASK_NAME, policy, request)
    LogUtil.i(AppConfig.TAG, "AppUpdateScheduler: periodic every ${INTERVAL_HOURS}h")
  }

  fun enqueueImmediate(context: Context = AngApplication.application) {
    if (!SettingsManager.isAutoCheckUpdateEnabled()) return
    if (SettingsManager.getAppUpdateApiUrl().isBlank()) return
    val request = OneTimeWorkRequestBuilder<AppUpdateWorker>()
        .setConstraints(
            Constraints.Builder()
                .setRequiredNetworkType(NetworkType.CONNECTED)
                .build()
        )
        .addTag(AppConfig.APP_UPDATE_TASK_NAME)
        .build()
    WorkManager.getInstance(context).enqueueUniqueWork(
        ONE_SHOT_TASK_NAME,
        ExistingWorkPolicy.REPLACE,
        request,
    )
  }

  class AppUpdateWorker(
      context: Context,
      params: WorkerParameters,
  ) : CoroutineWorker(context, params) {

    override suspend fun doWork(): Result {
      return try {
        AppUpdateRunner.checkAndMaybeDownload(applicationContext)
        Result.success()
      } catch (e: Exception) {
        LogUtil.e(AppConfig.TAG, "AppUpdateWorker failed", e)
        Result.retry()
      }
    }
  }
}