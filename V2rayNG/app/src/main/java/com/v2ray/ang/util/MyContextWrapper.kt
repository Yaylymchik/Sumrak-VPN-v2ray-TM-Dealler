package com.v2ray.ang.util

import android.content.Context
import android.content.ContextWrapper
import android.content.res.Configuration
import android.content.res.Resources
import android.os.LocaleList
import com.v2ray.ang.AppConfig
import com.v2ray.ang.handler.MmkvManager
import java.util.Locale

open class MyContextWrapper(base: Context?) : ContextWrapper(base) {
    companion object {
        private val FONT_SCALES = floatArrayOf(0.85f, 1.0f, 1.2f)

        /**
         * Wraps the context with a new locale and font scale.
         */
        fun wrap(context: Context, newLocale: Locale?): ContextWrapper {
            var mContext = context
            val res: Resources = mContext.resources
            val configuration: Configuration = Configuration(res.configuration)

            val locale = newLocale ?: Locale.getDefault()
            configuration.setLocale(locale)
            val localeList = LocaleList(locale)
            LocaleList.setDefault(localeList)
            configuration.setLocales(localeList)

            val fontSizeIndex = MmkvManager.decodeSettingsString(AppConfig.PREF_FONT_SIZE, "1")?.toIntOrNull() ?: 1
            configuration.fontScale = FONT_SCALES.getOrElse(fontSizeIndex) { 1.0f }

            mContext = mContext.createConfigurationContext(configuration)
            return ContextWrapper(mContext)
        }
    }
}