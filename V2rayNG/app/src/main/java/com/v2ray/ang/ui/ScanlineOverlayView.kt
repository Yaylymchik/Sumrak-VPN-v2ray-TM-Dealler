package com.v2ray.ang.ui

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapShader
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Shader
import android.util.AttributeSet
import android.view.View
import androidx.core.content.ContextCompat
import com.v2ray.ang.R

/**
 * CRT-style scanline overlay matching SumraX ScanlineOverlay (DarkTheme + ScanlineOverlay.xaml).
 * Cyan (#00A8FF) horizontal lines every 4dp at ~4% opacity.
 */
class ScanlineOverlayView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)

    init {
        isClickable = false
        isFocusable = false
        importantForAccessibility = IMPORTANT_FOR_ACCESSIBILITY_NO
        setWillNotDraw(false)

        val density = resources.displayMetrics.density
        val periodPx = (4f * density).toInt().coerceAtLeast(4)
        val linePx = (1f * density).toInt().coerceAtLeast(1)

        val tile = Bitmap.createBitmap(1, periodPx, Bitmap.Config.ARGB_8888)
        Canvas(tile).apply {
            drawColor(0)
            drawRect(
                0f,
                0f,
                1f,
                linePx.toFloat(),
                Paint(Paint.ANTI_ALIAS_FLAG).apply {
                    color = ContextCompat.getColor(context, R.color.sumrax_scanline_tint)
                    style = Paint.Style.FILL
                }
            )
        }

        paint.shader = BitmapShader(tile, Shader.TileMode.REPEAT, Shader.TileMode.REPEAT)
    }

    override fun onDraw(canvas: Canvas) {
        if (width <= 0 || height <= 0) return
        canvas.drawRect(0f, 0f, width.toFloat(), height.toFloat(), paint)
    }
}
