package com.v2ray.ang.ui

import android.content.Intent
import android.os.Bundle
import android.view.View
import androidx.activity.OnBackPressedCallback
import androidx.core.os.bundleOf
import androidx.preference.Preference
import androidx.preference.PreferenceCategory
import androidx.preference.CheckBoxPreference
import androidx.recyclerview.widget.RecyclerView
import androidx.preference.EditTextPreference
import androidx.preference.ListPreference
import androidx.preference.PreferenceFragmentCompat
import com.v2ray.ang.AppConfig
import com.v2ray.ang.AppConfig.VPN
import com.v2ray.ang.R
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.handler.SettingsManager
import com.v2ray.ang.handler.SubscriptionUpdater
import com.v2ray.ang.helper.MmkvPreferenceDataStore
import com.v2ray.ang.util.Utils

private fun PreferenceFragmentCompat.styleSumraXPreferenceList() {
    val pad = resources.getDimensionPixelSize(R.dimen.padding_spacing_dp16)
    val padBottom = resources.getDimensionPixelSize(R.dimen.padding_spacing_dp16)
    (listView as? RecyclerView)?.apply {
        overScrollMode = View.OVER_SCROLL_NEVER
        clipToPadding = false
        itemAnimator = null
        setPadding(pad, pad / 2, pad, padBottom)
        setBackgroundColor(android.graphics.Color.TRANSPARENT)
    }
}

class SettingsActivity : BaseActivity() {

    enum class SettingsFilter {
        ALL,
        TUNNEL,
        CORE_DNS,
        ADVANCED,
        OTHER,
        PING,
        LOGS,
        INBOUND,
    }

    companion object {
        const val ARG_FILTER = "arg_settings_filter"

        fun settingsFragment(filter: SettingsFilter = SettingsFilter.ALL): SettingsFragment {
            return SettingsFragment().apply {
                arguments = bundleOf(ARG_FILTER to filter.name)
            }
        }
    }

