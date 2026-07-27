package com.v2ray.ang.ui

import android.content.Intent
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.R
import com.v2ray.ang.databinding.FragmentSettingsHubBinding
import com.v2ray.ang.databinding.ItemSettingsSectionBinding

/**
 * Settings hub ordered to match the reference client sections:
 * UI → Tunnel → Advanced → Other → About.
 */
class HappSettingsHostFragment : Fragment() {

    private var _binding: FragmentSettingsHubBinding? = null
    private val binding get() = _binding!!

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View {
        _binding = FragmentSettingsHubBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        bindRows()
        binding.tvAboutVersion.text = "v${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})"
        binding.aboutBlock.setOnClickListener {
            startActivity(Intent(requireContext(), AboutActivity::class.java))
        }
    }

    override fun onResume() {
        super.onResume()
        (activity as? SettingsActivity)?.setScreenTitle(getString(R.string.title_settings))
    }

    private fun bindRows() {
        // UI
        bind(binding.rowLanguage, R.string.title_language, LanguageActivitySettings::class.java)
        bind(binding.rowUiMode, R.string.title_pref_ui_mode_night, UiModeSettingsActivity::class.java)
        bind(binding.rowUi, R.string.title_ui_settings, UISettingsActivity::class.java)

        // Tunnel
        bind(binding.rowRouting, R.string.routing_settings_title, RoutingSettingActivity::class.java)
        bind(binding.rowExcludedApps, R.string.title_pref_per_app_proxy, PerAppProxyActivity::class.java)
        bind(binding.rowTunnelOptions, R.string.title_tunnel, TunnelOptionsActivity::class.java)
        bind(binding.rowInbound, R.string.inbound_authorization_title, InboundAuthActivity::class.java)

        // Advanced
        bind(binding.rowVpn, R.string.title_vpn_settings, VpnSettingsActivity::class.java)
        bind(binding.rowSubscriptions, R.string.title_subscription, SubscriptionSettingsActivity::class.java)
        bind(binding.rowPing, R.string.title_ping, PingSettingsActivity::class.java)
        bind(binding.rowLanPorts, R.string.title_pref_proxy_sharing_enabled, LanPortsSettingsActivity::class.java)
        bind(binding.rowAutoStart, R.string.title_pref_auto_start_vpn, AutoStartSettingsActivity::class.java)

        // Other
        bind(binding.rowCheckUpdate, R.string.title_pref_update_button_check, CheckUpdateActivity::class.java)
        bind(binding.rowStatistics, R.string.title_statistics, StatisticsSettingsActivity::class.java)
        bind(binding.rowLogs, R.string.title_logs, LogsSettingsActivity::class.java)
        bind(binding.rowReset, R.string.title_reset, ResetSettingsActivity::class.java)

        // About
        bind(binding.rowFaq, R.string.title_faq, FaqActivity::class.java)
        bind(binding.rowUrlSchemes, R.string.title_url_schemes, UrlSchemesActivity::class.java)
        bind(binding.rowAbout, R.string.title_about, AboutActivity::class.java)
    }

    private fun bind(row: ItemSettingsSectionBinding, titleRes: Int, activity: Class<*>) {
        row.tvSettingsRowTitle.setText(titleRes)
        row.settingsRowRoot.setOnClickListener {
            startActivity(Intent(requireContext(), activity))
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
