package com.v2ray.ang.ui

import android.os.Bundle
import android.text.InputType
import android.text.TextUtils
import android.view.Menu
import android.view.MenuItem
import android.widget.ArrayAdapter
import android.widget.AutoCompleteTextView
import android.widget.EditText
import android.widget.ImageButton
import androidx.appcompat.widget.SwitchCompat
import androidx.appcompat.app.AlertDialog
import androidx.lifecycle.lifecycleScope
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ActivitySubEditBinding
import com.v2ray.ang.dto.entities.SubscriptionItem
import com.v2ray.ang.enums.EConfigType
import com.v2ray.ang.extension.toast
import com.v2ray.ang.extension.toastSuccess
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.handler.SettingsChangeManager
import com.v2ray.ang.handler.SettingsManager
import com.v2ray.ang.handler.SubscriptionUpdater
import com.v2ray.ang.util.SubscriptionLock
import com.v2ray.ang.util.Utils
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

class SubEditActivity : BaseActivity() {
    private val binding by lazy { ActivitySubEditBinding.inflate(layoutInflater) }

    private var del_config: MenuItem? = null
    private var save_config: MenuItem? = null

    private val editSubId by lazy { intent.getStringExtra("subId").orEmpty() }
    private var isLockedGroup = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(binding.root, showHomeAsUp = true, title = getString(R.string.title_sub_setting))

