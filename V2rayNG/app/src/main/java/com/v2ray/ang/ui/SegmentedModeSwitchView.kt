package com.v2ray.ang.ui

import android.animation.ValueAnimator
import android.content.Context
import android.util.AttributeSet
import android.view.Gravity
import android.view.View
import android.widget.FrameLayout
import android.widget.TextView
import androidx.core.content.ContextCompat
import com.v2ray.ang.R

/**
 * Proxy / VPN segmented switch matching SumraX SegmentedModeSwitch.xaml.
 */
class SegmentedModeSwitchView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : FrameLayout(context, attrs) {

    var isVpnMode: Boolean = true
        set(value) {
            if (field == value) return
            field = value
            animateHighlight(value)
            updateLabelColors()
        }

    var onModeChanged: ((Boolean) -> Unit)? = null

    private val highlight: View
    private val proxyLabel: TextView
    private val vpnLabel: TextView
    private val segmentWidth: Int

    init {
        val density = resources.displayMetrics.density
        segmentWidth = (88f * density).toInt()
        val segmentHeight = (32f * density).toInt()
        val padding = (3f * density).toInt()

        background = ContextCompat.getDrawable(context, R.drawable.bg_segmented_track)
        setPadding(padding, padding, padding, padding)

        highlight = View(context).apply {
            background = ContextCompat.getDrawable(context, R.drawable.bg_segmented_highlight)
            layoutParams = LayoutParams(segmentWidth, segmentHeight)
        }
        addView(highlight)

        proxyLabel = TextView(context).apply {
            layoutParams = LayoutParams(segmentWidth, segmentHeight).apply {
                gravity = Gravity.START
            }
            gravity = Gravity.CENTER
            text = context.getString(R.string.sumrax_mode_proxy)
            textSize = 13f
            setTypeface(typeface, android.graphics.Typeface.BOLD)
            setOnClickListener {
                if (!isVpnMode) return@setOnClickListener
                isVpnMode = false
                onModeChanged?.invoke(false)
            }
        }
        addView(proxyLabel)

        vpnLabel = TextView(context).apply {
            layoutParams = LayoutParams(segmentWidth, segmentHeight).apply {
                gravity = Gravity.END
            }
            gravity = Gravity.CENTER
            text = context.getString(R.string.sumrax_mode_vpn)
            textSize = 13f
            setTypeface(typeface, android.graphics.Typeface.BOLD)
            setOnClickListener {
                if (isVpnMode) return@setOnClickListener
                isVpnMode = true
                onModeChanged?.invoke(true)
            }
        }
        addView(vpnLabel)

        minimumWidth = segmentWidth * 2 + padding * 2

        post {
            highlight.translationX = if (isVpnMode) segmentWidth.toFloat() else 0f
            updateLabelColors()
        }
    }

    private fun animateHighlight(vpnSelected: Boolean) {
        val target = if (vpnSelected) segmentWidth.toFloat() else 0f
        ValueAnimator.ofFloat(highlight.translationX, target).apply {
            duration = 160
            addUpdateListener { highlight.translationX = it.animatedValue as Float }
            start()
        }
    }

    private fun updateLabelColors() {
        val active = ContextCompat.getColor(context, R.color.sumrax_success)
        val inactive = ContextCompat.getColor(context, R.color.sumrax_text_tertiary)
        proxyLabel.setTextColor(if (isVpnMode) inactive else active)
        vpnLabel.setTextColor(if (isVpnMode) active else inactive)
    }
}
