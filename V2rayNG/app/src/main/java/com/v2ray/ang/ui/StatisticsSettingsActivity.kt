package com.v2ray.ang.ui

import android.os.Bundle
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ActivityStatisticsSettingsBinding
import com.v2ray.ang.extension.toTrafficString
import com.v2ray.ang.handler.MonthlyTrafficTracker

class StatisticsSettingsActivity : BaseActivity() {

    private val binding by lazy { ActivityStatisticsSettingsBinding.inflate(layoutInflater) }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(
            binding.root,
            showHomeAsUp = true,
            title = getString(R.string.sumrax_settings_statistics)
        )
        refreshTrafficPanel()
    }

    private fun refreshTrafficPanel() {
        val totals = MonthlyTrafficTracker.loadCurrentTotals()
        binding.trafficPanel.tvProxyTraffic.text = getString(
            R.string.sumrax_statistics_proxy_total,
            totals.proxyUp.toTrafficString(),
            totals.proxyDown.toTrafficString()
        )
        binding.trafficPanel.tvDirectTraffic.text = getString(
            R.string.sumrax_statistics_direct_total,
            totals.directUp.toTrafficString(),
            totals.directDown.toTrafficString()
        )
        binding.trafficPanel.tvProxyTotal.text = getString(
            R.string.sumrax_statistics_proxy_label,
            AppConfig.TAG_PROXY
        )
        binding.trafficPanel.tvDirectTotal.text = getString(
            R.string.sumrax_statistics_direct_label,
            AppConfig.TAG_DIRECT
        )
        binding.trafficPanel.tvMonthlyTraffic.text =
            MonthlyTrafficTracker.formatMonthlyLine(this, totals)
    }
}
