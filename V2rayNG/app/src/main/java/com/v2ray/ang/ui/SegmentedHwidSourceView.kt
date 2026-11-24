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

/** Device vs custom HWID source (segmented). */
class SegmentedHwidSourceView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : FrameLayout(context, attrs) {

    var isCustomMode: Boolean = false
        set(value) {
            if (field == value) return
            field = value
            animateHighlight(value)
            updateLabelColors()
        }

    var onChanged: ((Boolean) -> Unit)? = null

    private val highlight: View
    private val deviceLabel: TextView
    private val customLabel: TextView
    private val segmentWidth: Int

    init {
        val density = resources.displayMetrics.density
        segmentWidth = (64f * density).toInt()
        val segmentHeight = (32f * density).toInt()
        val padding = (3f * density).toInt()

        background = ContextCompat.getDrawable(context, R.drawable.bg_segmented_track)
        setPadding(padding, padding, padding, padding)

        highlight = View(context).apply {
            background = ContextCompat.getDrawable(context, R.drawable.bg_segmented_highlight)
            layoutParams = LayoutParams(segmentWidth, segmentHeight)
        }
        addView(highlight)

        deviceLabel = TextView(context).apply {
            layoutParams = LayoutParams(segmentWidth, segmentHeight).apply { gravity = Gravity.START }
            gravity = Gravity.CENTER
            text = context.getString(R.string.sumrax_hwid_source_device)
            textSize = 11f
            setTypeface(typeface, android.graphics.Typeface.BOLD)
            setOnClickListener {
                if (!isCustomMode) return@setOnClickListener
                isCustomMode = false
                onChanged?.invoke(false)
            }
        }
        addView(deviceLabel)

        customLabel = TextView(context).apply {
            layoutParams = LayoutParams(segmentWidth, segmentHeight).apply { gravity = Gravity.END }
            gravity = Gravity.CENTER
            text = context.getString(R.string.sumrax_hwid_source_custom)
            textSize = 11f
            setTypeface(typeface, android.graphics.Typeface.BOLD)
            setOnClickListener {
                if (isCustomMode) return@setOnClickListener
                isCustomMode = true
                onChanged?.invoke(true)
            }
        }
        addView(customLabel)

        minimumWidth = segmentWidth * 2 + padding * 2

        post {
            highlight.translationX = if (isCustomMode) segmentWidth.toFloat() else 0f
            updateLabelColors()
        }
    }

    private fun animateHighlight(custom: Boolean) {
        val target = if (custom) segmentWidth.toFloat() else 0f
        ValueAnimator.ofFloat(highlight.translationX, target).apply {
            duration = 160
            addUpdateListener { highlight.translationX = it.animatedValue as Float }
            start()
        }
    }

    private fun updateLabelColors() {
        val active = ContextCompat.getColor(context, R.color.sumrax_accent)
        val inactive = ContextCompat.getColor(context, R.color.sumrax_text_tertiary)
        deviceLabel.setTextColor(if (isCustomMode) inactive else active)
        customLabel.setTextColor(if (isCustomMode) active else inactive)
    }
}