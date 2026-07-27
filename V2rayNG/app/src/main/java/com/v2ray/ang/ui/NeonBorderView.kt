package com.v2ray.ang.ui

import android.content.Context
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.RectF
import android.util.AttributeSet
import android.widget.FrameLayout
import androidx.core.content.ContextCompat
import com.v2ray.ang.R

class NeonBorderView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : FrameLayout(context, attrs) {

    private val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        color = ContextCompat.getColor(context, R.color.sumrax_glass_stroke)
    }
    private val accentPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
        color = ContextCompat.getColor(context, R.color.sumrax_accent)
    }
    private val borderRect = RectF()

    private val cornerRadius = 10f * resources.displayMetrics.density
    private val strokeWidth = 1f * resources.displayMetrics.density
    private val accentHeight = 2f * resources.displayMetrics.density

    init {
        setWillNotDraw(false)
        borderPaint.strokeWidth = strokeWidth
        val pad = (strokeWidth + 2f * resources.displayMetrics.density).toInt()
        setPadding(pad, pad, pad, pad)
    }

    override fun dispatchDraw(canvas: Canvas) {
        super.dispatchDraw(canvas)
        val w = width.toFloat()
        val h = height.toFloat()
        if (w <= 0f || h <= 0f) return

        val inset = strokeWidth / 2f
        borderRect.set(inset, inset, w - inset, h - inset)
        canvas.drawRoundRect(borderRect, cornerRadius, cornerRadius, borderPaint)
        canvas.drawRect(inset, inset, w - inset, inset + accentHeight, accentPaint)
    }
}
