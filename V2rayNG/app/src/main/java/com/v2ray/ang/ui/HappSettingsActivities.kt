package com.v2ray.ang.ui

import androidx.fragment.app.Fragment
import com.v2ray.ang.R

class VpnSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_vpn
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.TUNNEL)
}

class PingSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_ping
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.PING)
}

class LogsSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_logs
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
    override fun screenTitleRes() = R.string.sumrax_settings_inbound_auth
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.INBOUND)
}

class LanguageActivitySettings : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_language
    override fun createSettingsFragment(): Fragment = SettingsActivity.InterfaceSettingsFragment()
}

class UISettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_ui
    override fun createSettingsFragment(): Fragment = SettingsActivity.InterfaceSettingsFragment()
}

class SubscriptionSettingsActivity : BaseActivity() {
    override fun onCreate(savedInstanceState: android.os.Bundle?) {
        super.onCreate(savedInstanceState)
        startActivity(android.content.Intent(this, SubSettingActivity::class.java))
        finish()
    }
}

class AdvancedSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_advanced
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.ADVANCED)
}

class OtherSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_other
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.OTHER)
}

class CoreDnsSettingsActivity : HappSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.sumrax_settings_dns_network
    override fun createSettingsFragment(): Fragment =
        SettingsActivity.settingsFragment(SettingsActivity.SettingsFilter.CORE_DNS)
}
