package com.v2ray.ang.ui

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import androidx.viewpager2.adapter.FragmentStateAdapter
import com.google.android.material.tabs.TabLayoutMediator
import com.v2ray.ang.R
import com.v2ray.ang.databinding.FragmentSettingsHostTabsBinding

/**
 * Happ-style settings: tabs per section + «All» with every action sorted A–Z.
 */
class HappSettingsHostFragment : Fragment() {

    private var _binding: FragmentSettingsHostTabsBinding? = null
    private val binding get() = _binding!!

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View {
        _binding = FragmentSettingsHostTabsBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        val tabs = SettingsHubRegistry.tabs
        binding.pagerSettings.adapter = object : FragmentStateAdapter(this) {
            override fun getItemCount(): Int = tabs.size

            override fun createFragment(position: Int): Fragment {
                val tab = tabs[position]
                val showAbout = tab == SettingsHubRegistry.Tab.ABOUT
                return SettingsSectionListFragment.newInstance(tab, showAboutFooter = showAbout)
            }
        }
        TabLayoutMediator(binding.tabSettings, binding.pagerSettings) { tabLayoutTab, position ->
            tabLayoutTab.text = getString(SettingsHubRegistry.tabTitleRes(tabs[position]))
        }.attach()
    }

    override fun onResume() {
        super.onResume()
        (activity as? SettingsActivity)?.setScreenTitle(getString(R.string.title_settings))
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}