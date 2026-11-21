package com.v2ray.ang.ui

import android.os.Bundle
import android.view.View
import android.widget.CompoundButton
import androidx.appcompat.app.AlertDialog
import com.v2ray.ang.R
import com.v2ray.ang.core.CoreServiceManager
import com.v2ray.ang.databinding.ActivityVpnHotspotBinding
import com.v2ray.ang.extension.toast
import com.v2ray.ang.handler.VpnHotspotNoRootHelper
import com.v2ray.ang.handler.VpnHotspotManager
import com.v2ray.ang.root.RootLanSharing
import com.v2ray.ang.util.QRCodeDecoder
import com.v2ray.ang.util.Utils

class VpnHotspotActivity : BaseActivity() {

    private val binding by lazy { ActivityVpnHotspotBinding.inflate(layoutInflater) }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(binding.root, showHomeAsUp = true, title = getString(R.string.title_vpn_hotspot))

        binding.switchLanSharing.setOnCheckedChangeListener(lanSharingListener)
        binding.switchProxySharing.setOnCheckedChangeListener(proxySharingListener)
        binding.btnInstallCompanion.setOnClickListener { installCompanion() }
        binding.btnOpenCompanion.setOnClickListener { openCompanion() }
        binding.btnSystemHotspot.setOnClickListener { VpnHotspotManager.openSystemHotspotSettings(this) }
        binding.btnUpstreamSource.setOnClickListener {
            Utils.openUri(this, "https://github.com/Mygod/VPNHotspot")
        }
        binding.btnCopyProxy.setOnClickListener { copyProxyInstructions() }
        binding.btnReconnectVpn.setOnClickListener { reconnectVpn() }
        binding.btnInstallShizuku.setOnClickListener { VpnHotspotNoRootHelper.openShizukuInstall(this) }
        binding.btnShizukuDocs.setOnClickListener {
            Utils.openUri(this, VpnHotspotNoRootHelper.V2RAYNG_SHIZUKU_PR_URL)
        }
    }

    override fun onResume() {
        super.onResume()
        refreshUi()
    }

    private val lanSharingListener = CompoundButton.OnCheckedChangeListener { _, isChecked ->
        if (binding.switchLanSharing.isPressed) {
            RootLanSharing.setEnabled(applicationContext, isChecked)
            if (isChecked && !VpnHotspotManager.readStatus(this).rootAvailable) {
                toast(R.string.vpn_hotspot_root_required)
            }
            refreshUi()
        }
    }

    private val proxySharingListener = CompoundButton.OnCheckedChangeListener { _, isChecked ->
        if (!binding.switchProxySharing.isPressed) return@OnCheckedChangeListener
        VpnHotspotNoRootHelper.setProxySharingEnabled(isChecked)
        if (isChecked) {
            toast(R.string.toast_warning_pref_proxysharing_short)
        }
        val needsReconnect = CoreServiceManager.isRunning()
        binding.btnReconnectVpn.visibility = if (needsReconnect && isChecked) View.VISIBLE else View.GONE
        if (needsReconnect) {
            toast(R.string.vpn_hotspot_noroot_reconnect_hint)
        }
        refreshUi()
    }

    private fun refreshUi() {
        val status = VpnHotspotManager.readStatus(this)
        binding.tvStatus.text = VpnHotspotManager.statusSummary(this, status)
        binding.tvSteps.text = getString(R.string.vpn_hotspot_steps)

        binding.switchLanSharing.setOnCheckedChangeListener(null)
        binding.switchLanSharing.isChecked = status.lanSharingEnabled
        binding.switchLanSharing.setOnCheckedChangeListener(lanSharingListener)
        binding.switchLanSharing.isEnabled = status.rootAvailable

        binding.btnOpenCompanion.isEnabled = status.companionInstalled
        binding.btnInstallCompanion.isEnabled = status.bundledApkPresent && !status.companionInstalled
        binding.btnInstallCompanion.text = if (!status.bundledApkPresent) {
            getString(R.string.vpn_hotspot_install_missing_asset)
        } else {
            getString(R.string.vpn_hotspot_install_companion)
        }

        refreshNoRootSection()
        refreshShizukuSection()
    }

    private fun refreshNoRootSection() {
        val info = VpnHotspotNoRootHelper.buildProxyShareInfo(this)
        binding.switchProxySharing.setOnCheckedChangeListener(null)
        binding.switchProxySharing.isChecked = info.proxySharingEnabled
        binding.switchProxySharing.setOnCheckedChangeListener(proxySharingListener)

        binding.tvNorootProxySummary.text = VpnHotspotNoRootHelper.formatProxySummary(this, info)

        val showQr = info.proxySharingEnabled && info.vpnRunning
        binding.ivProxyQr.visibility = if (showQr) View.VISIBLE else View.GONE
        binding.btnCopyProxy.isEnabled = info.proxySharingEnabled
        if (showQr) {
            val bmp = QRCodeDecoder.createQRCode(info.qrPayload, 512)
            binding.ivProxyQr.setImageBitmap(bmp)
        } else {
            binding.ivProxyQr.setImageDrawable(null)
        }

        binding.btnReconnectVpn.visibility =
            if (info.proxySharingEnabled && info.vpnRunning) View.VISIBLE else View.GONE
    }

    private fun refreshShizukuSection() {
        val installed = VpnHotspotNoRootHelper.isShizukuInstalled(this)
        val apiOk = VpnHotspotNoRootHelper.supportsShizukuTethering()
        binding.tvShizukuStatus.text = when {
            !apiOk -> getString(R.string.vpn_hotspot_shizuku_api_low)
            installed -> getString(R.string.vpn_hotspot_shizuku_installed)
            else -> getString(R.string.vpn_hotspot_shizuku_missing)
        }
        binding.btnInstallShizuku.text = if (installed) {
            getString(R.string.vpn_hotspot_shizuku_open_app)
        } else {
            getString(R.string.vpn_hotspot_shizuku_install)
        }
        binding.btnInstallShizuku.setOnClickListener {
            if (VpnHotspotNoRootHelper.isShizukuInstalled(this)) {
                packageManager.getLaunchIntentForPackage(VpnHotspotNoRootHelper.SHIZUKU_PACKAGE)?.let {
                    startActivity(it)
                } ?: VpnHotspotNoRootHelper.openShizukuInstall(this)
            } else {
                VpnHotspotNoRootHelper.openShizukuInstall(this)
            }
        }
    }

    private fun copyProxyInstructions() {
        val info = VpnHotspotNoRootHelper.buildProxyShareInfo(this)
        if (!info.proxySharingEnabled) {
            toast(R.string.vpn_hotspot_noroot_enable_first)
            return
        }
        Utils.setClipboard(this, info.copyBlock)
        toast(R.string.toast_success)
    }

    private fun reconnectVpn() {
        if (!CoreServiceManager.isRunning()) {
            toast(R.string.vpn_hotspot_status_vpn_off)
            return
        }
        CoreServiceManager.stopVService(this)
        binding.root.postDelayed({
            try {
                CoreServiceManager.startVServiceFromToggle(this)
            } catch (_: Exception) {
                toast(R.string.toast_services_failure)
            }
            refreshUi()
        }, 400)
    }

    private fun installCompanion() {
        if (!VpnHotspotManager.readStatus(this).bundledApkPresent) {
            toast(R.string.vpn_hotspot_install_missing_asset)
            return
        }
        AlertDialog.Builder(this)
            .setTitle(R.string.vpn_hotspot_install_title)
            .setMessage(R.string.vpn_hotspot_install_message)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                try {
                    startActivity(VpnHotspotManager.buildInstallCompanionIntent(this))
                } catch (e: Exception) {
                    toast(e.message ?: getString(R.string.toast_services_failure))
                }
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun openCompanion() {
        if (!VpnHotspotManager.launchCompanion(this)) {
            toast(R.string.vpn_hotspot_launch_failed)
        }
    }
}