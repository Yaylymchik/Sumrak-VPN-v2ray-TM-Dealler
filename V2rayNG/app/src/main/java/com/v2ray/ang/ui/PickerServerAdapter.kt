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
            val isFavorite = ServerListsManager.isFavorite(guid)

            binding.tvPickerName.text = profile.remarks.ifBlank { profile.server }
            binding.tvPickerSubtitle.isVisible = false

            val aff = MmkvManager.decodeServerAffiliationInfo(guid)
            val delay = aff?.testDelayMillis ?: 0L
            when {
                delay > 0L -> {
                    binding.tvPickerPing.isVisible = true
                    binding.tvPickerPing.text = context.getString(R.string.sumrax_ping_ms, delay)
                    binding.tvPickerPing.setTextColor(ContextCompat.getColor(context, R.color.sumrax_success))
                }
                delay < 0L -> {
                    binding.tvPickerPing.isVisible = true
                    binding.tvPickerPing.text = "—"
                    binding.tvPickerPing.setTextColor(ContextCompat.getColor(context, R.color.sumrax_error))
                }
                else -> binding.tvPickerPing.isVisible = false
            }

            binding.btnFavorite.setImageResource(
                if (isFavorite) R.drawable.ic_star_24dp else R.drawable.ic_star_border_24dp
            )
            binding.btnFavorite.imageTintList = ContextCompat.getColorStateList(
                context,
                if (isFavorite) R.color.sumrax_accent else R.color.sumrax_text_tertiary
            )
            binding.btnFavorite.setOnClickListener {
                ServerListsManager.toggleFavorite(guid)
                onFavoriteChanged?.invoke()
                notifyItemChanged(bindingAdapterPosition)
            }

            binding.itemPickerRoot.setBackgroundResource(
                if (isSelected) R.drawable.bg_glass_profile_selected else R.drawable.bg_glass_profile_item
            )
            binding.itemPickerRoot.setOnClickListener { onSelect(guid) }
            binding.itemPickerRoot.setOnLongClickListener {
                ServerListsManager.toggleFavorite(guid)
                onFavoriteChanged?.invoke()
                notifyItemChanged(bindingAdapterPosition)
                true
            }
        }
    }
}
