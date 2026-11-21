package com.v2ray.ang.ui

import android.content.Intent
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.core.content.ContextCompat
import android.widget.LinearLayout
import android.widget.TextView
import androidx.fragment.app.Fragment
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ItemSettingsSectionBinding

/**
 * Settings: one scroll, sections UI → Tunnel → Advanced → Other → About
 * (same order as activity_settings.xml).
 */
class SumraxSettingsScrollFragment : Fragment() {

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View = inflater.inflate(R.layout.fragment_sumrax_settings_scroll, container, false)

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        val inflater = LayoutInflater.from(requireContext())
        fun fillSection(containerId: Int, tab: SettingsHubRegistry.Tab) {
            val container = view.findViewById<LinearLayout>(containerId)
            container.removeAllViews()
            val entries = SettingsHubRegistry.entriesForTab(tab)
            entries.forEachIndexed { index, entry ->
                val row = ItemSettingsSectionBinding.inflate(inflater, container, false)
                row.tvSettingsRowTitle.setText(entry.titleRes)
                row.settingsRowRoot.setOnClickListener {
                    startActivity(Intent(requireContext(), entry.activityClass))
                }
                if (entry.titleRes == R.string.title_reset) {
                    row.tvSettingsRowTitle.setTextColor(
                        ContextCompat.getColor(requireContext(), R.color.sumrax_ui_settings_red)
                    )
                }
                container.addView(row.root)
                if (index < entries.lastIndex) {
                    val divider = View(requireContext()).apply {
                        layoutParams = LinearLayout.LayoutParams(
                            LinearLayout.LayoutParams.MATCH_PARENT,
                            resources.getDimensionPixelSize(R.dimen.sumrax_ui_settings_divider_height)
                        )
                        setBackgroundResource(R.drawable.bg_sumrax_settings_divider)
                    }
                    container.addView(divider)
                }
            }
        }
        fillSection(R.id.section_ui, SettingsHubRegistry.Tab.UI)
        fillTunnelSection(view, inflater)
        fillSection(R.id.section_advanced, SettingsHubRegistry.Tab.ADVANCED)
        fillSection(R.id.section_other, SettingsHubRegistry.Tab.OTHER)
        fillSection(R.id.section_about, SettingsHubRegistry.Tab.ABOUT)

        view.findViewById<TextView>(R.id.tv_about_version)?.text =
            "v${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})"
        view.findViewById<View>(R.id.about_block)?.setOnClickListener {
            startActivity(Intent(requireContext(), AboutActivity::class.java))
        }
    }

    override fun onResume() {
        super.onResume()
        (activity as? SettingsActivity)?.setScreenTitle(getString(R.string.title_settings))
    }

    private fun fillTunnelSection(view: View, inflater: LayoutInflater) {
        val container = view.findViewById<LinearLayout>(R.id.section_tunnel)
        container.removeAllViews()
        val entries = SettingsHubRegistry.entriesForTab(SettingsHubRegistry.Tab.TUNNEL)
        fun addForward(entry: SettingsHubRegistry.Entry) {
            val row = ItemSettingsSectionBinding.inflate(inflater, container, false)
            row.tvSettingsRowTitle.setText(entry.titleRes)
            row.settingsRowRoot.setOnClickListener {
                startActivity(Intent(requireContext(), entry.activityClass))
            }
            container.addView(row.root)
            addDivider(container)
        }
        entries.take(2).forEach { addForward(it) }
        SumraxTunnelInlineBinder.appendToggles(container)
        addDivider(container)
        entries.drop(2).forEach { addForward(it) }
    }

    private fun addDivider(container: LinearLayout) {
        val divider = View(requireContext()).apply {
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                resources.getDimensionPixelSize(R.dimen.sumrax_ui_settings_divider_height)
            )
            setBackgroundResource(R.drawable.bg_sumrax_settings_divider)
        }
        container.addView(divider)
    }
}