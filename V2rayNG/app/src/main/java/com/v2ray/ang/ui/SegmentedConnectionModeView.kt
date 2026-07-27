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
 * Smart / Manual connection mode switch (Hiddify-style).
 */
class SegmentedConnectionModeView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : FrameLayout(context, attrs) {

    var isSmartMode: Boolean = true
        set(value) {
            if (field == value) return
            field = value
            animateHighlight(value)
            updateLabelColors()
        }

    var onModeChanged: ((Boolean) -> Unit)? = null

    private val highlight: View
    private val smartLabel: TextView
    private val manualLabel: TextView
    private val segmentWidth: Int

    init {
        val density = resources.displayMetrics.density
        segmentWidth = (96f * density).toInt()
        val segmentHeight = (32f * density).toInt()
        val padding = (3f * density).toInt()

        background = ContextCompat.getDrawable(context, R.drawable.bg_segmented_track)
        setPadding(padding, padding, padding, padding)

        highlight = View(context).apply {
            background = ContextCompat.getDrawable(context, R.drawable.bg_segmented_highlight)
            layoutParams = LayoutParams(segmentWidth, segmentHeight)
        }
        addView(highlight)

        smartLabel = TextView(context).apply {
            layoutParams = LayoutParams(segmentWidth, segmentHeight).apply {
                gravity = Gravity.START
            }
            gravity = Gravity.CENTER
            text = context.getString(R.string.sumrax_connection_mode_smart)
            textSize = 13f
            setTypeface(typeface, android.graphics.Typeface.BOLD)
            setOnClickListener {
                if (isSmartMode) return@setOnClickListener
                isSmartMode = true
                onModeChanged?.invoke(true)
            }
        }
        addView(smartLabel)

        manualLabel = TextView(context).apply {
            layoutParams = LayoutParams(segmentWidth, segmentHeight).apply {
                gravity = Gravity.END
            }
            gravity = Gravity.CENTER
            text = context.getString(R.string.sumrax_connection_mode_manual)
            textSize = 13f
            setTypeface(typeface, android.graphics.Typeface.BOLD)
            setOnClickListener {
                if (!isSmartMode) return@setOnClickListener
                isSmartMode = false
                onModeChanged?.invoke(false)
            }
        }
        addView(manualLabel)

        minimumWidth = segmentWidth * 2 + padding * 2

        post {
            highlight.translationX = if (isSmartMode) 0f else segmentWidth.toFloat()
            updateLabelColors()
        }
    }

    private fun animateHighlight(smartSelected: Boolean) {
        val target = if (smartSelected) 0f else segmentWidth.toFloat()
        ValueAnimator.ofFloat(highlight.translationX, target).apply {
            duration = 160
            addUpdateListener { highlight.translationX = it.animatedValue as Float }
            start()
        }
    }

    private fun updateLabelColors() {
        val active = ContextCompat.getColor(context, R.color.sumrax_success)
        val inactive = ContextCompat.getColor(context, R.color.sumrax_text_tertiary)
        smartLabel.setTextColor(if (isSmartMode) active else inactive)
        manualLabel.setTextColor(if (isSmartMode) inactive else active)
    }
}