    fun setScreenTitle(title: String) {
        supportActionBar?.title = title
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(R.layout.activity_settings, showHomeAsUp = true, title = getString(R.string.title_settings))

        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                if (supportFragmentManager.backStackEntryCount > 0) {
                    supportFragmentManager.popBackStack()
                } else {
                    isEnabled = false
                    onBackPressedDispatcher.onBackPressed()
                    isEnabled = true
                }
            }
        })
    }

    class InterfaceSettingsFragment : PreferenceFragmentCompat() {
        override fun onCreatePreferences(bundle: Bundle?, s: String?) {
            preferenceManager.preferenceDataStore = MmkvPreferenceDataStore()
            addPreferencesFromResource(R.xml.pref_interface_settings)
            initSummaries()
        }

        override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
            super.onViewCreated(view, savedInstanceState)
            styleSumraXPreferenceList()
        }

        private fun initSummaries() {
            findPreference<ListPreference>(AppConfig.PREF_FONT_SIZE)?.let { pref ->
                pref.summary = pref.entry ?: ""
                pref.setOnPreferenceChangeListener { p, newValue ->
                    val lp = p as ListPreference
                    val idx = lp.findIndexOfValue(newValue as? String)
                    lp.summary = (if (idx >= 0) lp.entries[idx] else newValue) as CharSequence?
                    activity?.recreate()
                    true
                }
            }
            findPreference<ListPreference>(AppConfig.PREF_LANGUAGE)?.let { pref ->
                pref.summary = pref.entry ?: ""
                pref.setOnPreferenceChangeListener { p, newValue ->
                    val lp = p as ListPreference
                    val idx = lp.findIndexOfValue(newValue as? String)
                    lp.summary = (if (idx >= 0) lp.entries[idx] else newValue) as CharSequence?
                    activity?.recreate()
                    true
                }
            }
            findPreference<ListPreference>(AppConfig.PREF_UI_MODE_NIGHT)?.let { pref ->
                pref.summary = pref.entry ?: ""
                pref.setOnPreferenceChangeListener { p, newValue ->
                    val lp = p as ListPreference
                    val idx = lp.findIndexOfValue(newValue as? String)
                    lp.summary = (if (idx >= 0) lp.entries[idx] else newValue) as CharSequence?
                    SettingsManager.setNightMode()
                    true
                }
            }
            findPreference<ListPreference>(AppConfig.PREF_CONNECTION_MODE)?.let { pref ->
                pref.summary = pref.entry ?: ""
                pref.setOnPreferenceChangeListener { p, newValue ->
                    val lp = p as ListPreference
                    val idx = lp.findIndexOfValue(newValue as? String)
                    lp.summary = (if (idx >= 0) lp.entries[idx] else newValue) as CharSequence?
                    true
                }
            }
        }
    }

    class SettingsFragment : PreferenceFragmentCompat() {

        private val localDns by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_LOCAL_DNS_ENABLED) }
        private val fakeDns by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_FAKE_DNS_ENABLED) }
        private val appendHttpProxy by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_APPEND_HTTP_PROXY) }

        //        private val localDnsPort by lazy { findPreference<EditTextPreference>(AppConfig.PREF_LOCAL_DNS_PORT) }
        private val vpnDns by lazy { findPreference<EditTextPreference>(AppConfig.PREF_VPN_DNS) }
        private val vpnBypassLan by lazy { findPreference<ListPreference>(AppConfig.PREF_VPN_BYPASS_LAN) }
        private val vpnInterfaceAddress by lazy { findPreference<ListPreference>(AppConfig.PREF_VPN_INTERFACE_ADDRESS_CONFIG_INDEX) }
        private val vpnMtu by lazy { findPreference<EditTextPreference>(AppConfig.PREF_VPN_MTU) }

        private val mux by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_MUX_ENABLED) }
        private val muxConcurrency by lazy { findPreference<EditTextPreference>(AppConfig.PREF_MUX_CONCURRENCY) }
        private val muxXudpConcurrency by lazy { findPreference<EditTextPreference>(AppConfig.PREF_MUX_XUDP_CONCURRENCY) }
        private val muxXudpQuic by lazy { findPreference<ListPreference>(AppConfig.PREF_MUX_XUDP_QUIC) }

        private val fragment by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_FRAGMENT_ENABLED) }
        private val fragmentPackets by lazy { findPreference<ListPreference>(AppConfig.PREF_FRAGMENT_PACKETS) }
        private val fragmentLength by lazy { findPreference<EditTextPreference>(AppConfig.PREF_FRAGMENT_LENGTH) }
        private val fragmentInterval by lazy { findPreference<EditTextPreference>(AppConfig.PREF_FRAGMENT_INTERVAL) }
        private val noiseEnabled by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_NOISE_ENABLED) }
        private val noiseRand by lazy { findPreference<EditTextPreference>(AppConfig.PREF_NOISE_RAND) }
        private val noiseDelay by lazy { findPreference<EditTextPreference>(AppConfig.PREF_NOISE_DELAY) }

        private val mode by lazy { findPreference<ListPreference>(AppConfig.PREF_MODE) }

        private val hevTunLogLevel by lazy { findPreference<ListPreference>(AppConfig.PREF_HEV_TUNNEL_LOGLEVEL) }
        private val hevTunRwTimeout by lazy { findPreference<EditTextPreference>(AppConfig.PREF_HEV_TUNNEL_RW_TIMEOUT) }
        private val useHevTun by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_USE_HEV_TUNNEL) }

        private val enableLocalProxy by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_ENABLE_LOCAL_PROXY) }
        private val socksPort by lazy { findPreference<EditTextPreference>(AppConfig.PREF_SOCKS_PORT) }
        private val dynamicSocksPort by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_DYNAMIC_SOCKS_PORT) }
        private val socksUsername by lazy { findPreference<EditTextPreference>(AppConfig.PREF_SOCKS_USERNAME) }
        private val socksPassword by lazy { findPreference<EditTextPreference>(AppConfig.PREF_SOCKS_PASSWORD) }
        private val socksEnableUdp by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_SOCKS_ENABLE_UDP) }
        private val proxySharing by lazy { findPreference<CheckBoxPreference>(AppConfig.PREF_PROXY_SHARING) }

        override fun onCreatePreferences(bundle: Bundle?, s: String?) {
            // Use MMKV as the storage backend for all Preferences
            // This prevents inconsistencies between SharedPreferences and MMKV
            preferenceManager.preferenceDataStore = MmkvPreferenceDataStore()

            addPreferencesFromResource(R.xml.pref_settings)
            applyFilter(readSettingsFilter())

            findPreference<Preference>(AppConfig.PREF_SUBSCRIPTIONS_MANAGE)?.setOnPreferenceClickListener {
                startActivity(Intent(requireContext(), SubSettingActivity::class.java))
                true
            }
            findPreference<Preference>("pref_check_update_now")?.setOnPreferenceClickListener {
                startActivity(Intent(requireContext(), CheckUpdateActivity::class.java))
                true
            }
            findPreference<CheckBoxPreference>(AppConfig.PREF_SUBSCRIPTION_AUTO_UPDATE_ENABLE)
                ?.setOnPreferenceChangeListener { _, newValue ->
                    MmkvManager.encodeSettings(AppConfig.PREF_SUBSCRIPTION_AUTO_UPDATE_ENABLE, newValue as Boolean)
                    SubscriptionUpdater.sync(forceReschedule = true)
                    true
                }

            initPreferenceSummaries()

            localDns?.setOnPreferenceChangeListener { _, any ->
                updateLocalDns(any as Boolean)
                true
            }

            mux?.setOnPreferenceChangeListener { _, newValue ->
                updateMux(newValue as Boolean)
                true
            }
            muxConcurrency?.setOnPreferenceChangeListener { _, newValue ->
                updateMuxConcurrency(newValue as String)
                true
            }
            muxXudpConcurrency?.setOnPreferenceChangeListener { _, newValue ->
                updateMuxXudpConcurrency(newValue as String)
                true
            }

            fragment?.setOnPreferenceChangeListener { _, newValue ->
                updateFragment(newValue as Boolean)
                true
            }
            noiseEnabled?.setOnPreferenceChangeListener { _, newValue ->
                updateNoise(newValue as Boolean)
                true
            }

            mode?.setOnPreferenceChangeListener { pref, newValue ->
                val valueStr = newValue.toString()
                (pref as? ListPreference)?.let { lp ->
                    val idx = lp.findIndexOfValue(valueStr)
                    lp.summary = if (idx >= 0) lp.entries[idx] else valueStr
                }
                updateMode(valueStr)
                true
            }
            mode?.dialogLayoutResource = R.layout.preference_with_help_link

            useHevTun?.setOnPreferenceChangeListener { _, newValue ->
                updateHevTunSettings(newValue as Boolean)
                true
            }

            enableLocalProxy?.setOnPreferenceChangeListener { _, newValue ->
                updateEnableLocalProxy(newValue as Boolean)
                true
            }

            dynamicSocksPort?.setOnPreferenceChangeListener { _, newValue ->
                updateDynamicSocksPort(newValue as Boolean)
                true
            }
        }

        override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
            super.onViewCreated(view, savedInstanceState)
            styleSumraXPreferenceList()
        }

        private fun readSettingsFilter(): SettingsFilter {
            val raw = arguments?.getString(ARG_FILTER) ?: return SettingsFilter.ALL
            return runCatching { SettingsFilter.valueOf(raw) }.getOrDefault(SettingsFilter.ALL)
        }

        private fun applyFilter(filter: SettingsFilter) {
            if (filter == SettingsFilter.ALL) return
            val screen = preferenceScreen

            findPreference<Preference>("pref_interface_settings_screen")?.let { screen.removePreference(it) }

            when (filter) {
                SettingsFilter.PING -> {
                    val categories = mutableListOf<PreferenceCategory>()
                    for (i in screen.preferenceCount - 1 downTo 0) {
                        val pref = screen.getPreference(i)
                        if (pref is PreferenceCategory &&
                            pref.title == getString(R.string.title_subscriptions_settings)
                        ) {
                            categories += pref
                        } else {
                            screen.removePreference(pref)
                        }
                    }
                    categories.forEach { category ->
                        category.findPreference<Preference>(AppConfig.PREF_SUBSCRIPTIONS_MANAGE)
                            ?.let { category.removePreference(it) }
                    }
                }

                SettingsFilter.LOGS, SettingsFilter.INBOUND -> {
                    val keepKeys = when (filter) {
                        SettingsFilter.LOGS -> setOf(
                            AppConfig.PREF_LOGLEVEL,
                            AppConfig.PREF_HEV_TUNNEL_LOGLEVEL,
                        )
                        SettingsFilter.INBOUND -> setOf(
                            AppConfig.PREF_ENABLE_LOCAL_PROXY,
                            AppConfig.PREF_SOCKS_PORT,
                            AppConfig.PREF_DYNAMIC_SOCKS_PORT,
                            AppConfig.PREF_SOCKS_USERNAME,
                            AppConfig.PREF_SOCKS_PASSWORD,
                            AppConfig.PREF_SOCKS_ENABLE_UDP,
                            AppConfig.PREF_PROXY_SHARING,
                        )
                        else -> emptySet()
                    }
                    removePreferencesExcept(screen, keepKeys)
                }

                else -> {
                    val keepCategories = when (filter) {
                        SettingsFilter.TUNNEL -> setOf(getString(R.string.title_vpn_settings))
                        SettingsFilter.CORE_DNS -> setOf(getString(R.string.title_core_settings))
                        SettingsFilter.ADVANCED -> setOf(
                            getString(R.string.title_advanced),
                            getString(R.string.title_mux_settings),
                            getString(R.string.title_fragment_settings),
                            getString(R.string.title_core_settings),
                        )
                        SettingsFilter.OTHER -> setOf(getString(R.string.title_subscriptions_settings))
                        SettingsFilter.ALL, SettingsFilter.PING, SettingsFilter.LOGS, SettingsFilter.INBOUND -> emptySet()
                    }
                    for (i in screen.preferenceCount - 1 downTo 0) {
                        val pref = screen.getPreference(i)
                        if (pref is PreferenceCategory) {
                            if (pref.title !in keepCategories) {
                                screen.removePreference(pref)
                            } else if (filter == SettingsFilter.OTHER &&
                                pref.title == getString(R.string.title_subscriptions_settings)
                            ) {
                                pref.findPreference<Preference>(AppConfig.PREF_SUBSCRIPTIONS_MANAGE)
                                    ?.let { pref.removePreference(it) }
                                pref.findPreference<Preference>(AppConfig.PREF_PING_TYPE)
                                    ?.let { pref.removePreference(it) }
                                pref.findPreference<Preference>(AppConfig.PREF_PING_ON_LAUNCH)
                                    ?.let { pref.removePreference(it) }
                            }
                        } else {
                            screen.removePreference(pref)
                        }
                    }
                }
            }
        }

        private fun removePreferencesExcept(
            screen: androidx.preference.PreferenceScreen,
            keepKeys: Set<String>
        ) {
            for (i in screen.preferenceCount - 1 downTo 0) {
                val pref = screen.getPreference(i)
                if (pref is PreferenceCategory) {
                    for (j in pref.preferenceCount - 1 downTo 0) {
                        val child = pref.getPreference(j)
                        if (child.key !in keepKeys) {
                            pref.removePreference(child)
                        }
                    }
                    if (pref.preferenceCount == 0) {
                        screen.removePreference(pref)
                    }
                } else if (pref.key !in keepKeys) {
                    screen.removePreference(pref)
                }
            }
        }

        private fun initPreferenceSummaries() {
            fun updateSummary(pref: androidx.preference.Preference) {
                when (pref) {
                    is EditTextPreference -> {
                        if (pref.key == AppConfig.PREF_SOCKS_PASSWORD) {
                            pref.summary = if (pref.text.isNullOrEmpty()) "" else "******"
                        } else {
                            pref.summary = pref.text.orEmpty()
                        }
                        pref.setOnPreferenceChangeListener { p, newValue ->
                            if (p.key == AppConfig.PREF_SOCKS_PASSWORD) {
                                p.summary = if ((newValue as? String).isNullOrEmpty()) "" else "******"
                            } else {
                                p.summary = (newValue as? String).orEmpty()
                            }
                            true
                        }
                    }

                    is ListPreference -> {
                        pref.summary = pref.entry ?: ""
                        pref.setOnPreferenceChangeListener { p, newValue ->
                            val lp = p as ListPreference
                            val idx = lp.findIndexOfValue(newValue as? String)
                            lp.summary = (if (idx >= 0) lp.entries[idx] else newValue) as CharSequence?
                            true
                        }
                    }

                    is CheckBoxPreference, is androidx.preference.SwitchPreferenceCompat -> {
                    }
                }
            }

            fun traverse(group: androidx.preference.PreferenceGroup) {
                for (i in 0 until group.preferenceCount) {
                    when (val p = group.getPreference(i)) {
                        is androidx.preference.PreferenceGroup -> traverse(p)
                        else -> updateSummary(p)
                    }
                }
            }

            preferenceScreen?.let { traverse(it) }
        }

        override fun onStart() {
            super.onStart()
            updateHevTunSettings(MmkvManager.decodeSettingsBool(AppConfig.PREF_USE_HEV_TUNNEL, false))

            // Initialize mode-dependent UI states
            updateMode(MmkvManager.decodeSettingsString(AppConfig.PREF_MODE, VPN))

            // Initialize local proxy state
            updateEnableLocalProxy(MmkvManager.decodeSettingsBool(AppConfig.PREF_ENABLE_LOCAL_PROXY, true))

            // Initialize mux-dependent UI states
            updateMux(MmkvManager.decodeSettingsBool(AppConfig.PREF_MUX_ENABLED, false))

            // Initialize fragment-dependent UI states
            updateFragment(MmkvManager.decodeSettingsBool(AppConfig.PREF_FRAGMENT_ENABLED, false))

            updateDynamicSocksPort(MmkvManager.decodeSettingsBool(AppConfig.PREF_DYNAMIC_SOCKS_PORT, false))
        }

        private fun updateMode(value: String?) {
            val vpn = value == VPN
            localDns?.isEnabled = vpn
            fakeDns?.isEnabled = vpn
            appendHttpProxy?.isEnabled = vpn
//            localDnsPort?.isEnabled = vpn
            vpnDns?.isEnabled = vpn
            vpnBypassLan?.isEnabled = vpn
            vpnInterfaceAddress?.isEnabled = vpn
            vpnMtu?.isEnabled = vpn
            useHevTun?.isEnabled = vpn
            updateHevTunSettings(false)
            if (vpn) {
                updateLocalDns(
                    MmkvManager.decodeSettingsBool(
                        AppConfig.PREF_LOCAL_DNS_ENABLED,
                        false
                    )
                )
                updateHevTunSettings(
                    MmkvManager.decodeSettingsBool(
                        AppConfig.PREF_USE_HEV_TUNNEL,
                        false
                    )
                )
            }
        }

        private fun updateLocalDns(enabled: Boolean) {
            fakeDns?.isEnabled = enabled
//            localDnsPort?.isEnabled = enabled
            vpnDns?.isEnabled = !enabled
        }

        private fun updateMux(enabled: Boolean) {
            muxConcurrency?.isEnabled = enabled
            muxXudpConcurrency?.isEnabled = enabled
            muxXudpQuic?.isEnabled = enabled
            if (enabled) {
                updateMuxConcurrency(MmkvManager.decodeSettingsString(AppConfig.PREF_MUX_CONCURRENCY, "8"))
                updateMuxXudpConcurrency(MmkvManager.decodeSettingsString(AppConfig.PREF_MUX_XUDP_CONCURRENCY, "8"))
            }
        }

        private fun updateMuxConcurrency(value: String?) {
            val concurrency = value?.toIntOrNull() ?: 8
            muxConcurrency?.summary = concurrency.toString()
        }


        private fun updateMuxXudpConcurrency(value: String?) {
            if (value == null) {
                muxXudpQuic?.isEnabled = true
            } else {
                val concurrency = value.toIntOrNull() ?: 8
                muxXudpConcurrency?.summary = concurrency.toString()
                muxXudpQuic?.isEnabled = concurrency >= 0
            }
        }

        private fun updateFragment(enabled: Boolean) {
            fragmentPackets?.isEnabled = enabled
            fragmentLength?.isEnabled = enabled
            fragmentInterval?.isEnabled = enabled
            noiseEnabled?.isEnabled = enabled
            if (!enabled && noiseEnabled?.isChecked == true) {
                noiseEnabled?.isChecked = false
                MmkvManager.encodeSettings(AppConfig.PREF_NOISE_ENABLED, false)
            }
            updateNoise(enabled && (noiseEnabled?.isChecked == true))
        }

        private fun updateNoise(enabled: Boolean) {
            noiseRand?.isEnabled = enabled
            noiseDelay?.isEnabled = enabled
        }

        private fun updateDynamicSocksPort(enabled: Boolean) {
            socksPort?.isEnabled = (enableLocalProxy?.isChecked == true) && !enabled
        }

        private fun updateEnableLocalProxy(enabled: Boolean) {
            val dynamic = MmkvManager.decodeSettingsBool(AppConfig.PREF_DYNAMIC_SOCKS_PORT, false)
            socksPort?.isEnabled = enabled && !dynamic
            dynamicSocksPort?.isEnabled = enabled
            socksUsername?.isEnabled = enabled
            socksPassword?.isEnabled = enabled
            socksEnableUdp?.isEnabled = enabled
            proxySharing?.isEnabled = enabled

            if (!enabled) {
                if (appendHttpProxy?.isChecked == true) {
                    appendHttpProxy?.isChecked = false
                    MmkvManager.encodeSettings(AppConfig.PREF_APPEND_HTTP_PROXY, false)
                }
                appendHttpProxy?.isEnabled = false
            } else {
                val vpn = MmkvManager.decodeSettingsString(AppConfig.PREF_MODE) == VPN
                appendHttpProxy?.isEnabled = vpn
            }
        }

        private fun updateHevTunSettings(enabled: Boolean) {
            hevTunLogLevel?.isEnabled = enabled
            hevTunRwTimeout?.isEnabled = enabled

            if (enabled) {
                if (enableLocalProxy?.isChecked == false) {
                    enableLocalProxy?.isChecked = true
                    MmkvManager.encodeSettings(AppConfig.PREF_ENABLE_LOCAL_PROXY, true)
                }
                enableLocalProxy?.isEnabled = false
            } else {
                enableLocalProxy?.isEnabled = true
            }
            updateEnableLocalProxy(enableLocalProxy?.isChecked == true)
        }
    }

    fun onModeHelpClicked(view: View) {
        Utils.openUri(this, AppConfig.APP_WIKI_MODE)
    }
}
