package com.v2ray.ang.util.npvs

import org.json.JSONObject

internal object NpvsV5ShareLinkMapper {

    fun linkFromConfig(cfg: Any?): String? {
        if (cfg !is JSONObject) return null
        val remarks = cfg.optString("name").ifBlank { cfg.optString("remarks", "NPVS") }
        val profile = cfg.optJSONObject("v2rayProfile") ?: return null
        val v2rayJson = profile.opt("v2rayJson")
        if (v2rayJson is JSONObject) {
            val outbounds = v2rayJson.optJSONArray("outbounds") ?: return null
            for (i in 0 until outbounds.length()) {
                val ob = outbounds.optJSONObject(i) ?: continue
                outboundToLink(ob, remarks)?.let { return it }
            }
        }
        return null
    }

  private fun outboundToLink(outbound: JSONObject, remark: String): String? {
        val proto = outbound.optString("protocol", "").lowercase()
        val settings = outbound.optJSONObject("settings") ?: JSONObject()
        val stream = outbound.optJSONObject("streamSettings") ?: JSONObject()
        val network = stream.optString("network", "tcp")
        val security = stream.optString("security", "none")
        val q = LinkedHashMap<String, String>()
        q["type"] = network
        q["security"] = security
        applyStreamExtras(stream, network, security, q)

        return when (proto) {
            "vless" -> {
                val vnext = settings.optJSONArray("vnext")?.optJSONObject(0) ?: return null
                val host = vnext.optString("address") ?: return null
                val port = vnext.optInt("port", 0)
                val user = vnext.optJSONArray("users")?.optJSONObject(0) ?: return null
                val uuid = user.optString("id", "")
                q["encryption"] = "none"
                user.optString("flow").takeIf { it.isNotBlank() }?.let { q["flow"] = it }
                "vless://$uuid@$host:$port?${encodeQuery(q)}#${encodeFrag(remark)}"
            }
            "trojan" -> {
                val srv = settings.optJSONArray("servers")?.optJSONObject(0) ?: return null
                val host = srv.optString("address") ?: return null
                val port = srv.optInt("port", 0)
                val password = srv.optString("password", "")
                "trojan://$password@$host:$port?${encodeQuery(q)}#${encodeFrag(remark)}"
            }
            else -> null
        }
    }

    private fun applyStreamExtras(
        stream: JSONObject,
        network: String,
        security: String,
        q: MutableMap<String, String>,
    ) {
        when (network) {
            "ws", "xhttp", "httpupgrade" -> {
                val set = stream.optJSONObject(network + "Settings")
                    ?: stream.optJSONObject("wsSettings")
                    ?: stream.optJSONObject("xhttpSettings")
                set?.optString("path")?.takeIf { it.isNotBlank() }?.let { q["path"] = it }
                val host = set?.optJSONObject("headers")?.optString("Host")
                    ?: set?.optString("host")
                host?.takeIf { it.isNotBlank() }?.let { q["host"] = it }
                set?.optString("mode")?.takeIf { it.isNotBlank() }?.let { q["mode"] = it }
            }
        }
        if (security == "tls") {
            stream.optJSONObject("tlsSettings")?.let { tls ->
                tls.optString("serverName").takeIf { it.isNotBlank() }?.let { q["sni"] = it }
                tls.optString("fingerprint").takeIf { it.isNotBlank() }?.let { q["fp"] = it }
                if (tls.optBoolean("allowInsecure")) q["allowInsecure"] = "1"
            }
        } else if (security == "reality") {
            stream.optJSONObject("realitySettings")?.let { re ->
                re.optString("serverName").takeIf { it.isNotBlank() }?.let { q["sni"] = it }
                re.optString("fingerprint").takeIf { it.isNotBlank() }?.let { q["fp"] = it }
                re.optString("publicKey").takeIf { it.isNotBlank() }?.let { q["pbk"] = it }
                re.optString("shortId").takeIf { it.isNotBlank() }?.let { q["sid"] = it }
                re.optString("spiderX").takeIf { it.isNotBlank() }?.let { q["spx"] = it }
            }
        }
        stream.opt("finalmask")?.let { fm ->
            if (fm is JSONObject || fm is String) {
                q["finalmask"] = fm.toString()
            }
        }
    }

    private fun encodeQuery(q: Map<String, String>): String =
        q.entries.joinToString("&") { (k, v) -> "$k=${java.net.URLEncoder.encode(v, "UTF-8")}" }

    private fun encodeFrag(s: String): String = java.net.URLEncoder.encode(s, "UTF-8")
}