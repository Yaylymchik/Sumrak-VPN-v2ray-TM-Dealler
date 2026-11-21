package com.v2ray.ang.util

/**
 * Short labels for home / picker, not full subscription remarks.
 */
object ServerDisplayName {

    fun fromProfile(remarks: String?, server: String?): String {
        val line = remarks?.lineSequence()?.firstOrNull { it.isNotBlank() }?.trim().orEmpty()
        if (line.isEmpty()) return shortenHost(server)

        val parts = line.split('|').map { it.trim() }.filter { it.isNotEmpty() }
        parts.firstOrNull { candidateCountryOrAlias(it) }?.let { return it.take(40) }
        parts.firstOrNull { !looksLikeTechnical(it) }?.let { return it.take(40) }

        return line.take(40)
    }

    private fun candidateCountryOrAlias(part: String): Boolean {
        if (part.length > 36) return false
        if (looksLikeTechnical(part)) return false
        if (part.matches(Regex("""^[\p{So}\p{Emoji}\p{Extended_Pictographic}]+\s*\p{L}{1,20}.*"""))) return true
        if (part.length <= 12 && !part.contains('.')) return true
        return false
    }

    private fun looksLikeTechnical(part: String): Boolean {
        val lower = part.lowercase()
        return lower.contains("://") ||
            lower.contains(".workers.") ||
            lower.contains("railway") ||
            lower.contains("@") ||
            lower.startsWith("vless") ||
            lower.startsWith("vmess") ||
            lower.startsWith("trojan")
    }

    private fun shortenHost(server: String?): String {
        val s = server?.trim().orEmpty()
        if (s.isEmpty()) return ""
        return s.substringBefore(':').take(32)
    }
}