package com.v2ray.ang.util

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import androidx.core.graphics.drawable.IconCompat
import com.v2ray.ang.R

object SumraXNotificationIcons {
    fun smallIcon(context: Context): IconCompat =
        IconCompat.createWithResource(context, R.mipmap.ic_launcher)

    fun largeIcon(context: Context): Bitmap? =
        BitmapFactory.decodeResource(context.resources, R.mipmap.ic_launcher_foreground)
}
