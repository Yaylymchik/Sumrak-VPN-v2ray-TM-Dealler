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

/** Compact Off / On toggle (Happ-style), used for HWID. */
class SegmentedHwidView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : FrameLayout(context, attrs) {

    var isHwidOn: Boolean = true
        set(value) {
            if (field == value) return
            field = value
            animateHighlight(value)
            updateLabelColors()
        }

    var onChanged: ((Boolean) -> Unit)? = null

    private val highlight: View
    private val offLabel: TextView
    private val onLabel: TextView
    private val segmentWidth: Int

    init {
        val density = resources.displayMetrics.density
        segmentWidth = (52f * density).toInt()
        val segmentHeight = (32f * density).toInt()
        val padding = (3f * density).toInt()

        background = ContextCompat.getDrawable(context, R.drawable.bg_segmented_track)
        setPadding(padding, padding, padding, padding)

        highlight = View(context).apply {
            background = ContextCompat.getDrawable(context, R.drawable.bg_segmented_highlight)
            layoutParams = LayoutParams(segmentWidth, segmentHeight)
        }
        addView(highlight)

        offLabel = TextView(context).apply {
            layoutParams = LayoutParams(segmentWidth, segmentHeight).apply { gravity = Gravity.START }
            gravity = Gravity.CENTER
            text = context.getString(R.string.sumrax_hwid_off)
            textSize = 12f
            setTypeface(typeface, android.graphics.Typeface.BOLD)
            setOnClickListener {
                if (!isHwidOn) return@setOnClickListener
                isHwidOn = false
                onChanged?.invoke(false)
            }
        }
        addView(offLabel)

        onLabel = TextView(context).apply {
            layoutParams = LayoutParams(segmentWidth, segmentHeight).apply { gravity = Gravity.END }
            gravity = Gravity.CENTER
            text = context.getString(R.string.sumrax_hwid_on)
            textSize = 12f
            setTypeface(typeface, android.graphics.Typeface.BOLD)
            setOnClickListener {
                if (isHwidOn) return@setOnClickListener
                isHwidOn = true
                onChanged?.invoke(true)
            }
        }
        addView(onLabel)

        minimumWidth = segmentWidth * 2 + padding * 2

        post {
            highlight.translationX = if (isHwidOn) segmentWidth.toFloat() else 0f
            updateLabelColors()
        }
    }

    private fun animateHighlight(on: Boolean) {
        val target = if (on) segmentWidth.toFloat() else 0f
        ValueAnimator.ofFloat(highlight.translationX, target).apply {
            duration = 160
            addUpdateListener { highlight.translationX = it.animatedValue as Float }
            start()
        }
    }

    private fun updateLabelColors() {
        val active = ContextCompat.getColor(context, R.color.sumrax_accent)
        val inactive = ContextCompat.getColor(context, R.color.sumrax_text_tertiary)
        offLabel.setTextColor(if (isHwidOn) inactive else active)
        onLabel.setTextColor(if (isHwidOn) active else inactive)
    }
}