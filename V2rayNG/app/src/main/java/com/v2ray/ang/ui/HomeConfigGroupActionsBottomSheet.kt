package com.v2ray.ang.ui

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.ImageView
import android.widget.TextView
import androidx.core.os.bundleOf
import androidx.core.view.isVisible
import com.google.android.material.bottomsheet.BottomSheetDialogFragment
import com.v2ray.ang.R

/**
 * Bottom sheet for subscription group actions on the home screen.
 */
class HomeConfigGroupActionsBottomSheet : BottomSheetDialogFragment() {

    enum class Action {
        SETTINGS,
        ROUTING,
        UPDATE_SUBSCRIPTION,
        PING,
        PIN,
        DELETE,
    }

    interface Listener {
        fun onGroupAction(subscriptionId: String, action: Action)
    }

    private val subscriptionId: String
        get() = requireArguments().getString(ARG_SUBSCRIPTION_ID).orEmpty()

    private val pinned: Boolean
        get() = requireArguments().getBoolean(ARG_PINNED)

    private val showSubscriptionActions: Boolean
        get() = requireArguments().getBoolean(ARG_SHOW_SUBSCRIPTION_ACTIONS)

    private var listener: Listener? = null

    fun setListener(listener: Listener) {
        this.listener = listener
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setStyle(STYLE_NORMAL, R.style.SumrakBottomSheetDialog)
    }

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View = inflater.inflate(R.layout.dialog_config_group_actions, container, false)

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        bindRow(
            view.findViewById(R.id.act_settings),
            R.drawable.ic_settings_24dp,
            R.string.dialog_config_group_actions_settings,
        ) {
            dispatch(Action.SETTINGS)
        }.root.isVisible = showSubscriptionActions

        bindRow(
            view.findViewById(R.id.act_routing),
            R.drawable.ic_routing_24dp,
            R.string.dialog_config_group_actions_routing,
        ) {
            dispatch(Action.ROUTING)
        }

        bindRow(
            view.findViewById(R.id.act_update_sub),
            R.drawable.ic_refresh_24dp,
            R.string.dialog_config_group_actions_update_subscription,
        ) {
            dispatch(Action.UPDATE_SUBSCRIPTION)
        }.root.isVisible = showSubscriptionActions

        bindRow(
            view.findViewById(R.id.act_ping),
            R.drawable.ic_speed_24dp,
            R.string.dialog_config_group_actions_ping,
        ) {
            dispatch(Action.PING)
        }

        val pinLabel = if (pinned) {
            R.string.dialog_config_group_actions_unpin
        } else {
            R.string.dialog_config_group_actions_pin
        }
        bindRow(
            view.findViewById(R.id.act_pin),
            R.drawable.ic_push_pin_24dp,
            pinLabel,
        ) {
            dispatch(Action.PIN)
        }.root.isVisible = showSubscriptionActions

        bindRow(
            view.findViewById(R.id.act_delete),
            R.drawable.ic_delete_24dp,
            R.string.dialog_config_group_actions_delete,
        ) {
            dispatch(Action.DELETE)
        }.root.isVisible = showSubscriptionActions
    }

    private data class RowBinding(val root: View)

    private fun bindRow(
        root: View,
        iconRes: Int,
        labelRes: Int,
        onClick: () -> Unit,
    ): RowBinding {
        root.findViewById<ImageView>(R.id.iv_action_icon).setImageResource(iconRes)
        root.findViewById<TextView>(R.id.tv_action_label).setText(labelRes)
        root.setOnClickListener {
            onClick()
            dismiss()
        }
        return RowBinding(root)
    }

    private fun dispatch(action: Action) {
        listener?.onGroupAction(subscriptionId, action)
    }

    companion object {
        private const val ARG_SUBSCRIPTION_ID = "subscription_id"
        private const val ARG_PINNED = "pinned"
        private const val ARG_SHOW_SUBSCRIPTION_ACTIONS = "show_subscription_actions"

        const val TAG = "HomeConfigGroupActionsBottomSheet"

        fun newInstance(
            subscriptionId: String,
            pinned: Boolean,
            showSubscriptionActions: Boolean,
        ): HomeConfigGroupActionsBottomSheet =
            HomeConfigGroupActionsBottomSheet().apply {
                arguments = bundleOf(
                    ARG_SUBSCRIPTION_ID to subscriptionId,
                    ARG_PINNED to pinned,
                    ARG_SHOW_SUBSCRIPTION_ACTIONS to showSubscriptionActions,
                )
            }
    }
}