package com.v2ray.ang.handler

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.provider.Settings
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.core.CoreServiceManager
import com.v2ray.ang.root.RootManager
import com.v2ray.ang.util.LogUtil
import java.io.File
import java.io.FileOutputStream

/**
 * Integrates [Mygod VPN Hotspot](https://github.com/Mygod/VPNHotspot) (3.0.8 bundled)
 * with Sumrak VPN: companion install/launch plus in-app LAN sharing toggles.
 */
object VpnHotspotManager {

    data class Status(
        val companionInstalled: Boolean,
        val companionVersion: String?,
        val rootAvailable: Boolean,
        val vpnCoreRunning: Boolean,
        val lanSharingEnabled: Boolean,
        val bundledApkPresent: Boolean,
    )

    fun readStatus(context: Context): Status {
        val pm = context.packageManager
        val installed = try {
            pm.getPackageInfo(AppConfig.VPN_HOTSPOT_PACKAGE, 0)
            true
        } catch (_: PackageManager.NameNotFoundException) {
            false
        }
        val version = if (installed) {
            try {
                pm.getPackageInfo(AppConfig.VPN_HOTSPOT_PACKAGE, 0).versionName
            } catch (_: Exception) {
                null
            }
        } else {
            null
        }
        return Status(
            companionInstalled = installed,
            companionVersion = version,
            rootAvailable = RootManager.cachedRoot(),
            vpnCoreRunning = CoreServiceManager.isRunning(),
            lanSharingEnabled = MmkvManager.decodeSettingsBool(AppConfig.PREF_ROOT_LAN_SHARING),
            bundledApkPresent = assetApkExists(context),
        )
    }

    private fun assetApkExists(context: Context): Boolean = try {
        context.assets.open(AppConfig.VPN_HOTSPOT_ASSET_APK).use { it.read() >= 0 }
        true
    } catch (_: Exception) {
        false
    }

    fun companionApkFile(context: Context): File {
        val out = File(context.cacheDir, "vpn-hotspot-companion.apk")
        if (out.exists() && out.length() > 0L) return out
        context.assets.open(AppConfig.VPN_HOTSPOT_ASSET_APK).use { input ->
            FileOutputStream(out).use { output -> input.copyTo(output) }
        }
        return out
    }

    fun buildInstallCompanionIntent(context: Context): Intent {
        val apk = companionApkFile(context)
        return AppUpdateRunner.buildInstallIntent(context, apk)
    }

    fun launchCompanion(context: Context): Boolean {
        val launch = context.packageManager.getLaunchIntentForPackage(AppConfig.VPN_HOTSPOT_PACKAGE)
            ?: Intent(Intent.ACTION_MAIN).apply { setClassName(AppConfig.VPN_HOTSPOT_PACKAGE, "${AppConfig.VPN_HOTSPOT_PACKAGE}.MainActivity") }
        return try {
            launch.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            context.startActivity(launch)
            true
        } catch (e: Exception) {
            LogUtil.e(AppConfig.TAG, "VpnHotspot: launch failed", e)
            false
        }
    }

    fun openSystemHotspotSettings(context: Context) {
        val intents = listOf(
            Intent("android.settings.TETHER_SETTINGS"),
            Intent(Settings.ACTION_WIRELESS_SETTINGS),
            Intent(Settings.ACTION_WIFI_SETTINGS),
        )
        for (intent in intents) {
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            if (intent.resolveActivity(context.packageManager) != null) {
                context.startActivity(intent)
                return
            }
        }
    }

    fun statusSummary(context: Context, status: Status): String {
        val parts = mutableListOf<String>()
        parts += if (status.vpnCoreRunning) {
            context.getString(R.string.vpn_hotspot_status_vpn_on)
        } else {
            context.getString(R.string.vpn_hotspot_status_vpn_off)
        }
        parts += if (status.companionInstalled) {
            context.getString(R.string.vpn_hotspot_status_companion_ok, status.companionVersion ?: "?")
        } else {
            context.getString(R.string.vpn_hotspot_status_companion_missing)
        }
        parts += if (status.rootAvailable) {
            context.getString(R.string.vpn_hotspot_status_root_ok)
        } else {
            context.getString(R.string.vpn_hotspot_status_root_missing)
        }
        return parts.joinToString("\n")
    }
}