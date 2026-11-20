package com.v2ray.ang.ui

import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.core.content.ContextCompat
import androidx.core.view.isVisible
import androidx.recyclerview.widget.RecyclerView
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ItemPickerServerBinding
import com.v2ray.ang.dto.entities.ServersCache
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.handler.ServerListsManager
import com.v2ray.ang.util.ServerDisplayName

class PickerServerAdapter(
    private val onSelect: (String) -> Unit,
    private val onFavoriteChanged: (() -> Unit)? = null
) : RecyclerView.Adapter<PickerServerAdapter.ViewHolder>() {

    private var items: List<ServersCache> = emptyList()

    fun submitList(newItems: List<ServersCache>) {
        items = newItems
        notifyDataSetChanged()
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ViewHolder {
        val binding = ItemPickerServerBinding.inflate(LayoutInflater.from(parent.context), parent, false)
        return ViewHolder(binding)
    }

    override fun onBindViewHolder(holder: ViewHolder, position: Int) {
        holder.bind(items[position])
    }

    override fun getItemCount(): Int = items.size

    inner class ViewHolder(
        private val binding: ItemPickerServerBinding
    ) : RecyclerView.ViewHolder(binding.root) {

        fun bind(item: ServersCache) {
            val context = binding.root.context
            val guid = item.guid
            val profile = item.profile
            val isSelected = guid == MmkvManager.getSelectServer()
            binding.tvPickerName.text = ServerDisplayName.fromProfile(profile.remarks, profile.server)
            binding.tvPickerSubtitle.isVisible = false

            val aff = MmkvManager.decodeServerAffiliationInfo(guid)
            val delay = aff?.testDelayMillis ?: 0L
            when {
                delay > 0L -> {
                    binding.tvPickerPing.isVisible = true
                    binding.tvPickerPing.text = context.getString(R.string.sumrax_ping_ms, delay)
                    binding.tvPickerPing.setTextColor(ContextCompat.getColor(context, R.color.happ_ping_ok))
                }
                delay < 0L -> {
                    binding.tvPickerPing.isVisible = true
                    binding.tvPickerPing.text = "—"
                    binding.tvPickerPing.setTextColor(ContextCompat.getColor(context, R.color.happ_settings_red))
                }
                else -> binding.tvPickerPing.isVisible = false
            }

            binding.configActivated.isVisible = isSelected
            binding.btnFavorite.isVisible = false

            binding.btnServerForward.setOnClickListener { onSelect(guid) }
            binding.itemPickerRoot.setOnClickListener { onSelect(guid) }
        }
    }
}
