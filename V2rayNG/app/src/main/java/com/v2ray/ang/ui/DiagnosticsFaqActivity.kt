package com.v2ray.ang.ui

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.widget.TextView
import androidx.lifecycle.lifecycleScope
import com.google.android.material.button.MaterialButton
import com.v2ray.ang.AppConfig
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.R
import com.v2ray.ang.core.CoreServiceManager
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.handler.SettingsManager
import com.v2ray.ang.extension.toast
import com.v2ray.ang.extension.toastSuccess
import com.v2ray.ang.viewmodel.MainViewModel
import androidx.activity.viewModels
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class DiagnosticsActivity : BaseActivity() {
    private val mainViewModel: MainViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(
            R.layout.activity_diagnostics,
            showHomeAsUp = true,
            title = getString(R.string.sumrax_settings_diagnostics)
        )

        val summary = findViewById<TextView>(R.id.tv_diagnostics_summary)
        summary.text = buildSummary()

        findViewById<MaterialButton>(R.id.btn_test_ping).setOnClickListener {
            if (mainViewModel.isRunning.value != true && !CoreServiceManager.isRunning()) {
                toast(getString(R.string.sumrax_diagnostics_need_connection))
                return@setOnClickListener
            }
            toast(getString(R.string.connection_test_testing))
            mainViewModel.testCurrentServerRealPing()
            lifecycleScope.launch {
                delay(2500)
                summary.text = buildSummary() + "\n\n" + getString(
                    R.string.sumrax_diagnostics_ping_hint
                )
            }
        }

        findViewById<MaterialButton>(R.id.btn_open_logs).setOnClickListener {
            startActivity(Intent(this, LogcatActivity::class.java))
        }

        findViewById<MaterialButton>(R.id.btn_share_diagnostics).setOnClickListener {
            val text = buildSummary()
            val clipboard = getSystemService(CLIPBOARD_SERVICE) as ClipboardManager
            clipboard.setPrimaryClip(ClipData.newPlainText("SumraX diagnostics", text))
            toastSuccess(R.string.toast_success)
            val share = Intent(Intent.ACTION_SEND).apply {
                type = "text/plain"
                putExtra(Intent.EXTRA_TEXT, text)
            }
            startActivity(Intent.createChooser(share, getString(R.string.sumrax_diagnostics_share)))
        }
    }

    private fun buildSummary(): String {
        val selected = MmkvManager.getSelectServer()
        val profile = selected?.let { MmkvManager.decodeServerConfig(it) }
        val mode = if (SettingsManager.isVpnMode()) "VPN" else "Proxy"
        val connectMode = if (SettingsManager.isSmartConnectionMode()) "Smart" else "Manual"
        val running = CoreServiceManager.isRunning()
        return buildString {
            appendLine("SumraX ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})")
            appendLine("Mode: $mode")
            appendLine("Connect: $connectMode")
            appendLine("Running: $running")
            appendLine("Server: ${profile?.remarks ?: "—"}")
            appendLine("Address: ${profile?.server ?: "—"}:${profile?.serverPort ?: "—"}")
            appendLine("Ping type: ${MmkvManager.decodeSettingsString(AppConfig.PREF_PING_TYPE, "tcp")}")
            appendLine("Routing: ${SettingsManager.isRoutingEnabled()}")
            appendLine("Hev TUN: ${SettingsManager.isUsingHevTun()}")
            appendLine("Fragment: ${MmkvManager.decodeSettingsBool(AppConfig.PREF_FRAGMENT_ENABLED, false)}")
            appendLine("Noise: ${MmkvManager.decodeSettingsBool(AppConfig.PREF_NOISE_ENABLED, false)}")
        }
    }
}

class FaqActivity : BaseActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(
            R.layout.activity_faq,
            showHomeAsUp = true,
            title = getString(R.string.sumrax_settings_faq)
        )
        findViewById<MaterialButton>(R.id.btn_open_happ_faq).setOnClickListener {
            val uri = Uri.parse("https://www.happ.su/main")
            startActivity(Intent(Intent.ACTION_VIEW, uri))
        }
    }
}
