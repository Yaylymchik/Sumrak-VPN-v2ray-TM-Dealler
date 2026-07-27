package com.v2ray.ang.ui

import android.animation.ValueAnimator
import android.content.Context
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.RectF
import android.util.AttributeSet
import android.view.View
import android.view.animation.DecelerateInterpolator
import androidx.core.content.ContextCompat
import com.v2ray.ang.R
import kotlin.math.cos
import kotlin.math.min
import kotlin.math.sin

class SpeedometerGaugeView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = 0
) : View(context, attrs, defStyleAttr) {

    private val arcPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeCap = Paint.Cap.ROUND
        strokeWidth = 10f
    }

    private val progressPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeCap = Paint.Cap.ROUND
        strokeWidth = 10f
    }

    private val needlePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
    }

    private val textPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        textAlign = Paint.Align.CENTER
        textSize = 34f
        isFakeBoldText = true
    }

    private val labelPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        textAlign = Paint.Align.CENTER
        textSize = 13f
    }

    private val arcRect = RectF()
    private var animatedSpeedMbps = 0f
    private var targetSpeedMbps = 0f
    private var speedAnimator: ValueAnimator? = null

    private val startAngle = 135f
    private val sweepAngle = 270f

    init {
        applyThemeColors()
    }

    private fun applyThemeColors() {
        arcPaint.color = ContextCompat.getColor(context, R.color.sumrax_glass_stroke)
        progressPaint.color = ContextCompat.getColor(context, R.color.sumrax_accent)
        needlePaint.color = ContextCompat.getColor(context, R.color.sumrax_accent)
        textPaint.color = ContextCompat.getColor(context, R.color.sumrax_text_primary)
        labelPaint.color = ContextCompat.getColor(context, R.color.sumrax_text_secondary)
    }

    fun setSpeedMbps(speedMbps: Float, animate: Boolean = true) {
        targetSpeedMbps = speedMbps.coerceAtLeast(0f)
        speedAnimator?.cancel()
        if (!animate) {
            animatedSpeedMbps = targetSpeedMbps
            invalidate()
            return
        }
        speedAnimator = ValueAnimator.ofFloat(animatedSpeedMbps, targetSpeedMbps).apply {
            duration = 600
            interpolator = DecelerateInterpolator()
            addUpdateListener {
                animatedSpeedMbps = it.animatedValue as Float
                invalidate()
            }
            start()
        }
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val size = min(width, height).toFloat()
        val cx = width / 2f
        val cy = height / 2f + size * 0.08f
        val radius = size * 0.38f

        arcRect.set(cx - radius, cy - radius, cx + radius, cy + radius)

        canvas.drawArc(arcRect, startAngle, sweepAngle, false, arcPaint)

        val maxSpeed = 100f
        val progress = (animatedSpeedMbps / maxSpeed).coerceIn(0f, 1f)
        canvas.drawArc(arcRect, startAngle, sweepAngle * progress, false, progressPaint)

        val needleAngle = Math.toRadians((startAngle + sweepAngle * progress).toDouble())
        val needleLen = radius * 0.75f
        val nx = cx + (needleLen * cos(needleAngle)).toFloat()
        val ny = cy + (needleLen * sin(needleAngle)).toFloat()
        canvas.drawCircle(cx, cy, 6f, needlePaint)
        canvas.drawLine(cx, cy, nx, ny, needlePaint.apply { strokeWidth = 4f })

        val speedText = if (animatedSpeedMbps < 0.1f) "0" else String.format("%.1f", animatedSpeedMbps)
        canvas.drawText(speedText, cx, cy + 8f, textPaint)
        canvas.drawText(context.getString(R.string.speedometer_mbps), cx, cy + 28f, labelPaint)

        drawTickMarks(canvas, cx, cy, radius)
    }

    private fun drawTickMarks(canvas: Canvas, cx: Float, cy: Float, radius: Float) {
        val tickPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = ContextCompat.getColor(context, R.color.sumrax_text_tertiary)
            strokeWidth = 2f
        }
        for (i in 0..10) {
            val fraction = i / 10f
            val angle = Math.toRadians((startAngle + sweepAngle * fraction).toDouble())
            val inner = radius - 18f
            val outer = radius - 8f
            canvas.drawLine(
                cx + (inner * cos(angle)).toFloat(),
                cy + (inner * sin(angle)).toFloat(),
                cx + (outer * cos(angle)).toFloat(),
                cy + (outer * sin(angle)).toFloat(),
                tickPaint
            )
        }
    }

    override fun onDetachedFromWindow() {
        speedAnimator?.cancel()
        super.onDetachedFromWindow()
    }
}
