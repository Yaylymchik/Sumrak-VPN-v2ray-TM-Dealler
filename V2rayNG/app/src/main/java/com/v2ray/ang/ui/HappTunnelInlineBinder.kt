package com.v2ray.ang.ui

import android.view.LayoutInflater
import android.widget.LinearLayout
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ItemHappSettingsToggleBinding
import com.v2ray.ang.handler.MmkvManager

object HappTunnelInlineBinder {

    fun appendToggles(container: LinearLayout) {
        val ctx = container.context
        val inflater = LayoutInflater.from(ctx)

        fun addToggle(key: String, titleRes: Int, dividerAfter: Boolean = true) {
            val row = ItemHappSettingsToggleBinding.inflate(inflater, container, false)
            row.tvToggleTitle.setText(titleRes)
            row.switchToggle.isChecked = MmkvManager.decodeSettingsBool(key, false)
            row.switchToggle.setOnCheckedChangeListener { _, checked ->
                MmkvManager.encodeSettings(key, checked)
            }
            container.addView(row.root)
            if (dividerAfter) addDivider(container)
        }

        addToggle(AppConfig.PREF_FRAGMENT_ENABLED, R.string.title_pref_fragment_enabled)
        addToggle(AppConfig.PREF_MUX_ENABLED, R.string.title_pref_mux_enabled)
        addToggle(AppConfig.PREF_XRAY_TUN_ENABLED, R.string.title_pref_xray_tun_enabled, dividerAfter = false)
    }

    private fun addDivider(container: LinearLayout) {
        val v = android.view.View(container.context).apply {
            layoutParams = LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                container.resources.getDimensionPixelSize(R.dimen.happ_settings_divider_height)
            )
            setBackgroundResource(R.drawable.bg_happ_settings_divider)
        }
        container.addView(v)
    }
}