        setupProfileRemarkInputs()
        setupEnumDropdowns()
        SettingsChangeManager.makeSetupGroupTab()
        val subItem = MmkvManager.decodeSubscription(editSubId)
        isLockedGroup = SubscriptionLock.isLocked(subItem)
        if (subItem != null) {
            bindingServer(subItem)
        } else {
            clearServer()
        }
        if (isLockedGroup) {
            applyLockedState()
        }
    }

    private fun applyLockedState() {
        lockField(binding.etRemarks)
        lockField(binding.etUrl)
        lockField(binding.etUserAgent)
        lockField(binding.etFilter)
        lockField(binding.etUpdateInterval)
        lockField(binding.etPreProfile)
        lockField(binding.etNextProfile)
        lockField(binding.spPingType)
        lockField(binding.spAutoConnectType)
        lockSwitch(binding.chkEnable)
        lockSwitch(binding.autoUpdateCheck)
        lockSwitch(binding.allowInsecureUrl)
        binding.btnPreProfileDropdown.isEnabled = false
        binding.btnPreProfileDropdown.isClickable = false
        binding.btnNextProfileDropdown.isEnabled = false
        binding.btnNextProfileDropdown.isClickable = false
        binding.btnPingTypeDropdown.isEnabled = false
        binding.btnPingTypeDropdown.isClickable = false
        binding.btnAutoConnectTypeDropdown.isEnabled = false
        binding.btnAutoConnectTypeDropdown.isClickable = false
    }

    private fun lockField(field: EditText) {
        field.isEnabled = false
        field.isFocusable = false
        field.isFocusableInTouchMode = false
        field.isClickable = false
        field.isLongClickable = false
        field.keyListener = null
        field.inputType = InputType.TYPE_NULL
    }

    private fun lockSwitch(switch: SwitchCompat) {
        switch.isEnabled = false
        switch.isClickable = false
        switch.isFocusable = false
    }

    private fun bindingServer(subItem: SubscriptionItem): Boolean {
        binding.etRemarks.text = Utils.getEditable(subItem.remarks)
        binding.etUrl.text = Utils.getEditable(subItem.url)
        binding.etUserAgent.text = Utils.getEditable(subItem.userAgent)
        binding.etUserAgent.hint = SettingsManager.getDefaultUserAgent()
        binding.etFilter.text = Utils.getEditable(subItem.filter)
        binding.chkEnable.isChecked = subItem.enabled
        binding.autoUpdateCheck.isChecked = subItem.autoUpdate
        binding.etUpdateInterval.text = Utils.getEditable(subItem.updateInterval.toString())
        binding.allowInsecureUrl.isChecked = subItem.allowInsecureUrl
        binding.etPreProfile.text = Utils.getEditable(subItem.prevProfile)
        binding.etNextProfile.text = Utils.getEditable(subItem.nextProfile)
        setDropdownValue(binding.spPingType, pingTypeEntries, pingTypeValues, subItem.pingType.orEmpty())
        setDropdownValue(
            binding.spAutoConnectType,
            autoConnectEntries,
            autoConnectValues,
            subItem.autoConnectType.orEmpty()
        )
        return true
    }

    private fun clearServer(): Boolean {
        binding.etRemarks.text = null
        binding.etUrl.text = null
        binding.etFilter.text = null
        binding.chkEnable.isChecked = true
        binding.etUpdateInterval.text = null
        binding.etPreProfile.text = null
        binding.etNextProfile.text = null
        setDropdownValue(binding.spPingType, pingTypeEntries, pingTypeValues, "")
        setDropdownValue(binding.spAutoConnectType, autoConnectEntries, autoConnectValues, "")
        return true
    }

    private val pingTypeEntries by lazy { resources.getStringArray(R.array.ping_type_entries_with_inherit) }
    private val pingTypeValues by lazy { resources.getStringArray(R.array.ping_type_value_with_inherit) }
    private val autoConnectEntries by lazy { resources.getStringArray(R.array.auto_connect_type_entries_with_inherit) }
    private val autoConnectValues by lazy { resources.getStringArray(R.array.auto_connect_type_value_with_inherit) }

    private fun setupEnumDropdowns() {
        setupValueDropdown(
            binding.spPingType,
            binding.btnPingTypeDropdown,
            pingTypeEntries
        )
        setupValueDropdown(
            binding.spAutoConnectType,
            binding.btnAutoConnectTypeDropdown,
            autoConnectEntries
        )
    }

    private fun setupValueDropdown(
        input: AutoCompleteTextView,
        dropdownButton: ImageButton,
        entries: Array<String>
    ) {
        val adapter = ArrayAdapter(this, android.R.layout.simple_dropdown_item_1line, entries)
        input.setAdapter(adapter)
        input.threshold = 0
        dropdownButton.setOnClickListener {
            if (isLockedGroup) return@setOnClickListener
            input.requestFocus()
            input.showDropDown()
        }
        input.setOnClickListener {
            if (isLockedGroup) return@setOnClickListener
            input.showDropDown()
        }
    }

    private fun setDropdownValue(
        input: AutoCompleteTextView,
        entries: Array<String>,
        values: Array<String>,
        value: String
    ) {
        val index = values.indexOf(value).takeIf { it >= 0 } ?: 0
        input.setText(entries.getOrElse(index) { entries.firstOrNull().orEmpty() }, false)
    }

    private fun selectedDropdownValue(
        input: AutoCompleteTextView,
        entries: Array<String>,
        values: Array<String>
    ): String? {
        val text = input.text?.toString().orEmpty()
        val index = entries.indexOf(text).takeIf { it >= 0 } ?: 0
        return values.getOrNull(index)?.takeIf { it.isNotEmpty() }
    }

    private fun setupProfileRemarkInputs() {
        val suggestions = SettingsManager.getProfileRemarks(
            excludeConfigTypes = setOf(
                EConfigType.CUSTOM,
                EConfigType.POLICYGROUP,
                EConfigType.PROXYCHAIN,
            )
        )

        setupProfileRemarkInput(binding.etPreProfile, binding.btnPreProfileDropdown, suggestions)
        setupProfileRemarkInput(binding.etNextProfile, binding.btnNextProfileDropdown, suggestions)
    }

    private fun setupProfileRemarkInput(
        input: AutoCompleteTextView,
        dropdownButton: ImageButton,
        suggestions: List<String>
    ) {
        val adapter = ArrayAdapter(this, android.R.layout.simple_dropdown_item_1line, suggestions)
        input.setAdapter(adapter)
        input.threshold = 0

        dropdownButton.setOnClickListener {
            if (isLockedGroup) return@setOnClickListener
            input.requestFocus()
            input.showDropDown()
        }
        input.setOnClickListener {
            if (isLockedGroup) return@setOnClickListener
            input.showDropDown()
        }
    }

    private fun saveServer(): Boolean {
        if (isLockedGroup) {
            toast(R.string.sumrax_sub_locked)
            return false
        }

        val subItem = MmkvManager.decodeSubscription(editSubId) ?: SubscriptionItem()

        subItem.remarks = binding.etRemarks.text.toString()
        subItem.url = binding.etUrl.text.toString()
        subItem.userAgent = binding.etUserAgent.text.toString()
        subItem.filter = binding.etFilter.text.toString()
        subItem.enabled = binding.chkEnable.isChecked
        subItem.autoUpdate = binding.autoUpdateCheck.isChecked

        val intervalInput = binding.etUpdateInterval.text.toString().trim()
        val intervalMinutes = intervalInput.toLongOrNull()
        if (subItem.autoUpdate) {
            if (intervalMinutes == null) {
                subItem.updateInterval = SubscriptionItem().updateInterval
            } else if (intervalMinutes < AppConfig.SUBSCRIPTION_MIN_INTERVAL_MINUTES) {
                toast(R.string.toast_invalid_update_interval)
                return false
            } else {
                subItem.updateInterval = intervalMinutes
            }
        } else {
            if (intervalMinutes != null && intervalMinutes >= AppConfig.SUBSCRIPTION_MIN_INTERVAL_MINUTES) {
                subItem.updateInterval = intervalMinutes
            }
        }

        subItem.prevProfile = binding.etPreProfile.text.toString()
        subItem.nextProfile = binding.etNextProfile.text.toString()
        subItem.allowInsecureUrl = binding.allowInsecureUrl.isChecked
        subItem.pingType = selectedDropdownValue(binding.spPingType, pingTypeEntries, pingTypeValues)
        subItem.autoConnectType = selectedDropdownValue(
            binding.spAutoConnectType,
            autoConnectEntries,
            autoConnectValues
        )

        if (TextUtils.isEmpty(subItem.remarks)) {
            toast(R.string.sub_setting_remarks)
            return false
        }
        if (subItem.url.isNotEmpty()) {
            if (!Utils.isValidUrl(subItem.url)) {
                toast(R.string.toast_invalid_url)
                return false
            }

            if (!Utils.isValidSubUrl(subItem.url)) {
                toast(R.string.toast_insecure_url_protocol)
                if (!subItem.allowInsecureUrl) {
                    return false
                }
            }
        }

        MmkvManager.encodeSubscription(editSubId, subItem)
        SubscriptionUpdater.syncOne(subId = editSubId)
        toastSuccess(R.string.toast_success)
        finish()
        return true
    }

    private fun deleteServer(): Boolean {
        if (isLockedGroup) {
            toast(R.string.sumrax_sub_locked)
            return false
        }

        if (editSubId.isNotEmpty()) {
            if (MmkvManager.decodeSettingsBool(AppConfig.PREF_CONFIRM_REMOVE)) {
                AlertDialog.Builder(this).setMessage(R.string.del_config_comfirm)
                    .setPositiveButton(android.R.string.ok) { _, _ ->
                        lifecycleScope.launch(Dispatchers.IO) {
                            SettingsManager.removeSubscriptionWithDefault(editSubId)
                            launch(Dispatchers.Main) {
                                finish()
                            }
                        }
                    }
                    .setNegativeButton(android.R.string.cancel) { _, _ -> }
                    .show()
            } else {
                lifecycleScope.launch(Dispatchers.IO) {
                    SettingsManager.removeSubscriptionWithDefault(editSubId)
                    launch(Dispatchers.Main) {
                        finish()
                    }
                }
            }
        }
        return true
    }

    override fun onCreateOptionsMenu(menu: Menu): Boolean {
        if (isLockedGroup) {
            return super.onCreateOptionsMenu(menu)
        }
        menuInflater.inflate(R.menu.action_server, menu)
        del_config = menu.findItem(R.id.del_config)
        save_config = menu.findItem(R.id.save_config)
        return super.onCreateOptionsMenu(menu)
    }

    override fun onOptionsItemSelected(item: MenuItem) = when (item.itemId) {
        R.id.del_config -> {
            deleteServer()
            true
        }

        R.id.save_config -> {
            saveServer()
            true
        }

        else -> super.onOptionsItemSelected(item)
    }
}
