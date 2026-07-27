package com.v2ray.ang.ui

import android.os.Bundle
import androidx.core.view.isVisible
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ActivityOnboardingBinding
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.handler.SettingsManager

/**
 * First-launch guide: subscription → Smart Connect → ready.
 */
class OnboardingActivity : BaseActivity() {
    private val binding by lazy { ActivityOnboardingBinding.inflate(layoutInflater) }
    private var step = 0

    private data class Page(
        val titleRes: Int,
        val bodyRes: Int,
        val primaryRes: Int
    )

    private val pages by lazy {
        listOf(
            Page(
                R.string.sumrax_onboarding_title_1,
                R.string.sumrax_onboarding_body_1,
                R.string.sumrax_onboarding_next
            ),
            Page(
                R.string.sumrax_onboarding_title_2,
                R.string.sumrax_onboarding_body_2,
                R.string.sumrax_onboarding_next
            ),
            Page(
                R.string.sumrax_onboarding_title_3,
                R.string.sumrax_onboarding_body_3,
                R.string.sumrax_onboarding_done
            )
        )
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(binding.root)
        render()
        binding.btnPrimary.setOnClickListener { onPrimary() }
        binding.btnSkip.setOnClickListener { finishOnboarding() }
    }

    private fun render() {
        val page = pages[step]
        binding.tvStep.text = getString(R.string.sumrax_onboarding_step, step + 1, pages.size)
        binding.tvTitle.setText(page.titleRes)
        binding.tvBody.setText(page.bodyRes)
        binding.btnPrimary.setText(page.primaryRes)
        binding.btnSkip.isVisible = step < pages.lastIndex
    }

    private fun onPrimary() {
        if (step < pages.lastIndex) {
            step++
            if (step == 1) {
                SettingsManager.setConnectionMode(true)
            }
            render()
        } else {
            SettingsManager.setConnectionMode(true)
            finishOnboarding()
        }
    }

    private fun finishOnboarding() {
        MmkvManager.encodeSettings(AppConfig.PREF_ONBOARDING_DONE, true)
        finish()
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() {
        if (step > 0) {
            step--
            render()
        } else {
            finishOnboarding()
        }
    }
}
