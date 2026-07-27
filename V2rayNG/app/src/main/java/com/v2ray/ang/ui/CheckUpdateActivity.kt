package com.v2ray.ang.ui

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.widget.TextView
import androidx.lifecycle.lifecycleScope
import com.google.android.material.button.MaterialButton
import com.google.android.material.checkbox.MaterialCheckBox
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.dto.CheckUpdateResult
import com.v2ray.ang.extension.toast
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.handler.SettingsManager
import com.v2ray.ang.handler.UpdateCheckerManager
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class CheckUpdateActivity : BaseActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(
            R.layout.activity_check_update,
            showHomeAsUp = true,
            title = getString(R.string.update_check_for_update)
        )

        val preRelease = findViewById<MaterialCheckBox>(R.id.check_pre_release)
        val status = findViewById<TextView>(R.id.tv_update_status)
        preRelease.isChecked = MmkvManager.decodeSettingsBool(AppConfig.PREF_CHECK_UPDATE_PRE_RELEASE, false)
        preRelease.setOnCheckedChangeListener { _, isChecked ->
            MmkvManager.encodeSettings(AppConfig.PREF_CHECK_UPDATE_PRE_RELEASE, isChecked)
        }

        findViewById<MaterialButton>(R.id.layout_check_update).setOnClickListener {
            if (SettingsManager.getAppUpdateApiUrl().isBlank()) {
                toast(getString(R.string.summary_pref_app_update_api_url))
                return@setOnClickListener
            }
            status.setText(R.string.update_checking_for_update)
            lifecycleScope.launch {
                val result = withContext(Dispatchers.IO) {
                    UpdateCheckerManager.checkForUpdate(preRelease.isChecked)
                }
                if (result.hasUpdate) {
                    showUpdateDialog(result)
                    status.text = getString(R.string.update_new_version_found, result.latestVersion)
                } else {
                    status.setText(R.string.update_already_latest_version)
                }
            }
        }
    }

    private fun showUpdateDialog(result: CheckUpdateResult) {
        androidx.appcompat.app.AlertDialog.Builder(this)
            .setTitle(getString(R.string.update_new_version_found, result.latestVersion))
            .setMessage(result.releaseNotes.ifBlank { getString(R.string.update_check_for_update) })
            .setPositiveButton(android.R.string.ok) { _, _ ->
                if (result.downloadUrl.isNotBlank()) {
                    startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(result.downloadUrl)))
                }
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }
}
