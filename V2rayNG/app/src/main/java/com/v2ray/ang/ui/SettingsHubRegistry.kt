package com.v2ray.ang.ui

import com.v2ray.ang.R
import java.text.Collator

/**
 * Settings hub rows grouped by tab; titles sorted within each tab (locale-aware).
 */
object SettingsHubRegistry {

    enum class Tab {
        ALL, UI, TUNNEL, ADVANCED, OTHER, ABOUT
    }

    data class Entry(
        val titleRes: Int,
        val activityClass: Class<*>,
        val tab: Tab
    )

    /** Row order mirrors Happ 4.11.2 `fragment_*_settings.xml` (forwards only where Happ uses sub-screens). */
    private val catalog: List<Entry> = listOf(
        Entry(R.string.title_language, LanguageActivitySettings::class.java, Tab.UI),
        Entry(R.string.title_pref_ui_mode_night, UiModeSettingsActivity::class.java, Tab.UI),
        Entry(R.string.title_ui_settings, UISettingsActivity::class.java, Tab.UI),

        Entry(R.string.routing_settings_title, RoutingSettingActivity::class.java, Tab.TUNNEL),
        Entry(R.string.title_pref_per_app_proxy, PerAppProxyActivity::class.java, Tab.TUNNEL),
        Entry(R.string.inbound_authorization_title, InboundAuthActivity::class.java, Tab.TUNNEL),
        Entry(R.string.title_tunnel, TunnelOptionsActivity::class.java, Tab.TUNNEL),

        Entry(R.string.title_vpn_settings, VpnSettingsActivity::class.java, Tab.ADVANCED),
        Entry(R.string.title_subscription, SubscriptionSettingsActivity::class.java, Tab.ADVANCED),
        Entry(R.string.title_ping, PingSettingsActivity::class.java, Tab.ADVANCED),
        Entry(R.string.title_pref_send_hwid_header, HwidSettingsActivity::class.java, Tab.ADVANCED),
        Entry(R.string.title_pref_proxy_sharing_enabled, LanPortsSettingsActivity::class.java, Tab.ADVANCED),
        Entry(R.string.title_pref_auto_start_vpn, AutoStartSettingsActivity::class.java, Tab.ADVANCED),
        Entry(R.string.title_advanced, AdvancedSettingsActivity::class.java, Tab.ADVANCED),
        Entry(R.string.title_core_dns, CoreDnsSettingsActivity::class.java, Tab.ADVANCED),

        Entry(R.string.title_pref_update_button_check, CheckUpdateActivity::class.java, Tab.OTHER),
        Entry(R.string.title_statistics, StatisticsSettingsActivity::class.java, Tab.OTHER),
        Entry(R.string.title_logs, LogsSettingsActivity::class.java, Tab.OTHER),
        Entry(R.string.title_other, OtherSettingsActivity::class.java, Tab.OTHER),
        Entry(R.string.title_reset, ResetSettingsActivity::class.java, Tab.OTHER),

        Entry(R.string.title_faq, FaqActivity::class.java, Tab.ABOUT),
        Entry(R.string.title_url_schemes, UrlSchemesActivity::class.java, Tab.ABOUT),
        Entry(R.string.title_about, AboutActivity::class.java, Tab.ABOUT),
    )

    /** Happ section order (fixed; not A–Z). */
    fun entriesForTab(tab: Tab): List<Entry> = catalog.filter { it.tab == tab }

    fun entriesFor(tab: Tab, titleOf: (Int) -> String, collator: Collator): List<Entry> {
        val list = when (tab) {
            Tab.ALL -> catalog
            else -> entriesForTab(tab)
        }
        return if (tab == Tab.ALL) {
            list.sortedWith(compareBy(collator) { titleOf(it.titleRes) })
        } else {
            list
        }
    }

    fun tabTitleRes(tab: Tab): Int = when (tab) {
        Tab.ALL -> R.string.sumrax_settings_tab_all
        Tab.UI -> R.string.sumrax_settings_tab_ui
        Tab.TUNNEL -> R.string.sumrax_settings_tab_tunnel
        Tab.ADVANCED -> R.string.sumrax_settings_tab_advanced
        Tab.OTHER -> R.string.sumrax_settings_tab_other
        Tab.ABOUT -> R.string.sumrax_settings_tab_about
    }

    val tabs: List<Tab> = Tab.entries
}