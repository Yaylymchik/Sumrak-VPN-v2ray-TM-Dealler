package com.v2ray.ang.ui

import android.os.Bundle
import androidx.appcompat.app.AlertDialog
import com.v2ray.ang.R
import com.v2ray.ang.extension.toast
import com.v2ray.ang.handler.MonthlyTrafficTracker
import com.v2ray.ang.handler.SettingsChangeManager
import com.v2ray.ang.handler.SettingsManager

class ResetSettingsActivity : BaseActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(
            R.layout.activity_reset_settings,
            showHomeAsUp = true,
            title = getString(R.string.sumrax_settings_reset)
        )

        findViewById<com.google.android.material.button.MaterialButton>(R.id.btn_reset_user_settings)
            .setOnClickListener { confirmResetUserSettings() }
        findViewById<com.google.android.material.button.MaterialButton>(R.id.btn_reset_routing)
            .setOnClickListener { confirmResetRouting() }
        findViewById<com.google.android.material.button.MaterialButton>(R.id.btn_reset_traffic)
            .setOnClickListener { confirmResetTraffic() }
    }

    private fun confirmResetUserSettings() {
        AlertDialog.Builder(this)
            .setTitle(R.string.sumrax_reset_user_settings)
            .setMessage(R.string.sumrax_reset_user_settings_description)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                SettingsManager.resetUserSettings()
                SettingsChangeManager.makeRestartService()
                toast(R.string.sumrax_reset_done)
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun confirmResetRouting() {
        AlertDialog.Builder(this)
            .setTitle(R.string.sumrax_reset_routing)
            .setMessage(R.string.sumrax_reset_routing_description)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                SettingsManager.resetRoutingRulesetsFromPresets(this, 0)
                SettingsChangeManager.makeRestartService()
                toast(R.string.sumrax_reset_done)
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun confirmResetTraffic() {
        AlertDialog.Builder(this)
            .setTitle(R.string.sumrax_reset_traffic)
            .setMessage(R.string.sumrax_reset_traffic_description)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                MonthlyTrafficTracker.resetCurrentMonthCounters()
                toast(R.string.sumrax_reset_done)
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }
}
