package com.v2ray.ang.ui

import android.content.Context
import android.util.AttributeSet
import android.widget.FrameLayout
import android.widget.TextView
import androidx.core.view.isVisible
import com.v2ray.ang.R
import java.util.Locale

/**
 * Square “wristwatch” style remaining-days indicator for the home screen.
 */
class WatchDaysRemainingView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = 0,
) : FrameLayout(context, attrs, defStyleAttr) {

    private val valueView: TextView

    init {
        inflate(context, R.layout.layout_watch_days_remaining, this)
        valueView = findViewById(R.id.tv_watch_days_value)
        isVisible = false
    }

    fun setRemainingDays(days: Int?) {
        if (days == null || days < 0) {
            isVisible = false
            return
        }
        valueView.text = String.format(Locale.US, "%03d", days.coerceIn(0, 999))
        isVisible = true
    }
}