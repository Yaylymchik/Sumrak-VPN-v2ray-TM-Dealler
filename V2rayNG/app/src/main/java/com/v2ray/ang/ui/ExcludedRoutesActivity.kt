package com.v2ray.ang.ui

import android.content.Intent
import android.os.Bundle
import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.appcompat.app.AlertDialog
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ActivityExcludedRoutesBinding
import com.v2ray.ang.databinding.ItemExcludedRouteBinding
import com.v2ray.ang.extension.toast
import com.v2ray.ang.extension.toastError
import com.v2ray.ang.handler.ExcludedRoutesManager
import com.v2ray.ang.handler.SettingsChangeManager

class ExcludedRoutesActivity : BaseActivity() {

    private val binding by lazy { ActivityExcludedRoutesBinding.inflate(layoutInflater) }
    private val adapter = ExcludedRouteAdapter { route ->
        AlertDialog.Builder(this)
            .setMessage(getString(R.string.sumrax_excluded_routes_remove_confirm, route))
            .setPositiveButton(android.R.string.ok) { _, _ ->
                ExcludedRoutesManager.removeRoute(route)
                SettingsChangeManager.makeRestartService()
                refreshList()
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(
            binding.root,
            showHomeAsUp = true,
            title = getString(R.string.sumrax_settings_excluded_routes)
        )
        binding.rvExcludedRoutes.layoutManager = LinearLayoutManager(this)
        binding.rvExcludedRoutes.adapter = adapter
        binding.btnAddRoute.setOnClickListener { addRoute() }
        refreshList()
    }

    private fun addRoute() {
        val raw = binding.etRoute.text?.toString().orEmpty()
        if (!ExcludedRoutesManager.addRoute(raw)) {
            toastError(R.string.sumrax_excluded_routes_invalid)
            return
        }
        binding.etRoute.text?.clear()
        SettingsChangeManager.makeRestartService()
        toast(R.string.sumrax_excluded_routes_added)
        refreshList()
    }

    private fun refreshList() {
        adapter.submit(ExcludedRoutesManager.getRoutes())
    }

    private class ExcludedRouteAdapter(
        private val onRemove: (String) -> Unit
    ) : RecyclerView.Adapter<ExcludedRouteAdapter.Holder>() {

        private var items: List<String> = emptyList()

        fun submit(routes: List<String>) {
            items = routes
            notifyDataSetChanged()
        }

        override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): Holder {
            val binding = ItemExcludedRouteBinding.inflate(
                LayoutInflater.from(parent.context),
                parent,
                false
            )
            return Holder(binding)
        }

        override fun onBindViewHolder(holder: Holder, position: Int) {
            holder.bind(items[position], onRemove)
        }

        override fun getItemCount(): Int = items.size

        class Holder(private val binding: ItemExcludedRouteBinding) :
            RecyclerView.ViewHolder(binding.root) {
            fun bind(route: String, onRemove: (String) -> Unit) {
                binding.tvRoute.text = route
                binding.btnRemove.setOnClickListener { onRemove(route) }
            }
        }
    }
}

class ReportActivity : BaseActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val intent = Intent(Intent.ACTION_SENDTO).apply {
            data = android.net.Uri.parse("mailto:")
            putExtra(Intent.EXTRA_EMAIL, arrayOf("support@happ.su"))
            putExtra(Intent.EXTRA_SUBJECT, "${getString(R.string.app_name)} report")
        }
        if (intent.resolveActivity(packageManager) != null) {
            startActivity(intent)
        }
        finish()
    }
}
