package com.v2ray.ang.ui

import androidx.fragment.app.Fragment
import com.v2ray.ang.R

class VpnSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_vpn_settings
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.TUNNEL)
}

class PingSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_ping
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.PING)
}

class LogsSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_logs
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.LOGS)
}

class LogsViewSettingsActivity : BaseActivity() {
    override fun onCreate(savedInstanceState: android.os.Bundle?) {
        super.onCreate(savedInstanceState)
        startActivity(android.content.Intent(this, LogcatActivity::class.java))
        finish()
    }
}

class InboundAuthActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.inbound_authorization_title
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.INBOUND)
}

class LanguageActivitySettings : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_language
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.LANGUAGE)
}

class UISettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_ui_settings
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.UI_DETAILS)
}

class UiModeSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_pref_ui_mode_night
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.UI_MODE)
}

class TunnelOptionsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_tunnel
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.TUNNEL_OPTIONS)
}

class LanPortsSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_pref_proxy_sharing_enabled
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.LAN_PORTS)
}

class AutoStartSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_pref_auto_start_vpn
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.AUTO_START)
}

class SubscriptionSettingsActivity : BaseActivity() {
    override fun onCreate(savedInstanceState: android.os.Bundle?) {
        super.onCreate(savedInstanceState)
        startActivity(android.content.Intent(this, SubSettingActivity::class.java))
        finish()
    }
}

class AdvancedSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_advanced
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.ADVANCED)
}

class OtherSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_other
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.OTHER)
}

class CoreDnsSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_dns_network
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.CORE_DNS)
}
