package com.v2ray.ang.handler

import android.annotation.SuppressLint
import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.NetworkType
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkerParameters
import androidx.work.multiprocess.RemoteWorkManager
import androidx.work.workDataOf
import com.v2ray.ang.AngApplication
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.dto.entities.SubscriptionCache
import com.v2ray.ang.enums.NotificationChannelType
import com.v2ray.ang.util.LogUtil
import com.v2ray.ang.util.NotificationHelper
import java.util.concurrent.TimeUnit

object SubscriptionUpdater {

    private const val GLOBAL_TASK_NAME = "${AppConfig.SUBSCRIPTION_UPDATE_TASK_NAME}_all"
    private const val GLOBAL_INTERVAL_MINUTES = 30L
    private const val KEY_SUB_ID = "subId"
    private const val KEY_UPDATE_ALL = "updateAll"

    /**
     * Sync subscription auto-update with current settings.
     * Schedules a single global job that refreshes all subscriptions every 30 minutes.
     */
    fun sync(
        context: Context = AngApplication.application,
        forceReschedule: Boolean = false
    ) {
        cancelLegacyPerSubscriptionTasks(context)

        val enabled = SettingsManager.isSubscriptionAutoUpdateEnabled() &&
            MmkvManager.decodeSubscriptions().any {
                it.subscription.enabled && it.subscription.url.isNotBlank()
            }

        val rw = RemoteWorkManager.getInstance(context)
        if (!enabled) {
            rw.cancelUniqueWork(GLOBAL_TASK_NAME)
            LogUtil.i(AppConfig.TAG, "SubscriptionUpdater: global auto-update cancelled")
            return
        }

        val policy = if (forceReschedule) {
            ExistingPeriodicWorkPolicy.UPDATE
        } else {
            ExistingPeriodicWorkPolicy.KEEP
        }

        val request = PeriodicWorkRequestBuilder<UpdateTask>(GLOBAL_INTERVAL_MINUTES, TimeUnit.MINUTES)
            .setConstraints(
                Constraints.Builder()
                    .setRequiredNetworkType(NetworkType.CONNECTED)
                    .build()
            )
            .setInputData(workDataOf(KEY_UPDATE_ALL to true))
            .setInitialDelay(GLOBAL_INTERVAL_MINUTES, TimeUnit.MINUTES)
            .addTag(AppConfig.SUBSCRIPTION_UPDATE_TASK_NAME)
            .build()

        rw.enqueueUniquePeriodicWork(GLOBAL_TASK_NAME, policy, request)
        LogUtil.i(
            AppConfig.TAG,
            "SubscriptionUpdater: global sync every ${GLOBAL_INTERVAL_MINUTES}min policy=$policy"
        )
    }

    /**
     * After saving a subscription, refresh the global auto-update schedule.
     */
    fun syncOne(context: Context = AngApplication.application, subId: String) {
        sync(context, forceReschedule = true)
        LogUtil.d(AppConfig.TAG, "SubscriptionUpdater: syncOne($subId) -> global reschedule")
    }

    fun cancelOne(context: Context = AngApplication.application, subId: String) {
        RemoteWorkManager.getInstance(context)
            .cancelUniqueWork("${AppConfig.SUBSCRIPTION_UPDATE_TASK_NAME}_$subId")
        sync(context, forceReschedule = true)
    }

    fun cancelAll(context: Context = AngApplication.application) {
        cancelLegacyPerSubscriptionTasks(context)
        RemoteWorkManager.getInstance(context).cancelUniqueWork(GLOBAL_TASK_NAME)
    }

    private fun cancelLegacyPerSubscriptionTasks(context: Context) {
        val rw = RemoteWorkManager.getInstance(context)
        MmkvManager.decodeSubscriptions().forEach { sub ->
            rw.cancelUniqueWork("${AppConfig.SUBSCRIPTION_UPDATE_TASK_NAME}_${sub.guid}")
        }
    }

    class UpdateTask(context: Context, params: WorkerParameters) :
        CoroutineWorker(context, params) {

        @SuppressLint("MissingPermission")
        override suspend fun doWork(): Result {
            val updateAll = inputData.getBoolean(KEY_UPDATE_ALL, false)
            val subId = inputData.getString(KEY_SUB_ID)

            if (updateAll || subId.isNullOrEmpty()) {
                return updateAllSubscriptions()
            }
            return updateOne(subId)
        }

        private suspend fun updateAllSubscriptions(): Result {
            if (!SettingsManager.isSubscriptionAutoUpdateEnabled()) {
                LogUtil.i(AppConfig.TAG, "SubscriptionUpdater: global auto-update disabled, skip")
                return Result.success()
            }

            val targets = MmkvManager.decodeSubscriptions().filter {
                it.subscription.enabled && it.subscription.url.isNotBlank()
            }
            if (targets.isEmpty()) {
                return Result.success()
            }

            NotificationHelper.notify(
                NotificationChannelType.SUBSCRIPTION_UPDATE,
                applicationContext,
                applicationContext.getString(R.string.title_pref_auto_update_subscription),
                applicationContext.getString(R.string.sumrax_refresh_subs)
            )

            LogUtil.i(AppConfig.TAG, "SubscriptionUpdater: updating ${targets.size} subscriptions")
            targets.forEach { cache ->
                try {
                    AngConfigManager.updateConfigViaSub(cache)
                } catch (e: Exception) {
                    LogUtil.e(AppConfig.TAG, "SubscriptionUpdater failed: ${cache.subscription.remarks}", e)
                }
            }

            NotificationHelper.cancel(NotificationChannelType.SUBSCRIPTION_UPDATE, applicationContext)
            return Result.success()
        }

        private suspend fun updateOne(subId: String): Result {
            val subItem = MmkvManager.decodeSubscription(subId) ?: return Result.success()
            if (!SettingsManager.isSubscriptionAutoUpdateEnabled() && !subItem.autoUpdate) {
                return Result.success()
            }
            if (!subItem.enabled || subItem.url.isBlank()) {
                return Result.success()
            }

            NotificationHelper.notify(
                NotificationChannelType.SUBSCRIPTION_UPDATE,
                applicationContext,
                applicationContext.getString(R.string.title_pref_auto_update_subscription),
                "Updating ${subItem.remarks}"
            )
            AngConfigManager.updateConfigViaSub(SubscriptionCache(subId, subItem))
            NotificationHelper.cancel(NotificationChannelType.SUBSCRIPTION_UPDATE, applicationContext)
            return Result.success()
        }
    }
}
