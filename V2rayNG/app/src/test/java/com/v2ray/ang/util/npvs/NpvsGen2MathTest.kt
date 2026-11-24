package com.v2ray.ang.util.npvs

import org.junit.Assert.assertEquals
import org.junit.Test
import java.util.zip.Inflater

class NpvsGen2MathTest {

    @Test
    fun gen2A16_matchesReferenceVector() {
        val compressed = readResource("gen2_tables.bin.z")
        val tables = inflate(compressed)
        val salt = hex("d12bfe4de31926bf0ed42512c2388c11")
        val a16 = NpvsGen2Math.gen2A16(tables, salt)
        assertEquals("8a2dc4658b2ae03e774d32cb02e6ca5c", a16.toHex())
    }

    private fun inflate(compressed: ByteArray): ByteArray {
        val inflater = Inflater()
        inflater.setInput(compressed)
        val out = ByteArray(13 * 57344 + 4096)
        val n = inflater.inflate(out)
        inflater.end()
        assertEquals(out.size, n)
        return out
    }

    private fun readResource(name: String): ByteArray =
        javaClass.classLoader!!.getResourceAsStream(name)!!.readBytes()

    private fun hex(s: String): ByteArray {
        val out = ByteArray(s.length / 2)
        for (i in out.indices) {
            out[i] = s.substring(i * 2, i * 2 + 2).toInt(16).toByte()
        }
        return out
    }

    private fun ByteArray.toHex(): String = joinToString("") { "%02x".format(it) }
}