package com.v2ray.ang.ui

import android.text.TextUtils
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import com.v2ray.ang.R
import com.v2ray.ang.contracts.BaseAdapterListener
import com.v2ray.ang.databinding.ItemRecyclerSubSettingBinding
import com.v2ray.ang.handler.MmkvManager
import com.v2ray.ang.helper.ItemTouchHelperAdapter
import com.v2ray.ang.helper.ItemTouchHelperViewHolder
import com.v2ray.ang.util.SubscriptionLock
import com.v2ray.ang.util.Utils
import com.v2ray.ang.viewmodel.SubscriptionsViewModel

class SubSettingRecyclerAdapter(
    private val viewModel: SubscriptionsViewModel,
    private val adapterListener: BaseAdapterListener?
) : RecyclerView.Adapter<SubSettingRecyclerAdapter.MainViewHolder>(), ItemTouchHelperAdapter {

    fun isLocked(subId: String): Boolean {
        return SubscriptionLock.isLocked(MmkvManager.decodeSubscription(subId))
    }

    fun getSubId(position: Int): String? = viewModel.getAll().getOrNull(position)?.guid

    override fun getItemCount() = viewModel.getAll().size

    override fun onBindViewHolder(holder: MainViewHolder, position: Int) {
        val subscriptions = viewModel.getAll()
        val subId = subscriptions[position].guid
        val subItem = subscriptions[position].subscription
        val locked = isLocked(subId)
        holder.itemSubSettingBinding.tvName.text = subItem.remarks
        holder.itemSubSettingBinding.tvUrl.text = subItem.url
        holder.itemSubSettingBinding.chkEnable.isChecked = subItem.enabled
        holder.itemSubSettingBinding.chkEnable.isEnabled = !locked
        holder.itemSubSettingBinding.tvLastUpdated.text = Utils.formatTimestamp(subItem.lastUpdated)

        holder.itemSubSettingBinding.layoutEdit.visibility = View.GONE
        holder.itemSubSettingBinding.layoutShare.visibility = View.GONE
        holder.itemSubSettingBinding.layoutUrl.visibility = View.GONE
        holder.itemSubSettingBinding.layoutLastUpdated.visibility =
            if (TextUtils.isEmpty(subItem.url)) View.GONE else View.VISIBLE

        holder.itemSubSettingBinding.layoutRemove.setOnClickListener {
            adapterListener?.onRemove(subId, position)
        }

        holder.itemSubSettingBinding.infoContainer.apply {
            isEnabled = !locked
            isClickable = !locked
            isFocusable = !locked
            if (locked) {
                foreground = null
                setOnClickListener {
                    adapterListener?.onLockedEditAttempt()
                }
            } else {
                setOnClickListener { adapterListener?.onEdit(subId, position) }
            }
        }
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): MainViewHolder {
        return MainViewHolder(
            ItemRecyclerSubSettingBinding.inflate(
                LayoutInflater.from(parent.context),
                parent,
                false
            )
        )
    }

    class MainViewHolder(val itemSubSettingBinding: ItemRecyclerSubSettingBinding) :
        BaseViewHolder(itemSubSettingBinding.root), ItemTouchHelperViewHolder

    open class BaseViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        fun onItemSelected() {
            itemView.setBackgroundResource(R.drawable.bg_glass_profile_selected)
        }

        fun onItemClear() {
            itemView.setBackgroundResource(R.drawable.bg_glass_profile_item)
        }
    }

    override fun onItemMove(fromPosition: Int, toPosition: Int): Boolean {
        val fromId = viewModel.getAll().getOrNull(fromPosition)?.guid.orEmpty()
        val toId = viewModel.getAll().getOrNull(toPosition)?.guid.orEmpty()
        if (isLocked(fromId) || isLocked(toId)) {
            return false
        }
        viewModel.swap(fromPosition, toPosition)
        notifyItemMoved(fromPosition, toPosition)
        return true
    }

    override fun onItemMoveCompleted() {
        adapterListener?.onRefreshData()
    }

    override fun onItemDismiss(position: Int) {
    }
}
