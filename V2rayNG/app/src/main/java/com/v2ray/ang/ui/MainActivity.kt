package com.v2ray.ang.ui

import android.animation.ObjectAnimator
import android.content.ClipboardManager
import android.content.Intent
import android.net.Uri
import android.net.VpnService
import android.os.Bundle
import android.view.KeyEvent
import android.view.Menu
import android.view.MenuItem
import android.view.animation.DecelerateInterpolator
import android.view.animation.LinearInterpolator
import androidx.activity.OnBackPressedCallback
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.appcompat.app.ActionBarDrawerToggle
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.widget.SearchView
import androidx.core.content.ContextCompat
import androidx.core.view.GravityCompat
import androidx.core.view.isVisible
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import com.google.android.material.tabs.TabLayoutMediator
import com.v2ray.ang.AppConfig
import com.v2ray.ang.AppConfig.PROXY_ONLY
import com.v2ray.ang.AppConfig.VPN
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.R
import com.v2ray.ang.core.CoreServiceManager
import com.v2ray.ang.databinding.ActivityMainBinding
import com.v2ray.ang.databinding.ItemNavSumraxBinding
import com.v2ray.ang.databinding.NavTrafficFooterBinding
import com.v2ray.ang.enums.EConfigType
import com.v2ray.ang.extension.toSpeedString
import com.v2ray.ang.extension.toTrafficString
import com.v2ray.ang.enums.PermissionType
import com.v2ray.ang.extension.toast
import com.v2ray.ang.extension.toastError
import com.v2ray.ang.handler.AngConfigManager
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.handler.MonthlyTrafficTracker
import com.v2ray.ang.handler.SettingsChangeManager
import com.v2ray.ang.handler.SettingsManager
import com.v2ray.ang.handler.ServerListsManager
import com.v2ray.ang.handler.SubscriptionUpdater
import com.v2ray.ang.handler.TrafficStatsHelper
import com.v2ray.ang.util.LogUtil
import com.v2ray.ang.util.ProfileAutoSelector
import com.v2ray.ang.util.ProfileRemarkParser
import com.v2ray.ang.util.ProfileSettingsApplier
import com.v2ray.ang.util.Utils
import com.v2ray.ang.util.encrypt.EncryptedCryptResolver
import com.v2ray.ang.viewmodel.MainViewModel
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity : HelperBaseActivity() {
    private enum class ConnectionUiState {
        DISCONNECTED, CONNECTING, CONNECTED, ERROR
    }

    private val binding by lazy {
        ActivityMainBinding.inflate(layoutInflater)
    }

    val mainViewModel: MainViewModel by viewModels()
    private lateinit var groupPagerAdapter: GroupPagerAdapter
    private var tabMediator: TabLayoutMediator? = null
    private var speedometerJob: Job? = null
    private var navTrafficBinding: NavTrafficFooterBinding? = null
    private var lastQueryTime = 0L
    private var sessionProxyDownBytes = 0L
    private var sessionProxyUpBytes = 0L
    private var sessionDirectDownBytes = 0L
    private var sessionDirectUpBytes = 0L
    private var connectionUiState = ConnectionUiState.DISCONNECTED
    private var connectionErrorMessage: String? = null
    private var isSmartScanning = false
    private var smartConnectGeneration = 0
    private var showingServerScreen = false
    private var pickerOpen = false
    private var pickerFilter = PickerFilter.PLACES
    private lateinit var pickerAdapter: PickerServerAdapter

    private enum class PickerFilter { PLACES, FAVORITES, RECENT }

    private val homePanel get() = binding.jumpjumpHomeInclude
    private val serverPicker get() = binding.jumpjumpHomeInclude.serverPickerEmbed

    private val requestVpnPermission = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) {
        if (it.resultCode == RESULT_OK) {
            startV2Ray()
        } else {
            connectionUiState = ConnectionUiState.DISCONNECTED
            applyConnectionUi()
        }
    }
    private val requestActivityLauncher = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) {
        if (SettingsChangeManager.consumeRestartService() && mainViewModel.isRunning.value == true) {
            restartV2Ray()
        }
        if (SettingsChangeManager.consumeSetupGroupTab()) {
            setupGroupTab()
        }
    }


    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(binding.root)
        setupToolbar(binding.toolbar, false, getString(R.string.title_server))

        // setup viewpager and tablayout
        groupPagerAdapter = GroupPagerAdapter(this, emptyList())
        binding.viewPager.adapter = groupPagerAdapter
        binding.viewPager.isUserInputEnabled = true

        // setup navigation drawer
        setupNavigationDrawer()

        MonthlyTrafficTracker.checkPeriodOnLaunch(this)

        binding.fab.setOnClickListener { handleFabAction() }
        binding.fabUpdateSub.setOnClickListener { handleUpdateSubscription() }
        binding.fabPing.setOnClickListener { handlePingProfiles() }
        binding.layoutTest.setOnClickListener { handleLayoutTestClick() }

        setupHomeControls()
        setupServerPicker()
        setupFabBackgrounds()
        setupGroupTab()
        setupViewModel()
        SubscriptionUpdater.sync()
        mainViewModel.reloadServerList()
        refreshDaysRemaining()

        maybeShowOnboarding()
        maybeUpdateSubscriptionsOnLaunch()
        maybeCheckAppUpdate()

        checkAndRequestPermission(PermissionType.POST_NOTIFICATIONS) {
        }
    }

    private fun maybeShowOnboarding() {
        if (MmkvManager.decodeSettingsBool(AppConfig.PREF_ONBOARDING_DONE, false)) return
        startActivity(Intent(this, OnboardingActivity::class.java))
    }

    private fun maybeUpdateSubscriptionsOnLaunch() {
        if (!SettingsManager.isSubscriptionUpdateOnLaunchEnabled()) return
        lifecycleScope.launch(Dispatchers.IO) {
            val result = mainViewModel.updateConfigViaSubAll()
            withContext(Dispatchers.Main) {
                if (result.configCount > 0) {
                    mainViewModel.reloadServerList()
                    refreshGroupTabTitles()
                    SubscriptionUpdater.sync(forceReschedule = true)
                }
            }
        }
    }

    private fun maybeCheckAppUpdate() {
        if (!SettingsManager.isAutoCheckUpdateEnabled()) return
        if (SettingsManager.getAppUpdateApiUrl().isBlank()) return
        lifecycleScope.launch(Dispatchers.IO) {
            val includePre = MmkvManager.decodeSettingsBool(AppConfig.PREF_CHECK_UPDATE_PRE_RELEASE, false)
            val result = com.v2ray.ang.handler.UpdateCheckerManager.checkForUpdate(includePre)
            if (result.hasUpdate) {
                withContext(Dispatchers.Main) {
                    toast(getString(R.string.update_new_version_found, result.latestVersion))
                }
            }
        }
    }

    private fun setupHomeControls() {
        homePanel.laptopHero.setOnClickListener { handleFabAction() }
        serverPicker.layoutServerPicker.setOnClickListener { openServerPicker() }
        homePanel.tvHomeConnectionTest.setOnClickListener { handleLayoutTestClick() }

        homePanel.connectionModeSwitch.isSmartMode = SettingsManager.isSmartConnectionMode()
        homePanel.connectionModeSwitch.onModeChanged = { smart ->
            SettingsManager.setConnectionMode(smart)
            applyConnectionUi()
        }

        homePanel.modeSwitch.isVpnMode = SettingsManager.isVpnMode()
        homePanel.modeSwitch.onModeChanged = { vpn ->
            val newMode = if (vpn) VPN else PROXY_ONLY
            val currentMode = MmkvManager.decodeSettingsString(AppConfig.PREF_MODE, VPN)
            if (currentMode != newMode) {
                MmkvManager.encodeSettings(AppConfig.PREF_MODE, newMode)
                updateNavModeBadge()
                if (mainViewModel.isRunning.value == true) {
                    SettingsChangeManager.makeRestartService()
                    toast(getString(R.string.sumrax_mode_changed_restart))
                    restartV2Ray()
                }
            }
        }

        refreshServerPickerBar()
        applyConnectionUi()
        showHomeScreen()
    }

    private fun updateNavModeBadge() {
        val sidebar = binding.navSidebarInclude
        val isVpn = SettingsManager.isVpnMode()
        sidebar.navBrandInclude.tvNavModeBadge.text = if (isVpn) {
            getString(R.string.sumrax_mode_badge_vpn)
        } else {
            getString(R.string.sumrax_mode_badge_proxy)
        }
    }

    private fun setupServerPicker() {
        val overlay = binding.serverPickerOverlayInclude
        pickerAdapter = PickerServerAdapter(
            onSelect = { guid -> selectServerFromPicker(guid) },
            onFavoriteChanged = { refreshPickerList() }
        )
        overlay.rvPickerServers.layoutManager = LinearLayoutManager(this)
        overlay.rvPickerServers.adapter = pickerAdapter

        overlay.btnClosePicker.setOnClickListener { closeServerPicker() }
        overlay.overlayBackdrop.setOnClickListener { closeServerPicker() }

        overlay.btnPasteImport.setOnClickListener {
            val clipboard = getSystemService(CLIPBOARD_SERVICE) as ClipboardManager
            val text = clipboard.primaryClip?.getItemAt(0)?.text?.toString().orEmpty()
            if (text.isNotBlank()) {
                overlay.etImportLink.setText(text)
            } else {
                toastError(R.string.toast_failure)
            }
        }
        overlay.btnImportLink.setOnClickListener {
            importBatchConfig(overlay.etImportLink.text?.toString())
        }
        overlay.btnImportQr.setOnClickListener {
            closeServerPicker()
            importQRcode()
        }
        overlay.btnRefreshSubs.setOnClickListener {
            closeServerPicker()
            handleUpdateSubscription()
        }
        overlay.btnTestAll.setOnClickListener {
            closeServerPicker()
            handlePingProfiles()
        }

        overlay.chipPlaces.setOnClickListener {
            pickerFilter = PickerFilter.PLACES
            refreshPickerList()
        }
        overlay.chipFavorites.setOnClickListener {
            pickerFilter = PickerFilter.FAVORITES
            refreshPickerList()
        }
        overlay.chipRecent.setOnClickListener {
            pickerFilter = PickerFilter.RECENT
            refreshPickerList()
        }
    }

    fun openServerPicker() {
        val overlay = binding.serverPickerOverlayInclude
        overlay.root.isVisible = true
        overlay.root.alpha = 0f
        refreshPickerList()
        overlay.overlaySheet.post {
            val sheet = overlay.overlaySheet
            sheet.translationY = sheet.height.toFloat()
            overlay.root.animate().alpha(1f).setDuration(200).start()
            sheet.animate()
                .translationY(0f)
                .setDuration(280)
                .setInterpolator(DecelerateInterpolator())
                .start()
        }
        pickerOpen = true
    }

    fun closeServerPicker() {
        if (!pickerOpen) return
        val overlay = binding.serverPickerOverlayInclude
        val sheet = overlay.overlaySheet
        sheet.animate()
            .translationY(sheet.height.toFloat())
            .setDuration(220)
            .setInterpolator(DecelerateInterpolator())
            .start()
        overlay.root.animate()
            .alpha(0f)
            .setDuration(200)
            .withEndAction { overlay.root.isVisible = false }
            .start()
        pickerOpen = false
    }

    private fun refreshPickerList() {
        val favorites = ServerListsManager.getFavoriteGuids()
        val servers = when (pickerFilter) {
            PickerFilter.PLACES -> mainViewModel.serversCache.toList()
            PickerFilter.FAVORITES -> mainViewModel.serversCache.filter { favorites.contains(it.guid) }
            PickerFilter.RECENT -> {
                val recent = ServerListsManager.getRecentGuids()
                val byGuid = mainViewModel.serversCache.associateBy { it.guid }
                recent.mapNotNull { byGuid[it] }.ifEmpty {
                    mainViewModel.serversCache.toList().asReversed()
                }
            }
        }
        pickerAdapter.submitList(servers)
        if (servers.isEmpty() && pickerFilter == PickerFilter.FAVORITES) {
            toast(getString(R.string.sumrax_picker_favorites_empty))
        }
    }

    fun selectServerFromPicker(guid: String) {
        val selected = MmkvManager.getSelectServer()
        ServerListsManager.recordRecent(guid)
        if (guid != selected) {
            MmkvManager.setSelectServer(guid)
            if (mainViewModel.isRunning.value == true) {
                restartV2Ray()
            }
        }
        refreshDaysRemaining()
        refreshServerPickerBar()
        closeServerPicker()
        showHomeScreen()
    }

    fun showHomeScreen() {
        showingServerScreen = false
        binding.layoutServerScreen.isVisible = false
        homePanel.root.isVisible = true
        title = getString(R.string.title_server)
    }

    fun showServerScreen() {
        showingServerScreen = true
        homePanel.root.isVisible = false
        binding.layoutServerScreen.isVisible = true
        title = getString(R.string.jumpjump_servers_title)
    }

    fun refreshServerPickerBar() {
        val guid = MmkvManager.getSelectServer()
        if (guid.isNullOrEmpty()) {
            serverPicker.tvServerName.text = getString(R.string.sumrax_no_server_selected)
            serverPicker.tvServerSubtitle.text = ""
            serverPicker.tvServerPing.isVisible = false
            serverPicker.tvServerFlag.text = "🌐"
            return
        }

        val config = MmkvManager.decodeServerConfig(guid)
        val remarks = config?.remarks.orEmpty()
        serverPicker.tvServerName.text = remarks.ifBlank { config?.server.orEmpty() }
        val address = config?.getServerAddressAndPort().orEmpty()
        if (address.isNotBlank()) {
            serverPicker.tvServerSubtitle.isVisible = true
            serverPicker.tvServerSubtitle.text = address
        } else {
            serverPicker.tvServerSubtitle.isVisible = false
        }
        serverPicker.tvServerFlag.text = extractLeadingEmoji(remarks) ?: "🌐"

        val delay = MmkvManager.decodeServerAffiliationInfo(guid)?.testDelayMillis ?: 0L
        when {
            delay > 0L -> {
                serverPicker.tvServerPing.isVisible = true
                serverPicker.tvServerPing.text = "${delay}ms"
                serverPicker.tvServerPing.setTextColor(ContextCompat.getColor(this, R.color.sumrax_success))
            }
            delay < 0L -> {
                serverPicker.tvServerPing.isVisible = true
                serverPicker.tvServerPing.text = "—"
                serverPicker.tvServerPing.setTextColor(ContextCompat.getColor(this, R.color.sumrax_error))
            }
            else -> serverPicker.tvServerPing.isVisible = false
        }
    }

    private fun extractLeadingEmoji(text: String): String? {
        if (text.isEmpty()) return null
        val first = text.codePointAt(0)
        if (first !in 0x1F300..0x1FAFF && first !in 0x2600..0x27BF && first !in 0x1F1E6..0x1F1FF) {
            return null
        }
        val end = text.offsetByCodePoints(0, 1)
        return text.substring(0, end)
    }

    private fun setupFabBackgrounds() {
        binding.fabPing.setBackgroundResource(R.drawable.bg_fab_glass_cyan)
        binding.fabUpdateSub.setBackgroundResource(R.drawable.bg_fab_glass_magenta)
        binding.fab.backgroundTintList = null
        binding.fab.setBackgroundResource(R.drawable.bg_fab_glass_connect)
    }

    private fun handleUpdateSubscription() {
        animateFabRotation(binding.fabUpdateSub)
        setTestState(getString(R.string.title_updating_subscription))

        lifecycleScope.launch(Dispatchers.IO) {
            val result = mainViewModel.updateConfigViaSubAll()
            delay(300L)
            withContext(Dispatchers.Main) {
                val message = when {
                    result.successCount + result.failureCount + result.skipCount == 0 ->
                        getString(R.string.title_update_subscription_no_subscription)
                    result.configCount > 0 ->
                        getString(R.string.result_subscription_updated, result.configCount)
                    else -> getString(R.string.title_update_subscription_result,
                        result.configCount, result.successCount, result.failureCount, result.skipCount)
                }
                setTestState(message)
                if (result.configCount > 0) {
                    mainViewModel.reloadServerList()
                    refreshGroupTabTitles()
                }
            }
        }
    }

    private fun handlePingProfiles(showAnimation: Boolean = true) {
        if (showAnimation) {
            animateFabPulse(binding.fabPing)
        }
        mainViewModel.testAllPingWithSort(
            onStatus = { setTestState(it) },
            onComplete = { message ->
                if (message != MainViewModel.PING_CANCELLED) {
                    setTestState(message)
                }
            }
        )
    }

    private fun animateFabRotation(fab: com.google.android.material.floatingactionbutton.FloatingActionButton) {
        ObjectAnimator.ofFloat(fab, "rotation", 0f, 360f).apply {
            duration = 800
            interpolator = LinearInterpolator()
            start()
        }
    }

    private fun animateFabPulse(fab: com.google.android.material.floatingactionbutton.FloatingActionButton) {
        ObjectAnimator.ofFloat(fab, "alpha", 0.92f, 0.4f, 0.92f).apply {
            duration = 600
            repeatCount = 2
            start()
        }
        ObjectAnimator.ofFloat(fab, "scaleX", 1f, 1.15f, 1f).apply {
            duration = 600
            repeatCount = 2
            start()
        }
        ObjectAnimator.ofFloat(fab, "scaleY", 1f, 1.15f, 1f).apply {
            duration = 600
            repeatCount = 2
            start()
        }
    }

    private fun setupNavigationDrawer() {
        val toggle = ActionBarDrawerToggle(
            this,
            binding.drawerLayout,
            binding.toolbar,
            R.string.navigation_drawer_open,
            R.string.navigation_drawer_close
        )
        binding.drawerLayout.addDrawerListener(toggle)
        toggle.syncState()
        setupSumraxNav()

        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                when {
                    binding.drawerLayout.isDrawerOpen(GravityCompat.START) -> {
                        binding.drawerLayout.closeDrawer(GravityCompat.START)
                    }
                    pickerOpen -> closeServerPicker()
                    showingServerScreen -> showHomeScreen()
                    else -> {
                        isEnabled = false
                        onBackPressedDispatcher.onBackPressed()
                        isEnabled = true
                    }
                }
            }
        })
    }

    private fun setupSumraxNav() {
        val sidebar = binding.navSidebarInclude

        fun bindNavItem(includeBinding: ItemNavSumraxBinding, icon: String, label: String, selected: Boolean = false) {
            includeBinding.tvNavIcon.text = icon
            includeBinding.tvNavLabel.text = label
            includeBinding.navItemRoot.isSelected = selected
            if (selected) {
                includeBinding.tvNavLabel.setTextColor(ContextCompat.getColor(this, R.color.sumrax_success))
            }
        }

        bindNavItem(sidebar.navItemHome, "▣", getString(R.string.sumrax_nav_home), selected = true)
        bindNavItem(sidebar.navItemProtection, "⬡", getString(R.string.sumrax_nav_protection))
        bindNavItem(sidebar.navItemAi, "◎", getString(R.string.sumrax_nav_ai_label))
        bindNavItem(sidebar.navItemSettings, "⚙", getString(R.string.sumrax_nav_settings))

        sidebar.navItemHome.navItemRoot.setOnClickListener {
            binding.drawerLayout.closeDrawer(GravityCompat.START)
            showHomeScreen()
        }
        sidebar.navItemProtection.navItemRoot.setOnClickListener {
            binding.drawerLayout.closeDrawer(GravityCompat.START)
            requestActivityLauncher.launch(Intent(this, PerAppProxyActivity::class.java))
        }
        sidebar.navItemAi.navItemRoot.setOnClickListener {
            binding.drawerLayout.closeDrawer(GravityCompat.START)
            toast(getString(R.string.sumrax_nav_ai_coming_soon))
        }
        sidebar.navItemSettings.navItemRoot.setOnClickListener {
            binding.drawerLayout.closeDrawer(GravityCompat.START)
            requestActivityLauncher.launch(Intent(this, SettingsActivity::class.java))
        }

        navTrafficBinding = sidebar.navTrafficFooterInclude
        sidebar.navFooterInclude.tvNavVersion.text = "v${BuildConfig.VERSION_NAME}"

        updateNavModeBadge()

        updateTrafficFooterUi(0.0, 0.0, 0.0, 0.0, MonthlyTrafficTracker.loadCurrentTotals())
    }

    private fun setupViewModel() {
        mainViewModel.updateTestResultAction.observe(this) { setTestState(it) }
        mainViewModel.updateListAction.observe(this) {
            refreshDaysRemaining()
            refreshServerPickerBar()
            if (pickerOpen) refreshPickerList()
        }
        mainViewModel.isRunning.observe(this) { isRunning ->
            if (isRunning) {
                connectionUiState = ConnectionUiState.CONNECTED
                connectionErrorMessage = null
            } else if (connectionUiState == ConnectionUiState.CONNECTED) {
                connectionUiState = ConnectionUiState.DISCONNECTED
            }
            applyConnectionUi()
            refreshDaysRemaining()
            refreshServerPickerBar()
            if (isRunning) {
                startSpeedometerUpdates()
                lifecycleScope.launch {
                    delay(2000)
                    if (mainViewModel.isRunning.value == true) {
                        setTestState(getString(R.string.connection_test_testing))
                        mainViewModel.testCurrentServerRealPing()
                    }
                }
            } else {
                stopSpeedometerUpdates()
            }
        }
        mainViewModel.startListenBroadcast()
        mainViewModel.initAssets(assets)
    }

    fun refreshDaysRemaining() {
        val selectedGuid = MmkvManager.getSelectServer()
        val selectedProfile = selectedGuid?.let { MmkvManager.decodeServerConfig(it) }
        val selectedRemarks = selectedProfile?.remarks
        val runningRemarks = CoreServiceManager.getRunningServerName().takeIf { it.isNotBlank() }
        val fallback = findFirstProfileWithDays()

        val remarks = ProfileRemarkParser.resolveRemarksWithDays(
            selectedRemarks,
            runningRemarks,
            fallback?.second
        )
        val guid = when {
            selectedRemarks != null && ProfileRemarkParser.parseRemainingDays(selectedRemarks) != null -> selectedGuid
            fallback != null && remarks == fallback.second -> fallback.first
            else -> selectedGuid
        }
        val subscriptionId = guid?.let { MmkvManager.decodeServerConfig(it)?.subscriptionId }
            ?: selectedProfile?.subscriptionId
            ?: fallback?.let { MmkvManager.decodeServerConfig(it.first)?.subscriptionId }

        val days = ProfileRemarkParser.resolveLiveRemainingDays(remarks, subscriptionId, guid)
        if (days != null) {
            binding.layoutDaysRemaining.isVisible = true
            binding.tvDaysRemaining.text = ProfileRemarkParser.formatRemainingDays(this, days)
            val colorRes = when {
                days <= 3 -> R.color.sumrax_error
                days <= 7 -> R.color.sumrax_warning
                else -> R.color.colorPing
            }
            binding.tvDaysRemaining.setTextColor(ContextCompat.getColor(this, colorRes))
        } else {
            binding.layoutDaysRemaining.isVisible = false
        }
    }

    private fun findFirstProfileWithDays(): Pair<String, String>? {
        val serverList = if (mainViewModel.subscriptionId.isEmpty()) {
            MmkvManager.decodeAllServerList()
        } else {
            MmkvManager.decodeServerList(mainViewModel.subscriptionId)
        }
        return serverList.firstNotNullOfOrNull { guid ->
            val remarks = MmkvManager.decodeServerConfig(guid)?.remarks
            if (remarks != null && ProfileRemarkParser.parseRemainingDays(remarks) != null) {
                guid to remarks
            } else {
                null
            }
        }
    }

    private fun startSpeedometerUpdates() {
        speedometerJob?.cancel()
        lastQueryTime = System.currentTimeMillis()
        speedometerJob = lifecycleScope.launch(Dispatchers.IO) {
            while (isActive) {
                updateSpeedometer()
                delay(2000)
            }
        }
    }

    private fun stopSpeedometerUpdates() {
        speedometerJob?.cancel()
        speedometerJob = null
        sessionProxyDownBytes = 0L
        sessionProxyUpBytes = 0L
        sessionDirectDownBytes = 0L
        sessionDirectUpBytes = 0L
        runOnUiThread {
            homePanel.layoutHomeTraffic.isVisible = false
            homePanel.tvHomeConnectionTest.isVisible = false
            homePanel.tvHomeConnectionIp.isVisible = false
            updateTrafficFooterUi(0.0, 0.0, 0.0, 0.0, MonthlyTrafficTracker.loadCurrentTotals())
        }
    }

    private fun formatTrafficSpeedLine(tag: String, upBps: Double, downBps: Double): String {
        return "$tag • ${upBps.toLong().toSpeedString()}↑  ${downBps.toLong().toSpeedString()}↓"
    }

    private fun updateTrafficFooterUi(
        proxyUpBps: Double,
        proxyDownBps: Double,
        directUpBps: Double,
        directDownBps: Double,
        monthlyTotals: MonthlyTrafficTracker.TrafficTotals = MonthlyTrafficTracker.loadCurrentTotals()
    ) {
        navTrafficBinding?.apply {
            tvProxyTraffic.text = formatTrafficSpeedLine(AppConfig.TAG_PROXY, proxyUpBps, proxyDownBps)
            tvDirectTraffic.text = formatTrafficSpeedLine(AppConfig.TAG_DIRECT, directUpBps, directDownBps)
            tvProxyTotal.text = getString(
                R.string.drawer_traffic_session_total,
                sessionProxyUpBytes.toTrafficString(),
                sessionProxyDownBytes.toTrafficString()
            )
            tvDirectTotal.text = getString(
                R.string.drawer_traffic_session_total,
                sessionDirectUpBytes.toTrafficString(),
                sessionDirectDownBytes.toTrafficString()
            )
            tvMonthlyTraffic.text = MonthlyTrafficTracker.formatMonthlyLine(this@MainActivity, monthlyTotals)
        }
    }

    private suspend fun updateSpeedometer() {
        if (mainViewModel.isRunning.value != true) return

        val queryTime = System.currentTimeMillis()
        val sinceLastQuery = (queryTime - lastQueryTime).coerceAtLeast(1000L)
        val sinceSeconds = sinceLastQuery / 1000.0

        val traffic = TrafficStatsHelper.aggregate(CoreServiceManager.queryAllOutboundTrafficStats())

        sessionProxyDownBytes += traffic.proxyDownlink
        sessionProxyUpBytes += traffic.proxyUplink
        sessionDirectDownBytes += traffic.directDownlink
        sessionDirectUpBytes += traffic.directUplink
        lastQueryTime = queryTime
        val monthlyTotals = MonthlyTrafficTracker.recordDelta(
            traffic.proxyUplink,
            traffic.proxyDownlink,
            traffic.directUplink,
            traffic.directDownlink,
            applicationContext
        )

        withContext(Dispatchers.Main) {
            homePanel.layoutHomeTraffic.isVisible = true
            homePanel.tvHomeProxyTraffic.text = formatTrafficSpeedLine(
                AppConfig.TAG_PROXY,
                traffic.proxyUplink / sinceSeconds,
                traffic.proxyDownlink / sinceSeconds
            )
            homePanel.tvHomeDirectTraffic.text = formatTrafficSpeedLine(
                AppConfig.TAG_DIRECT,
                traffic.directUplink / sinceSeconds,
                traffic.directDownlink / sinceSeconds
            )
            updateTrafficFooterUi(
                traffic.proxyUplink / sinceSeconds,
                traffic.proxyDownlink / sinceSeconds,
                traffic.directUplink / sinceSeconds,
                traffic.directDownlink / sinceSeconds,
                monthlyTotals
            )
        }
    }

    private fun setupGroupTab() {
        val groups = mainViewModel.getSubscriptions(this)
        groupPagerAdapter.update(groups)

        tabMediator?.detach()
        tabMediator = TabLayoutMediator(binding.tabGroup, binding.viewPager) { tab, position ->
            groupPagerAdapter.groups.getOrNull(position)?.let {
                tab.text = it.remarks
                tab.tag = it.id
            }
        }.also { it.attach() }

        val targetIndex = groups.indexOfFirst { it.id == mainViewModel.subscriptionId }.takeIf { it >= 0 } ?: (groups.size - 1)
        binding.viewPager.setCurrentItem(targetIndex, false)

        binding.tabGroup.isVisible = groups.size > 1
        refreshGroupTabTitles(true)
    }

    fun refreshGroupTabTitles(refreshAll: Boolean = false) {
        val groupsToRefresh = if (refreshAll || mainViewModel.subscriptionId.isEmpty()) {
            groupPagerAdapter.groups
        } else {
            groupPagerAdapter.groups.filter { it.id == mainViewModel.subscriptionId }
        }

        groupsToRefresh.forEach { group ->
            if (group.id.isEmpty()) {
                return@forEach
            }
            val tabIndex = groupPagerAdapter.groups.indexOfFirst { it.id == group.id }
            if (tabIndex >= 0) {
                val count = MmkvManager.decodeServerList(group.id).size
                binding.tabGroup.getTabAt(tabIndex)?.text = "${group.remarks} ($count)"
            }
        }
    }

    private fun handleFabAction() {
        if (isSmartScanning || connectionUiState == ConnectionUiState.CONNECTING) {
            cancelSmartOrConnecting()
            return
        }

        if (mainViewModel.isRunning.value == true) {
            CoreServiceManager.stopVService(this)
            return
        }

        connectionErrorMessage = null
        if (SettingsManager.isSmartConnectionMode()) {
            when (SettingsManager.getAutoConnectType(mainViewModel.subscriptionId)) {
                AppConfig.AUTO_CONNECT_LAST_USED -> beginServiceConnect()
                AppConfig.AUTO_CONNECT_RANDOM -> {
                    val picked = ProfileAutoSelector.applyRandomSelection(mainViewModel.subscriptionId)
                    if (picked == null) {
                        connectionErrorMessage = getString(R.string.sumrax_smart_no_servers)
                        connectionUiState = ConnectionUiState.ERROR
                        applyConnectionUi()
                        toastError(R.string.sumrax_smart_no_servers)
                    } else {
                        refreshServerPickerBar()
                        beginServiceConnect()
                    }
                }
                else -> startSmartConnect()
            }
        } else {
            beginServiceConnect()
        }
    }

    private fun cancelSmartOrConnecting() {
        smartConnectGeneration++
        isSmartScanning = false
        mainViewModel.cancelAllPingTests()
        CoreServiceManager.stopVService(this)
        connectionUiState = ConnectionUiState.DISCONNECTED
        connectionErrorMessage = null
        applyConnectionUi()
    }

    private fun startSmartConnect() {
        if (mainViewModel.serversCache.isEmpty()) {
            connectionErrorMessage = getString(R.string.sumrax_no_server_selected)
            connectionUiState = ConnectionUiState.ERROR
            applyConnectionUi()
            return
        }

        isSmartScanning = true
        connectionUiState = ConnectionUiState.CONNECTING
        applyConnectionUi()

        val generation = ++smartConnectGeneration
        mainViewModel.testAllPingWithSort(
            onStatus = { status ->
                if (generation != smartConnectGeneration) return@testAllPingWithSort
                homePanel.tvHomeStatusSubtitle.text = status
            },
            onComplete = { message ->
                if (generation != smartConnectGeneration) return@testAllPingWithSort
                isSmartScanning = false

                if (message == MainViewModel.PING_CANCELLED) {
                    connectionUiState = ConnectionUiState.DISCONNECTED
                    applyConnectionUi()
                    return@testAllPingWithSort
                }

                val best = ProfileAutoSelector.applyBestReachableSelection(mainViewModel.subscriptionId)
                if (best == null) {
                    connectionErrorMessage = getString(R.string.sumrax_smart_no_servers)
                    connectionUiState = ConnectionUiState.ERROR
                    applyConnectionUi()
                    toastError(R.string.sumrax_smart_no_servers)
                    return@testAllPingWithSort
                }

                refreshServerPickerBar()
                homePanel.tvHomeStatusSubtitle.text = getString(
                    R.string.sumrax_smart_found,
                    best.remarks,
                    best.pingMs.toInt()
                )
                beginServiceConnect()
            }
        )
    }

    private fun beginServiceConnect() {
        connectionErrorMessage = null
        connectionUiState = ConnectionUiState.CONNECTING
        applyConnectionUi()

        if (SettingsManager.isVpnMode()) {
            val intent = VpnService.prepare(this)
            if (intent == null) {
                startV2Ray()
            } else {
                requestVpnPermission.launch(intent)
            }
        } else {
            startV2Ray()
        }
    }

    private fun handleLayoutTestClick() {
        if (mainViewModel.isRunning.value == true) {
            setTestState(getString(R.string.connection_test_testing))
            mainViewModel.testCurrentServerRealPing()
        } else {
            // service not running: keep existing no-op (could show a message if desired)
        }
    }

    private fun startV2Ray() {
        ProfileSettingsApplier.applyForCurrentSelection()
        if (MmkvManager.getSelectServer().isNullOrEmpty()) {
            connectionErrorMessage = getString(R.string.title_file_chooser)
            connectionUiState = ConnectionUiState.ERROR
            applyConnectionUi()
            return
        }
        CoreServiceManager.startVService(this)
    }

    fun restartV2Ray() {
        if (mainViewModel.isRunning.value == true) {
            CoreServiceManager.stopVService(this)
        }
        lifecycleScope.launch {
            delay(500)
            startV2Ray()
        }
    }

    private fun setTestState(content: String?) {
        binding.tvTestState.text = content
        if (content.isNullOrBlank()) {
            homePanel.tvHomeConnectionTest.isVisible = false
            homePanel.tvHomeConnectionIp.isVisible = false
            return
        }
        val lines = content.lines().filter { it.isNotBlank() }
        homePanel.layoutHomeTraffic.isVisible = true
        homePanel.tvHomeConnectionTest.text = lines.first()
        homePanel.tvHomeConnectionTest.isVisible = true
        val ipLine = lines.getOrNull(1).orEmpty()
        homePanel.tvHomeConnectionIp.text = ipLine
        homePanel.tvHomeConnectionIp.isVisible = ipLine.isNotEmpty()
    }

    private fun updateStatusDot(isRunning: Boolean) {
        binding.viewStatusDot.setBackgroundResource(
            if (isRunning) R.drawable.bg_status_dot_connected else R.drawable.bg_status_dot_idle
        )
    }

    private fun applyConnectionUi() {
        val isRunning = connectionUiState == ConnectionUiState.CONNECTED
        val isConnecting = connectionUiState == ConnectionUiState.CONNECTING

        homePanel.laptopHero.heroState = when (connectionUiState) {
            ConnectionUiState.CONNECTED -> LaptopHeroView.HeroState.CONNECTED
            ConnectionUiState.CONNECTING -> LaptopHeroView.HeroState.CONNECTING
            ConnectionUiState.ERROR, ConnectionUiState.DISCONNECTED -> LaptopHeroView.HeroState.IDLE
        }

        homePanel.tvHomeStatusTitle.text = when {
            isSmartScanning -> getString(R.string.sumrax_smart_scanning)
            connectionUiState == ConnectionUiState.CONNECTED -> getString(R.string.jumpjump_connected)
            connectionUiState == ConnectionUiState.CONNECTING -> getString(R.string.jumpjump_connecting)
            connectionUiState == ConnectionUiState.ERROR -> getString(R.string.sumrax_connection_failed)
            else -> getString(R.string.jumpjump_not_connected)
        }

        homePanel.tvHomeStatusTitle.setTextColor(
            ContextCompat.getColor(
                this,
                when (connectionUiState) {
                    ConnectionUiState.CONNECTED -> R.color.sumrax_success
                    ConnectionUiState.CONNECTING -> R.color.sumrax_accent
                    ConnectionUiState.ERROR, ConnectionUiState.DISCONNECTED -> R.color.sumrax_error
                }
            )
        )

        homePanel.tvHomeStatusSubtitle.text = when {
            isSmartScanning -> getString(R.string.sumrax_smart_cancel_hint)
            isRunning -> getString(R.string.jumpjump_tap_mac_disconnect)
            isConnecting -> getString(R.string.jumpjump_connecting)
            SettingsManager.isSmartConnectionMode() -> getString(R.string.sumrax_smart_tap_hint)
            else -> getString(R.string.sumrax_manual_tap_hint)
        }

        val error = connectionErrorMessage
        homePanel.tvConnectionError.isVisible = !error.isNullOrBlank() && connectionUiState == ConnectionUiState.ERROR
        homePanel.tvConnectionError.text = error.orEmpty()

        if (!isRunning) {
            homePanel.layoutHomeTraffic.isVisible = false
            homePanel.tvHomeConnectionTest.isVisible = false
            homePanel.tvHomeConnectionIp.isVisible = false
        }

        updateStatusDot(isRunning)
    }

    override fun onResume() {
        super.onResume()
        refreshDaysRemaining()
        refreshServerPickerBar()
        homePanel.modeSwitch.isVpnMode = SettingsManager.isVpnMode()
        updateNavModeBadge()
        applyConnectionUi()
    }

    override fun onPause() {
        super.onPause()
    }

    override fun onCreateOptionsMenu(menu: Menu): Boolean {
        menuInflater.inflate(R.menu.menu_main, menu)

        val searchItem = menu.findItem(R.id.search_view)
        if (searchItem != null) {
            val searchView = searchItem.actionView as SearchView
            searchView.setOnQueryTextListener(object : SearchView.OnQueryTextListener {
                override fun onQueryTextSubmit(query: String?): Boolean = false

                override fun onQueryTextChange(newText: String?): Boolean {
                    mainViewModel.filterConfig(newText.orEmpty())
                    return false
                }
            })

            searchView.setOnCloseListener {
                mainViewModel.filterConfig("")
                false
            }
        }
        return super.onCreateOptionsMenu(menu)
    }

    override fun onOptionsItemSelected(item: MenuItem) = when (item.itemId) {
        R.id.import_qrcode -> {
            importQRcode()
            true
        }

        R.id.import_clipboard -> {
            importClipboard()
            true
        }

        R.id.import_local -> {
            importConfigLocal()
            true
        }

        R.id.import_manually_policy_group -> {
            importManually(EConfigType.POLICYGROUP.value)
            true
        }

        R.id.import_manually_proxy_chain -> {
            importManually(EConfigType.PROXYCHAIN.value)
            true
        }

        R.id.import_manually_vmess -> {
            importManually(EConfigType.VMESS.value)
            true
        }

        R.id.import_manually_vless -> {
            importManually(EConfigType.VLESS.value)
            true
        }

        R.id.import_manually_ss -> {
            importManually(EConfigType.SHADOWSOCKS.value)
            true
        }

        R.id.import_manually_socks -> {
            importManually(EConfigType.SOCKS.value)
            true
        }

        R.id.import_manually_http -> {
            importManually(EConfigType.HTTP.value)
            true
        }

        R.id.import_manually_trojan -> {
            importManually(EConfigType.TROJAN.value)
            true
        }

        R.id.import_manually_wireguard -> {
            importManually(EConfigType.WIREGUARD.value)
            true
        }

        R.id.import_manually_hysteria2 -> {
            importManually(EConfigType.HYSTERIA2.value)
            true
        }

        R.id.ping_all -> {
            toast(getString(R.string.connection_test_testing_count, mainViewModel.serversCache.count()))
            mainViewModel.testAllTcping()
            true
        }

        R.id.real_ping_all -> {
            toast(getString(R.string.connection_test_testing_count, mainViewModel.serversCache.count()))
            mainViewModel.testAllRealPing()
            true
        }

        R.id.service_restart -> {
            restartV2Ray()
            true
        }

        R.id.del_all_config -> {
            delAllConfig()
            true
        }

        R.id.del_duplicate_config -> {
            delDuplicateConfig()
            true
        }

        R.id.del_invalid_config -> {
            delInvalidConfig()
            true
        }

        R.id.sort_by_test_results -> {
            sortByTestResults()
            true
        }

        R.id.sub_update -> {
            importConfigViaSub()
            true
        }

        R.id.locate_selected_config -> {
            locateSelectedServer()
            true
        }

        else -> super.onOptionsItemSelected(item)
    }

    private fun importManually(createConfigType: Int) {
        if (createConfigType == EConfigType.POLICYGROUP.value) {
            startActivity(
                Intent()
                    .putExtra("subscriptionId", mainViewModel.subscriptionId)
                    .setClass(this, ServerGroupActivity::class.java)
            )
        } else if (createConfigType == EConfigType.PROXYCHAIN.value) {
            startActivity(
                Intent()
                    .putExtra("subscriptionId", mainViewModel.subscriptionId)
                    .setClass(this, ServerProxyChainActivity::class.java)
            )
        } else {
            startActivity(
                Intent()
                    .putExtra("createConfigType", createConfigType)
                    .putExtra("subscriptionId", mainViewModel.subscriptionId)
                    .setClass(this, ServerActivity::class.java)
            )
        }
    }

    /**
     * import config from qrcode
     */
    private fun importQRcode(): Boolean {
        launchQRCodeScanner { scanResult ->
            if (scanResult != null) {
                importBatchConfig(scanResult)
            }
        }
        return true
    }

    /**
     * import config from clipboard
     */
    private fun importClipboard()
            : Boolean {
        try {
            val clipboard = Utils.getClipboard(this)
            importBatchConfig(clipboard)
        } catch (e: Exception) {
            LogUtil.e(AppConfig.TAG, "Failed to import config from clipboard", e)
            return false
        }
        return true
    }

    private fun importBatchConfig(server: String?) {
        showLoading()

        lifecycleScope.launch(Dispatchers.IO) {
            try {
                val (count, countSub) = AngConfigManager.importBatchConfig(server, mainViewModel.subscriptionId, true)
                delay(500L)
                withContext(Dispatchers.Main) {
                    when {
                        count > 0 -> {
                            toast(getString(R.string.title_import_config_count, count))
                            mainViewModel.reloadServerList()
                            refreshGroupTabTitles()
                        }

                        countSub > 0 -> setupGroupTab()
                        EncryptedCryptResolver.isEncryptedDeeplink(server) ->
                            toastError(R.string.toast_encrypted_import_failed)
                        else -> toastError(R.string.toast_failure)
                    }
                    hideLoading()
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    toastError(R.string.toast_failure)
                    hideLoading()
                }
                LogUtil.e(AppConfig.TAG, "Failed to import batch config", e)
            }
        }
    }

    /**
     * import config from local config file
     */
    private fun importConfigLocal(): Boolean {
        try {
            showFileChooser()
        } catch (e: Exception) {
            LogUtil.e(AppConfig.TAG, "Failed to import config from local file", e)
            return false
        }
        return true
    }


    /**
     * import config from sub
     */
    fun importConfigViaSub(): Boolean {
        showLoading()

        lifecycleScope.launch(Dispatchers.IO) {
            val result = mainViewModel.updateConfigViaSubAll()
            delay(500L)
            launch(Dispatchers.Main) {
                if (result.successCount + result.failureCount + result.skipCount == 0) {
                    toast(R.string.title_update_subscription_no_subscription)
                } else if (result.successCount > 0 && result.failureCount + result.skipCount == 0) {
                    toast(getString(R.string.title_update_config_count, result.configCount))
                } else {
                    toast(
                        getString(
                            R.string.title_update_subscription_result,
                            result.configCount, result.successCount, result.failureCount, result.skipCount
                        )
                    )
                }
                if (result.configCount > 0) {
                    mainViewModel.reloadServerList()
                    refreshGroupTabTitles()
                }
                hideLoading()
            }
        }
        return true
    }

    private fun delAllConfig() {
        AlertDialog.Builder(this).setMessage(R.string.del_config_comfirm)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                showLoading()
                lifecycleScope.launch(Dispatchers.IO) {
                    val ret = mainViewModel.removeAllServer()
                    launch(Dispatchers.Main) {
                        mainViewModel.reloadServerList()
                        refreshGroupTabTitles()
                        toast(getString(R.string.title_del_config_count, ret))
                        hideLoading()
                    }
                }
            }
            .setNegativeButton(android.R.string.cancel) { _, _ ->
                //do noting
            }
            .show()
    }

    private fun delDuplicateConfig() {
        AlertDialog.Builder(this).setMessage(R.string.del_config_comfirm)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                showLoading()
                lifecycleScope.launch(Dispatchers.IO) {
                    val ret = mainViewModel.removeDuplicateServer()
                    launch(Dispatchers.Main) {
                        mainViewModel.reloadServerList()
                        refreshGroupTabTitles()
                        toast(getString(R.string.title_del_duplicate_config_count, ret))
                        hideLoading()
                    }
                }
            }
            .setNegativeButton(android.R.string.cancel) { _, _ ->
                //do noting
            }
            .show()
    }

    private fun delInvalidConfig() {
        AlertDialog.Builder(this).setMessage(R.string.del_invalid_config_comfirm)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                showLoading()
                lifecycleScope.launch(Dispatchers.IO) {
                    val ret = mainViewModel.removeInvalidServer()
                    launch(Dispatchers.Main) {
                        mainViewModel.reloadServerList()
                        refreshGroupTabTitles()
                        toast(getString(R.string.title_del_config_count, ret))
                        hideLoading()
                    }
                }
            }
            .setNegativeButton(android.R.string.cancel) { _, _ ->
                //do noting
            }
            .show()
    }

    private fun sortByTestResults() {
        showLoading()
        lifecycleScope.launch(Dispatchers.IO) {
            mainViewModel.sortByTestResults()
            launch(Dispatchers.Main) {
                mainViewModel.reloadServerList()
                hideLoading()
            }
        }
    }

    /**
     * show file chooser
     */
    private fun showFileChooser() {
        launchFileChooser { uri ->
            if (uri == null) {
                return@launchFileChooser
            }

            readContentFromUri(uri)
        }
    }

    /**
     * read content from uri
     */
    private fun readContentFromUri(uri: Uri) {
        try {
            contentResolver.openInputStream(uri).use { input ->
                importBatchConfig(input?.bufferedReader()?.readText())
            }
        } catch (e: Exception) {
            LogUtil.e(AppConfig.TAG, "Failed to read content from URI", e)
        }
    }

    /**
     * Locates and scrolls to the currently selected server.
     * If the selected server is in a different group, automatically switches to that group first.
     */
    private fun locateSelectedServer() {
        showServerScreen()
        val targetSubscriptionId = mainViewModel.findSubscriptionIdBySelect()
        if (targetSubscriptionId.isNullOrEmpty()) {
            toast(R.string.title_file_chooser)
            return
        }

        val targetGroupIndex = groupPagerAdapter.groups.indexOfFirst { it.id == targetSubscriptionId }
        if (targetGroupIndex < 0) {
            toast(R.string.toast_server_not_found_in_group)
            return
        }

        // Switch to target group if needed, then scroll to the server
        if (binding.viewPager.currentItem != targetGroupIndex) {
            binding.viewPager.setCurrentItem(targetGroupIndex, true)
            binding.viewPager.postDelayed({ scrollToSelectedServer(targetGroupIndex) }, 1000)
        } else {
            scrollToSelectedServer(targetGroupIndex)
        }
    }

    /**
     * Scrolls to the selected server in the specified fragment.
     * @param groupIndex The index of the group/fragment to scroll in
     */
    private fun scrollToSelectedServer(groupIndex: Int) {
        val itemId = groupPagerAdapter.getItemId(groupIndex)
        val fragment = supportFragmentManager.findFragmentByTag("f$itemId") as? GroupServerFragment

        if (fragment?.isAdded == true && fragment.view != null) {
            fragment.scrollToSelectedServer()
        } else {
            toast(R.string.toast_fragment_not_available)
        }
    }

    override fun onKeyDown(keyCode: Int, event: KeyEvent): Boolean {
        if (keyCode == KeyEvent.KEYCODE_BACK || keyCode == KeyEvent.KEYCODE_BUTTON_B) {
            moveTaskToBack(false)
            return true
        }
        return super.onKeyDown(keyCode, event)
    }


    override fun onDestroy() {
        speedometerJob?.cancel()
        tabMediator?.detach()
        super.onDestroy()
    }
}