package com.v2ray.ang.ui

import android.os.Bundle
import androidx.fragment.app.Fragment
import com.v2ray.ang.R

abstract class HappSingleSettingsActivity : BaseActivity() {

    abstract fun screenTitleRes(): Int
    abstract fun createSettingsFragment(): Fragment

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(
            R.layout.activity_settings_single_fragment,
            showHomeAsUp = true,
            title = getString(screenTitleRes())
        )
        if (savedInstanceState == null) {
            supportFragmentManager.beginTransaction()
                .replace(R.id.fragment_settings_container, createSettingsFragment())
                .commit()
        }
    }
}
