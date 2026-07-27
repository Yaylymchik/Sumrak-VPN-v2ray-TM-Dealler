package com.v2ray.ang.ui

import android.os.Bundle
import android.widget.LinearLayout
import android.widget.TextView
import com.v2ray.ang.BuildConfig
import com.v2ray.ang.R

class UrlSchemesActivity : BaseActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentViewWithToolbar(
            R.layout.activity_url_schemes,
            showHomeAsUp = true,
            title = getString(R.string.title_url_schemes)
        )
        val container = findViewById<LinearLayout>(R.id.url_schemes_list)
        val schemes = listOf(
            "sumrax://add/{base64}" to R.string.url_scheme_add_server,
            "sumrax://crypt/{payload}" to R.string.url_scheme_crypt,
            "sumrax://crypt2/{payload}" to R.string.url_scheme_crypt2,
            "sumrax://crypt3/{payload}" to R.string.url_scheme_crypt3,
            "sumrax://crypt4/{payload}" to R.string.url_scheme_crypt4,
            "sumrax://crypt5/{payload}" to R.string.url_scheme_crypt5,
            "sumrax://connect" to R.string.url_scheme_connect,
            "sumrax://disconnect" to R.string.url_scheme_disconnect,
            "sumrax://toggle" to R.string.url_scheme_toggle,
        )
        schemes.forEach { (scheme, titleRes) ->
            val title = TextView(this).apply {
                text = getString(titleRes)
                setTextColor(getColor(R.color.sumrax_text_secondary))
                textSize = 12f
                setPadding(0, 24, 0, 4)
            }
            val value = TextView(this).apply {
                text = scheme
                setTextColor(getColor(R.color.sumrax_text_primary))
                textSize = 14f
                setTextIsSelectable(true)
                setPadding(0, 0, 0, 8)
            }
            container.addView(title)
            container.addView(value)
        }
        findViewById<TextView>(R.id.tv_url_schemes_note).text =
            getString(R.string.url_schemes_note, BuildConfig.APPLICATION_ID)
    }
}
