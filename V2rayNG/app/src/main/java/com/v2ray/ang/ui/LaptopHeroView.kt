package com.v2ray.ang.ui

import android.animation.ValueAnimator
import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.DashPathEffect
import android.graphics.Paint
import android.graphics.Path
import android.graphics.RectF
import android.graphics.Typeface
import android.util.AttributeSet
import android.view.View
import android.view.animation.DecelerateInterpolator
import android.view.animation.LinearInterpolator
import androidx.core.content.ContextCompat
import com.v2ray.ang.R
import kotlin.math.min

/**
 * SumraX laptop hero — tap to connect; shows BLOCK SERVICE on screen when connected.
 */
class LaptopHeroView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    enum class HeroState { IDLE, CONNECTING, CONNECTED }

    var heroState: HeroState = HeroState.IDLE
        set(value) {
            val old = field
            field = value
            ringAnimator?.cancel()
            blockServiceAnimator?.cancel()
            ringAnimator = if (value == HeroState.CONNECTING) {
                ValueAnimator.ofFloat(0f, 360f).apply {
                    duration = 2400
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
            if (value == HeroState.CONNECTED && old != HeroState.CONNECTED) {
                blockServiceScale = 0.72f
                blockServiceAlpha = 0f
                blockServiceAnimator = ValueAnimator.ofFloat(0f, 1f).apply {
                    duration = 520
                    interpolator = DecelerateInterpolator()
                    addUpdateListener {
                        val t = it.animatedValue as Float
                        blockServiceAlpha = t
                        blockServiceScale = 0.72f + 0.28f * t
                        invalidate()
                    }
                    start()
                }
            } else if (value != HeroState.CONNECTED) {
                blockServiceAlpha = 0f
                blockServiceScale = 1f
            }
            invalidate()
        }

    private var ringRotation = 0f
    private var blockServiceAlpha = 0f
    private var blockServiceScale = 1f
    private var ringAnimator: ValueAnimator? = null
    private var blockServiceAnimator: ValueAnimator? = null

    private val greenRingPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeWidth = 3f * resources.displayMetrics.density
    }
    private val blueRingPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeWidth = 2f * resources.displayMetrics.density
        pathEffect = DashPathEffect(floatArrayOf(12f, 10f), 0f)
    }
    private val laptopBodyPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val laptopScreenPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val laptopDetailPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val blockBloomPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val blockLabelPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val ovalRect = RectF()

    init {
        setWillNotDraw(false)
        blockBloomPaint.typeface = Typeface.create(Typeface.MONOSPACE, Typeface.BOLD)
        blockBloomPaint.textAlign = Paint.Align.CENTER
        blockLabelPaint.typeface = Typeface.create(Typeface.MONOSPACE, Typeface.BOLD)
        blockLabelPaint.textAlign = Paint.Align.CENTER
    }

    override fun onDetachedFromWindow() {
        ringAnimator?.cancel()
        blockServiceAnimator?.cancel()
        super.onDetachedFromWindow()
    }

    override fun onDraw(canvas: Canvas) {
        val cx = width / 2f
        val cy = height / 2f
        val r = min(width, height) * 0.46f

        val green = when (heroState) {
            HeroState.CONNECTED -> ContextCompat.getColor(context, R.color.sumrax_success)
            HeroState.CONNECTING -> ContextCompat.getColor(context, R.color.sumrax_accent)
            HeroState.IDLE -> ContextCompat.getColor(context, R.color.sumrax_success)
        }
        val blue = ContextCompat.getColor(context, R.color.sumrax_accent)

        greenRingPaint.color = green
        blueRingPaint.color = blue

        ovalRect.set(cx - r, cy - r, cx + r, cy + r)
        canvas.drawOval(ovalRect, greenRingPaint)

        canvas.save()
        canvas.rotate(ringRotation, cx, cy)
        ovalRect.set(cx - r * 1.12f, cy - r * 1.12f, cx + r * 1.12f, cy + r * 1.12f)
        canvas.drawOval(ovalRect, blueRingPaint)
        canvas.restore()

        drawLaptop(canvas, cx, cy, r * 0.55f)
    }

    private fun drawLaptop(canvas: Canvas, cx: Float, cy: Float, size: Float) {
        val bodyColor = ContextCompat.getColor(context, R.color.sumrax_hero_body)
        val screenColor = if (heroState == HeroState.CONNECTED) {
            ContextCompat.getColor(context, R.color.sumrax_hero_screen_on)
        } else {
            ContextCompat.getColor(context, R.color.sumrax_hero_screen)
        }
        val bezelColor = ContextCompat.getColor(context, R.color.sumrax_hero_bezel)
        val accent = ContextCompat.getColor(context, R.color.sumrax_accent)

        laptopBodyPaint.color = bodyColor
        laptopScreenPaint.color = screenColor

        val screenW = size * 1.5f
        val screenH = size * 0.95f
        val baseH = size * 0.18f
        val left = cx - screenW / 2f
        val top = cy - screenH / 2f - baseH * 0.3f

        val screenRect = RectF(left, top, left + screenW, top + screenH)
        canvas.drawRoundRect(screenRect, size * 0.08f, size * 0.08f, laptopBodyPaint)

        val inner = RectF(
            left + size * 0.08f,
            top + size * 0.08f,
            left + screenW - size * 0.08f,
            top + screenH - size * 0.08f
        )
        laptopBodyPaint.color = bezelColor
        canvas.drawRoundRect(inner, size * 0.05f, size * 0.05f, laptopBodyPaint)

        val display = RectF(
            inner.left + size * 0.06f,
            inner.top + size * 0.06f,
            inner.right - size * 0.06f,
            inner.bottom - size * 0.06f
        )
        laptopScreenPaint.color = screenColor
        canvas.drawRoundRect(display, size * 0.04f, size * 0.04f, laptopScreenPaint)

        when (heroState) {
            HeroState.CONNECTED -> drawBlockService(canvas, display, size)
            else -> {
                laptopDetailPaint.color = accent
                val blockW = (display.width() - size * 0.2f) / 2f
                canvas.drawRect(
                    display.left + size * 0.08f, display.top + size * 0.2f,
                    display.left + size * 0.08f + blockW, display.bottom - size * 0.2f, laptopDetailPaint
                )
                canvas.drawRect(
                    display.right - size * 0.08f - blockW, display.top + size * 0.2f,
                    display.right - size * 0.08f, display.bottom - size * 0.2f, laptopDetailPaint
                )
            }
        }

        val baseTop = top + screenH - size * 0.04f
        val baseRect = RectF(cx - screenW * 0.55f, baseTop, cx + screenW * 0.55f, baseTop + baseH)
        laptopBodyPaint.color = bodyColor
        val basePath = Path().apply {
            addRoundRect(baseRect, size * 0.06f, size * 0.06f, Path.Direction.CW)
        }
        canvas.drawPath(basePath, laptopBodyPaint)

        laptopBodyPaint.color = ContextCompat.getColor(context, R.color.sumrax_hero_hinge)
        canvas.drawRect(
            cx - screenW * 0.12f,
            baseTop + baseH * 0.35f,
            cx + screenW * 0.12f,
            baseTop + baseH * 0.55f,
            laptopBodyPaint
        )
    }

    private fun drawBlockService(canvas: Canvas, display: RectF, size: Float) {
        if (blockServiceAlpha <= 0f) return

        val textSize = size * 0.19f
        val lineGap = size * 0.24f
        val centerX = display.centerX()
        val centerY = display.centerY()
        val alpha = (blockServiceAlpha * 255f).toInt().coerceIn(0, 255)

        blockBloomPaint.textSize = textSize
        blockBloomPaint.color = Color.parseColor("#55E8FC")
        blockBloomPaint.alpha = (alpha * 0.75f).toInt()

        blockLabelPaint.textSize = textSize
        blockLabelPaint.color = Color.parseColor("#E8FCFF")
        blockLabelPaint.alpha = alpha
        blockLabelPaint.setShadowLayer(10f * resources.displayMetrics.density, 0f, 0f, Color.parseColor("#E8FCFF"))

        canvas.save()
        canvas.scale(blockServiceScale, blockServiceScale, centerX, centerY)
        canvas.drawText("BLOCK", centerX, centerY - lineGap * 0.2f, blockBloomPaint)
        canvas.drawText("SERVICE", centerX, centerY + lineGap * 0.9f, blockBloomPaint)
        canvas.drawText("BLOCK", centerX, centerY - lineGap * 0.2f, blockLabelPaint)
        canvas.drawText("SERVICE", centerX, centerY + lineGap * 0.9f, blockLabelPaint)
        canvas.restore()

        blockLabelPaint.clearShadowLayer()
    }
}
