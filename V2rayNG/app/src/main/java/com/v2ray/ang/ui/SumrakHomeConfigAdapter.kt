package com.v2ray.ang.ui

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.core.content.ContextCompat
import androidx.core.view.isVisible
import androidx.recyclerview.widget.RecyclerView
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ItemSumraxHomeGroupBinding
import com.v2ray.ang.databinding.ItemSumraxHomeServerBinding
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.util.SumrakServerProtocol
import com.v2ray.ang.util.ServerDisplayName

class SumrakHomeConfigAdapter(
    private val callbacks: Callbacks,
) : RecyclerView.Adapter<RecyclerView.ViewHolder>() {

    interface Callbacks {
        fun onSelectServer(guid: String)
        fun onEditServer(guid: String, subscriptionId: String)
        fun onToggleGroup(subscriptionId: String)
        fun onRefreshGroup(subscriptionId: String)
        fun onPingGroup(subscriptionId: String)
        fun onMoreGroup(subscriptionId: String, anchor: View)
        fun onServerPingTap(guid: String)
    }

    private var rows: List<SumrakHomeRow> = emptyList()

    fun submit(rows: List<SumrakHomeRow>) {
        this.rows = rows
        notifyDataSetChanged()
    }

    override fun getItemViewType(position: Int): Int =
        when (rows[position]) {
            is SumrakHomeRow.GroupHeader -> TYPE_GROUP
            is SumrakHomeRow.Server -> TYPE_SERVER
        }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): RecyclerView.ViewHolder {
        val inflater = LayoutInflater.from(parent.context)
        return when (viewType) {
            TYPE_GROUP -> GroupHolder(ItemSumraxHomeGroupBinding.inflate(inflater, parent, false))
            else -> ServerHolder(ItemSumraxHomeServerBinding.inflate(inflater, parent, false))
        }
    }

    override fun onBindViewHolder(holder: RecyclerView.ViewHolder, position: Int) {
        when (val row = rows[position]) {
            is SumrakHomeRow.GroupHeader -> (holder as GroupHolder).bind(row)
            is SumrakHomeRow.Server -> (holder as ServerHolder).bind(row)
        }
    }

    override fun getItemCount(): Int = rows.size

    inner class GroupHolder(
        private val binding: ItemSumraxHomeGroupBinding,
    ) : RecyclerView.ViewHolder(binding.root) {
        fun bind(row: SumrakHomeRow.GroupHeader) {
            binding.configGroupName.text = row.title
            binding.configGroupDescription.text = row.subtitle
            binding.configGroupDescription.isVisible = row.subtitle.isNotBlank()
            binding.configGroupExpandCollapse.setImageResource(
                if (row.expanded) R.drawable.ic_expand_less_24dp else R.drawable.ic_expand_more_24dp
            )
            binding.configGroupExpandCollapse.setOnClickListener {
                callbacks.onToggleGroup(row.subscriptionId)
            }
            binding.configGroupRefresh.setOnClickListener {
                callbacks.onRefreshGroup(row.subscriptionId)
            }
            binding.configGroupPing.setOnClickListener {
                callbacks.onPingGroup(row.subscriptionId)
            }
            binding.configGroupMore.setOnClickListener {
                callbacks.onMoreGroup(row.subscriptionId, it)
            }
        }
    }

    inner class ServerHolder(
        private val binding: ItemSumraxHomeServerBinding,
    ) : RecyclerView.ViewHolder(binding.root) {
        fun bind(row: SumrakHomeRow.Server) {
            val context = binding.root.context
            val guid = row.cache.guid
            val profile = row.cache.profile
            val selected = guid == MmkvManager.getSelectServer()
            binding.configActivated.isVisible = selected

            val display = ServerDisplayName.fromProfile(profile.remarks, profile.server)
            val emoji = display.firstOrNull()?.code?.let { cp ->
                if (cp in 0x1F300..0x1FAFF || cp in 0x2600..0x27BF) {
                    display.take(display.offsetByCodePoints(0, 1))
                } else null
            }
            val name = if (emoji != null) display.removePrefix(emoji).trim() else display
            binding.ivDeviceIcon.text = emoji ?: "🌐"
            binding.tvPickerName.text = name.ifBlank { display }
            binding.tvPickerSubtitle.text = SumrakServerProtocol.format(profile)
            binding.tvPickerSubtitle.isVisible = true

            val delay = MmkvManager.decodeServerAffiliationInfo(guid)?.testDelayMillis ?: 0L
            when {
                delay > 0L -> {
                    binding.tvPickerPing.text = context.getString(R.string.sumrax_ping_ms, delay)
                    binding.tvPickerPing.setTextColor(
                        ContextCompat.getColor(context, R.color.sumrax_ui_ping_ok)
                    )
                }
                delay < 0L -> {
                    binding.tvPickerPing.text = context.getString(R.string.sumrax_ping_na)
                    binding.tvPickerPing.setTextColor(
                        ContextCompat.getColor(context, R.color.sumrax_ui_settings_red)
                    )
                }
                else -> {
                    binding.tvPickerPing.text = context.getString(R.string.sumrax_ping_na)
                    binding.tvPickerPing.setTextColor(
                        ContextCompat.getColor(context, R.color.sumrax_ui_text_tertiary)
                    )
                }
            }

            binding.itemPickerRoot.setOnClickListener { callbacks.onSelectServer(guid) }
            binding.btnServerForward.isVisible = false
        }
    }

    companion object {
        private const val TYPE_GROUP = 0
        private const val TYPE_SERVER = 1
    }
}