package com.v2ray.ang.ui

import android.os.Bundle
import android.view.View
import androidx.preference.PreferenceFragmentCompat
import com.v2ray.ang.R
import com.v2ray.ang.helper.MmkvPreferenceDataStore

class HwidSettingsActivity : SumraxSingleSettingsActivity() {
    override fun screenTitleRes() = R.string.title_pref_send_hwid_header
    override fun createSettingsFragment() = HwidSettingsFragment()
}

class HwidSettingsFragment : PreferenceFragmentCompat() {
    override fun onCreatePreferences(bundle: Bundle?, rootKey: String?) {
        preferenceManager.preferenceDataStore = MmkvPreferenceDataStore()
        addPreferencesFromResource(R.xml.pref_hwid)
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        styleSumraXPreferenceList()
    }

    private fun styleSumraXPreferenceList() {
        val pad = resources.getDimensionPixelSize(R.dimen.padding_spacing_dp16)
        (listView as? androidx.recyclerview.widget.RecyclerView)?.apply {
            overScrollMode = View.OVER_SCROLL_NEVER
            clipToPadding = false
            itemAnimator = null
            setPadding(pad, pad / 2, pad, pad)
            setBackgroundColor(android.graphics.Color.TRANSPARENT)
        }
    }
}