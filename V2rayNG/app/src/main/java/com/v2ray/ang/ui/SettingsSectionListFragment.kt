package com.v2ray.ang.ui

import android.content.Intent
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.core.os.bundleOf
import androidx.fragment.app.Fragment
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.R
import com.v2ray.ang.databinding.FragmentSettingsSectionListBinding
import com.v2ray.ang.databinding.ItemSettingsSectionBinding
import java.text.Collator

class SettingsSectionListFragment : Fragment() {

    private var _binding: FragmentSettingsSectionListBinding? = null
    private val binding get() = _binding!!

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View {
        _binding = FragmentSettingsSectionListBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        val tabName = arguments?.getString(ARG_TAB).orEmpty()
        val tab = SettingsHubRegistry.Tab.entries.firstOrNull { it.name == tabName }
            ?: SettingsHubRegistry.Tab.ALL
        val collator = Collator.getInstance(resources.configuration.locales[0])
        collator.strength = Collator.PRIMARY
        val titleOf: (Int) -> String = { res -> getString(res) }
        val rows = SettingsHubRegistry.entriesFor(tab, titleOf, collator)
        val container = binding.settingsRowsContainer
        container.removeAllViews()
        val inflater = LayoutInflater.from(requireContext())
        rows.forEach { entry ->
            val row = ItemSettingsSectionBinding.inflate(inflater, container, false)
            row.tvSettingsRowTitle.setText(entry.titleRes)
            row.settingsRowRoot.setOnClickListener {
                startActivity(Intent(requireContext(), entry.activityClass))
            }
            container.addView(row.root)
        }
        if (arguments?.getBoolean(ARG_SHOW_ABOUT_FOOTER) == true) {
            inflater.inflate(R.layout.include_settings_about_footer, container, true)
            val version = container.findViewById<TextView>(R.id.tv_about_version)
            version?.text = "v${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})"
            container.findViewById<View>(R.id.about_block)?.setOnClickListener {
                startActivity(Intent(requireContext(), AboutActivity::class.java))
            }
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }

    companion object {
        private const val ARG_TAB = "arg_settings_tab"
        private const val ARG_SHOW_ABOUT_FOOTER = "arg_show_about_footer"

        fun newInstance(tab: SettingsHubRegistry.Tab, showAboutFooter: Boolean = false): SettingsSectionListFragment {
            return SettingsSectionListFragment().apply {
                arguments = bundleOf(
                    ARG_TAB to tab.name,
                    ARG_SHOW_ABOUT_FOOTER to showAboutFooter
                )
            }
        }
    }
}