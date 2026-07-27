package com.v2ray.ang.ui

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import androidx.lifecycle.lifecycleScope
import com.v2ray.ang.AppConfig
import com.v2ray.ang.R
import com.v2ray.ang.databinding.ActivityLogcatBinding
import com.v2ray.ang.extension.toast
import com.v2ray.ang.extension.toastError
import com.v2ray.ang.handler.AngConfigManager
import com.v2ray.ang.util.LogUtil
import com.v2ray.ang.util.encrypt.EncryptedCryptResolver
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.net.URLDecoder

class UrlSchemeActivity : BaseActivity() {
    private val binding by lazy { ActivityLogcatBinding.inflate(layoutInflater) }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(binding.root)

        lifecycleScope.launch {
            try {
                when (intent?.action) {
                    Intent.ACTION_SEND -> {
                        if ("text/plain" == intent.type) {
                            intent.getStringExtra(Intent.EXTRA_TEXT)?.let { parseUri(it, null) }
                        }
                    }

                    Intent.ACTION_VIEW -> {
                        val data = intent.data
                        when {
                            data != null && isDirectEncryptedDeeplink(data) -> {
                                parseUri(deeplinkToImportString(data), data.fragment)
                            }

                            data?.host == "install-config" || data?.host == "install-sub" -> {
                                val shareUrl = data.getQueryParameter("url").orEmpty()
                                parseUri(shareUrl, data.fragment)
                            }

                            else -> toastError(R.string.toast_failure)
                        }
                    }
                }
            } catch (e: Exception) {
                LogUtil.e(AppConfig.TAG, "Error processing URL scheme", e)
                toastError(R.string.toast_failure)
            } finally {
                startActivity(Intent(this@UrlSchemeActivity, MainActivity::class.java))
                finish()
            }
        }
    }

    private fun isDirectEncryptedDeeplink(uri: Uri): Boolean {
        val raw = deeplinkToImportString(uri)
        if (EncryptedCryptResolver.isEncryptedDeeplink(raw)) return true
        val host = uri.host?.lowercase().orEmpty()
        return host in CRYPT_HOSTS
    }

    /** Rebuilds happ://crypt5/{payload} without Uri.toString() mangling + or /. */
    private fun deeplinkToImportString(uri: Uri): String {
        val host = uri.host?.lowercase().orEmpty()
        if (host in CRYPT_HOSTS) {
            val scheme = uri.scheme?.lowercase().orEmpty().ifBlank { "happ" }
            val payload = uri.encodedPath?.trimStart('/').orEmpty()
            return "$scheme://$host/$payload"
        }
        return uri.toString()
    }

    private suspend fun parseUri(uriString: String?, fragment: String?) {
        if (uriString.isNullOrEmpty()) {
            return
        }
        LogUtil.i(AppConfig.TAG, uriString)

        var decodedUrl = runCatching {
            URLDecoder.decode(uriString, Charsets.UTF_8.name())
        }.getOrDefault(uriString)
        decodedUrl = EncryptedCryptResolver.canonicalize(decodedUrl)

        val uri = Uri.parse(decodedUrl)
        if (uri.fragment.isNullOrEmpty() && !fragment.isNullOrEmpty()) {
            decodedUrl += "#$fragment"
        }
        LogUtil.i(AppConfig.TAG, decodedUrl)

        val (count, countSub) = withContext(Dispatchers.IO) {
            AngConfigManager.importBatchConfig(decodedUrl, "", false)
        }
        when {
            count + countSub > 0 -> toast(R.string.import_subscription_success)
            EncryptedCryptResolver.isEncryptedDeeplink(decodedUrl) ->
                toastError(R.string.toast_encrypted_import_failed)
            else -> toast(R.string.import_subscription_failure)
        }
    }

    companion object {
        private val CRYPT_HOSTS = setOf("crypt", "crypt2", "crypt3", "crypt4", "crypt5")
    }
}
