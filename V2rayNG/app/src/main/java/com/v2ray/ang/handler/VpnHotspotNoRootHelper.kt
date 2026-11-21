package com.v2ray.ang.handler

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.core.CoreServiceManager
import com.v2ray.ang.util.Utils
import java.net.Inet4Address
import java.net.NetworkInterface

object VpnHotspotNoRootHelper {

    const val SHIZUKU_PACKAGE = "moe.shizuku.privileged.api"
    const val SHIZUKU_INSTALL_URL = "https://shizuku.rikka.app/"
    const val V2RAYNG_SHIZUKU_PR_URL = "https://github.com/2dust/v2rayNG/pull/5903"
    const val VPN_HOTSPOT_CANARY_URL = "https://github.com/Mygod/VPNHotspot/releases/tag/canary"

    data class ProxyShareInfo(
        val hostIp: String,
        val socksPort: Int,
        val httpPort: Int?,
        val authRequired: Boolean,
        val proxySharingEnabled: Boolean,
        val vpnRunning: Boolean,
        val hostIpIsGuessed: Boolean,
        val copyBlock: String,
        val qrPayload: String,
    )

    fun isProxySharingEnabled(): Boolean =
        MmkvManager.decodeSettingsBool(AppConfig.PREF_PROXY_SHARING)

    fun setProxySharingEnabled(enabled: Boolean) {
        MmkvManager.encodeSettings(AppConfig.PREF_PROXY_SHARING, enabled)
    }

    fun isShizukuInstalled(context: Context): Boolean = try {
        context.packageManager.getPackageInfo(SHIZUKU_PACKAGE, 0)
        true
    } catch (_: PackageManager.NameNotFoundException) {
        false
    }

    fun openShizukuInstall(context: Context) {
        val market = Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=$SHIZUKU_PACKAGE")).apply {
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        if (market.resolveActivity(context.packageManager) != null) {
            context.startActivity(market)
        } else {
            Utils.openUri(context, SHIZUKU_INSTALL_URL)
        }
    }

    fun supportsShizukuTethering(): Boolean = Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU

    fun buildProxyShareInfo(context: Context): ProxyShareInfo {
        val (host, guessed) = detectHotspotHostIpv4()
        val socks = SettingsManager.getSocksPort()
        val http = if (Utils.isXray()) null else SettingsManager.getHttpPort()
        val auth = SettingsManager.getSocksUsername() != null
        val enabled = isProxySharingEnabled()
        val running = CoreServiceManager.isRunning()
        val copy = buildCopyBlock(context, host, socks, http, auth)
        val qr = buildQrPayload(host, socks, http)
        return ProxyShareInfo(
            hostIp = host,
            socksPort = socks,
            httpPort = http,
            authRequired = auth,
            proxySharingEnabled = enabled,
            vpnRunning = running,
            hostIpIsGuessed = guessed,
            copyBlock = copy,
            qrPayload = qr,
        )
    }

    fun formatProxySummary(context: Context, info: ProxyShareInfo): String {
        val lines = mutableListOf<String>()
        lines += context.getString(
            if (info.vpnRunning) R.string.vpn_hotspot_status_vpn_on else R.string.vpn_hotspot_status_vpn_off
        )
        lines += context.getString(
            if (info.proxySharingEnabled) R.string.vpn_hotspot_noroot_proxy_on
            else R.string.vpn_hotspot_noroot_proxy_off
        )
        if (info.proxySharingEnabled) {
            lines += context.getString(R.string.vpn_hotspot_noroot_socks_line, info.hostIp, info.socksPort)
            info.httpPort?.let { lines += context.getString(R.string.vpn_hotspot_noroot_http_line, info.hostIp, it) }
            if (info.hostIpIsGuessed) {
                lines += context.getString(R.string.vpn_hotspot_noroot_ip_hint)
            }
            if (info.authRequired) {
                lines += context.getString(R.string.vpn_hotspot_noroot_auth_warning)
            }
        }
        return lines.joinToString("\n")
    }

    private fun buildCopyBlock(
        context: Context,
        host: String,
        socks: Int,
        http: Int?,
        auth: Boolean,
    ): String = buildString {
        appendLine(context.getString(R.string.vpn_hotspot_noroot_copy_header))
        appendLine(context.getString(R.string.vpn_hotspot_noroot_socks_line, host, socks))
        http?.let { appendLine(context.getString(R.string.vpn_hotspot_noroot_http_line, host, it)) }
        if (auth) appendLine(context.getString(R.string.vpn_hotspot_noroot_auth_warning))
        appendLine()
        append(context.getString(R.string.vpn_hotspot_noroot_client_steps_short))
    }

    private fun buildQrPayload(host: String, socks: Int, http: Int?): String = buildString {
        appendLine("sumrak-vpn-hotspot-proxy")
        appendLine("socks=$host:$socks")
        http?.let { appendLine("http=$host:$it") }
        append("type=manual-wifi-proxy")
    }

  /**
   * Likely IPv4 of this device on the hotspot/tether interface (gateway for clients).
   * Falls back to a common Android default when tethering is not active yet.
   */
    private fun detectHotspotHostIpv4(): Pair<String, Boolean> {
        val candidates = mutableListOf<String>()
        try {
            val interfaces = NetworkInterface.getNetworkInterfaces() ?: return fallbackIp()
            for (nif in interfaces) {
                if (!nif.isUp || nif.isLoopback) continue
                val name = nif.name.lowercase()
                val tetherLike = name.startsWith("ap") || name.startsWith("wlan") ||
                    name.contains("rndis") || name.contains("softap") || name.contains("swlan")
                for (addr in nif.inetAddresses) {
                    if (addr !is Inet4Address || addr.isLoopbackAddress) continue
                    val host = addr.hostAddress ?: continue
                    if (!host.startsWith("192.168.") && !host.startsWith("10.")) continue
                    if (tetherLike || host.endsWith(".1")) {
                        candidates += host
                    }
                }
            }
        } catch (_: Exception) {
            return fallbackIp()
        }
        val best = candidates.firstOrNull { it.endsWith(".1") } ?: candidates.firstOrNull()
        return if (best != null) best to false else fallbackIp()
    }

    private fun fallbackIp(): Pair<String, Boolean> = "192.168.43.1" to true
}