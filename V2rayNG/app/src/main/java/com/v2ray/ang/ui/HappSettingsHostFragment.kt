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
 * Happ-style settings hub: sectioned list (Interface / Connection / Data / Xray / System).
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
        bind(binding.rowUi, R.string.sumrax_settings_ui, UISettingsActivity::class.java)
        bind(binding.rowLanguage, R.string.sumrax_settings_language, LanguageActivitySettings::class.java)
        bind(binding.rowTun, R.string.sumrax_settings_vpn, VpnSettingsActivity::class.java)
        bind(binding.rowRouting, R.string.sumrax_settings_routing, RoutingSettingActivity::class.java)
        bind(binding.rowExcludedApps, R.string.sumrax_settings_excluded_apps, PerAppProxyActivity::class.java)
        bind(binding.rowExcludedRoutes, R.string.sumrax_settings_excluded_routes, ExcludedRoutesActivity::class.java)
        bind(binding.rowInbound, R.string.sumrax_settings_inbound_auth, InboundAuthActivity::class.java)
        bind(binding.rowConnectionSecurity, R.string.sumrax_settings_dns_network, CoreDnsSettingsActivity::class.java)
        bind(binding.rowPing, R.string.sumrax_settings_ping, PingSettingsActivity::class.java)
        bind(binding.rowSubscriptions, R.string.sumrax_settings_subscriptions, SubscriptionSettingsActivity::class.java)
        bind(binding.rowGeoFiles, R.string.sumrax_settings_geo_files, UserAssetActivity::class.java)
        bind(binding.rowAdvanced, R.string.sumrax_settings_advanced, AdvancedSettingsActivity::class.java)
        bind(binding.rowOther, R.string.sumrax_settings_other, OtherSettingsActivity::class.java)
        bind(binding.rowLogs, R.string.sumrax_settings_logs, LogsSettingsActivity::class.java)
        bind(binding.rowLogsView, R.string.sumrax_settings_logs_view, LogsViewSettingsActivity::class.java)
        bind(binding.rowStatistics, R.string.sumrax_settings_statistics, StatisticsSettingsActivity::class.java)
        bind(binding.rowDiagnostics, R.string.sumrax_settings_diagnostics, DiagnosticsActivity::class.java)
        bind(binding.rowBackup, R.string.sumrax_settings_backup, BackupActivity::class.java)
        bind(binding.rowReset, R.string.sumrax_settings_reset, ResetSettingsActivity::class.java)
        bind(binding.rowFaq, R.string.sumrax_settings_faq, FaqActivity::class.java)
        bind(binding.rowReport, R.string.sumrax_settings_report, ReportActivity::class.java)
        // Check update is reachable from Other / Subscriptions prefs
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
