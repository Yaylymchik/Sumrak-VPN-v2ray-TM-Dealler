package com.v2ray.ang.ui

import android.animation.ValueAnimator
import android.content.Context
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.RectF
import android.util.AttributeSet
import android.view.View
import android.view.animation.LinearInterpolator
import androidx.core.content.ContextCompat
import androidx.core.graphics.drawable.DrawableCompat
import com.v2ray.ang.R
import kotlin.math.min

/**
 * Happ-style circular connect control: ring + classic power symbol (stroke).
 */
class HappPowerButtonView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    enum class PowerState { IDLE, CONNECTING, CONNECTED }

    var powerState: PowerState = PowerState.IDLE
        set(value) {
            if (field == value) return
            field = value
            ringAnimator?.cancel()
            ringAnimator = if (value == PowerState.CONNECTING) {
                ValueAnimator.ofFloat(0f, 360f).apply {
                    duration = 1800
                    repeatCount = ValueAnimator.INFINITE
                    interpolator = LinearInterpolator()
                    addUpdateListener {
                        ringRotation = it.animatedValue as Float
                        invalidate()
                    }
                    start()
                }
            } else {
                ringRotation = 0f
                null
            }
            pulseAnimator?.cancel()
            if (value == PowerState.CONNECTED) {
                pulseAnimator = ValueAnimator.ofFloat(0.97f, 1.03f, 0.97f).apply {
                    duration = 1600
                    repeatCount = ValueAnimator.INFINITE
                    addUpdateListener {
                        pulseScale = it.animatedValue as Float
                        invalidate()
                    }
                    start()
                }
            } else {
                pulseScale = 1f
                pulseAnimator = null
            }
            invalidate()
        }

    private var ringRotation = 0f
    private var pulseScale = 1f
    private var ringAnimator: ValueAnimator? = null
    private var pulseAnimator: ValueAnimator? = null

    private val fillPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val ringPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
    }
    private val spinPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeCap = Paint.Cap.ROUND
    }
    private val arcBounds = RectF()
    private val powerIcon = ContextCompat.getDrawable(context, R.drawable.ic_happ_power_symbol)!!.mutate()

    init {
        setWillNotDraw(false)
    }

    override fun onDetachedFromWindow() {
        ringAnimator?.cancel()
        pulseAnimator?.cancel()
        super.onDetachedFromWindow()
    }

    override fun onDraw(canvas: Canvas) {
        val d = resources.displayMetrics.density
        val cx = width / 2f
        val cy = height / 2f
        val outerR = min(width, height) * 0.44f * pulseScale

        val accent = ContextCompat.getColor(context, R.color.happ_accent)
        val surface = ContextCompat.getColor(context, R.color.happ_power_fill_idle)
        val deep = ContextCompat.getColor(context, R.color.happ_text_primary)
        when (powerState) {
            PowerState.CONNECTED -> fillPaint.color = accent
            PowerState.CONNECTING -> fillPaint.color = surface
            PowerState.IDLE -> fillPaint.color = surface
        }

        canvas.drawCircle(cx, cy, outerR, fillPaint)

        if (powerState == PowerState.CONNECTING) {
            spinPaint.strokeWidth = 4.5f * d
            spinPaint.color = accent
            canvas.save()
            canvas.rotate(ringRotation, cx, cy)
            arcBounds.set(cx - outerR, cy - outerR, cx + outerR, cy + outerR)
            canvas.drawArc(arcBounds, -90f, 90f, false, spinPaint)
            canvas.restore()
        }

        val iconColor = when (powerState) {
            PowerState.CONNECTED -> deep
            else -> accent
        }
        DrawableCompat.setTint(powerIcon, iconColor)
        val iconHalf = outerR * 0.42f
        val left = (cx - iconHalf).toInt()
        val top = (cy - iconHalf).toInt()
        val right = (cx + iconHalf).toInt()
        val bottom = (cy + iconHalf).toInt()
        powerIcon.setBounds(left, top, right, bottom)
        powerIcon.draw(canvas)
    }
}