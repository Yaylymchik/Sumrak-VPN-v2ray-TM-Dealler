package com.v2ray.ang.ui

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.R
import com.v2ray.ang.core.CoreNativeManager
import com.v2ray.ang.databinding.FragmentAboutSettingsBinding

class AboutSettingsFragment : Fragment() {

    private var _binding: FragmentAboutSettingsBinding? = null
    private val binding get() = _binding!!

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View {
        _binding = FragmentAboutSettingsBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        binding.tvAboutVersion.text = getString(
            R.string.sumrax_about_version,
            BuildConfig.VERSION_NAME,
            CoreNativeManager.getLibVersion()
        )
        binding.aboutBlock.setOnClickListener {
            startActivity(android.content.Intent(requireContext(), AboutActivity::class.java))
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
