package com.v2ray.ang.util.npvs

import android.content.Context
import java.util.zip.Inflater

/**
 * Gen2 AppKey KDF tables and white-box mix (NPVS v5). Ported from npvs-decoder.
 */
internal object NpvsGen2Math {

    private val SHIFT_ORDER = intArrayOf(0, 5, 10, 15, 4, 9, 14, 3, 8, 13, 2, 7, 12, 1, 6, 11)
    private var tables: ByteArray? = null

    private fun m32(v: Int): Int = (v.toLong() and 0xFFFFFFFFL).toInt()

    fun ensureTables(context: Context): ByteArray {
        tables?.let { return it }
        val compressed = context.assets.open("npv/gen2_tables.bin.z").use { it.readBytes() }
        val inflater = Inflater()
        inflater.setInput(compressed)
        val out = ByteArray(13 * 57344 + 4096)
        val n = inflater.inflate(out)
        inflater.end()
        require(n == out.size) { "gen2 tables size mismatch: $n" }
        tables = out
        return out
    }

    fun gen2A16(tables: ByteArray, salt: ByteArray): ByteArray {
        val state = IntArray(16) { salt[it].toInt() and 0xFF }
        for (b in 0 until 13) {
            val block = tables.copyOfRange(b * 57344, (b + 1) * 57344)
            val reordered = IntArray(16) { state[SHIFT_ORDER[it]] }
            System.arraycopy(reordered, 0, state, 0, 16)
            for (j in 0 until 4) {
                gen2Group(state, block, j)
            }
        }
        val page = tables.copyOfRange(13 * 57344, tables.size)
        return ByteArray(16) { i ->
            page[0x100 * i + state[SHIFT_ORDER[i]]].toByte()
        }
    }

    private fun gen2Be32(block: ByteArray, off: Int): Int {
        return ((block[off].toInt() and 0xFF) shl 24) or
            ((block[off + 1].toInt() and 0xFF) shl 16) or
            ((block[off + 2].toInt() and 0xFF) shl 8) or
            (block[off + 3].toInt() and 0xFF)
    }

    private fun gen2Group(state: IntArray, block: ByteArray, j: Int) {
        val base = 4 * j
        val s = intArrayOf(state[base], state[base + 1], state[base + 2], state[base + 3])
        val base1 = 0x6000 + 0x1000 * j
        val args1 = intArrayOf(
            gen2Be32(block, base1 + 4 * s[0]),
            gen2Be32(block, base1 + 0x400 + 4 * s[1]),
            gen2Be32(block, base1 + 0x800 + 4 * s[2]),
            gen2Be32(block, base1 + 0xC00 + 4 * s[3]),
        )
        val w1 = gen2Sub7dc0(block, j, args1[0], args1[1], args1[2], args1[3])
        for (k in 0 until 4) state[base + k] = w1[k]

        val base2 = 0xA000 + 0x1000 * j
        val args2 = intArrayOf(
            gen2Be32(block, base2 + 4 * w1[0]),
            gen2Be32(block, base2 + 0x400 + 4 * w1[1]),
            gen2Be32(block, base2 + 0x800 + 4 * w1[2]),
            gen2Be32(block, base2 + 0xC00 + 4 * w1[3]),
        )
        val w2 = gen2Sub7dc0(block, j, args2[0], args2[1], args2[2], args2[3])
        for (k in 0 until 4) state[base + k] = w2[k]
    }

    private fun gen2Sub7dc0(block: ByteArray, i: Int, x3In: Int, x4In: Int, x5In: Int, x6In: Int): IntArray {
        var x2 = i
        var x3 = m32(x3In)
        var x4 = m32(x4In)
        var x5 = m32(x5In)
        var x6 = m32(x6In)
        var x8 = m32(x2 + m32((x2 shl 1)))
        var x12 = m32((x3 ushr 0x14))
        var x11 = m32((x3 ushr 0x18))
        var x13 = ((x4 ushr 0x18)) and 0xF
        var x14 = m32((x5 ushr 0x18))
        var x10 = x6
        var x9 = m32((x8 shl 3))
        x8 = x4
        x12 = x12 and 0xF0
        x12 = x12 + x13
        x13 = m32((x5 ushr 0x14))
        var x16 = (x8 ushr 0x1C)
        x9 = m32(x9)
        x11 = x11 and 0xF0
        x14 = x14 and 0xF0
        var x15 = m32((x6 ushr 0x18)) and 0xF
        x13 = x13 and 0xF0
        var x17 = m32((x3 ushr 0x10))
        x9 = x9 shl 8
        x8 = x8 and 0xF
        x13 = x13 + x15
        x17 = x17 and 0xF0
        x11 = x9 + x11
        x14 = x9 + x14
        x12 = x9 + x12
        x16 = block[(x11 + x16).toInt()].toInt() and 0xFF
        x14 = x14 + (x10 ushr 28)
        x12 = block[(x12 + 0x200).toInt()].toInt() and 0xFF
        x13 = x9 + x13
        var x0 = m32((x6 ushr 0x14)) and 0xF
        x14 = block[(x14 + 0x100).toInt()].toInt() and 0xFF
        x15 = x9 + ((x16 shl 4))
        x13 = block[(x13 + 0x300).toInt()].toInt() and 0xFF
        x12 = x9 + ((x12 shl 4))
        x16 = m32((x4 ushr 0x14)) and 0xF
        x10 = x10 and 0xF
        x14 = x15 + x14
        x15 = m32((x5 ushr 0x10))
        x12 = x12 + x13
        x13 = m32((x3 ushr 0xC))
        x14 = block[(x14 + 0x400).toInt()].toInt() and 0xFF
        x12 = block[(x12 + 0x500).toInt()].toInt() and 0xFF
        x15 = x15 and 0xF0
        x16 = x17 + x16
        x15 = x15 + x0
        x0 = m32((x4 ushr 0x10)) and 0xF
        x17 = m32((x5 ushr 0xC))
        x12 = x12 or ((x14 shl 4))
        x13 = x13 and 0xF0
        x14 = m32((x6 ushr 0x10)) and 0xF
        x13 = x13 + x0
        x17 = x17 and 0xF0
        x16 = x9 + x16
        val out0 = ((x12 and 0xFF)) and 0xFF
        x13 = x9 + x13
        x12 = x17 + x14
        x14 = block[(x16 + 0x600).toInt()].toInt() and 0xFF
        x13 = block[(x13 + 0x800).toInt()].toInt() and 0xFF
        x15 = x9 + x15
        x12 = x9 + x12
        x15 = block[(x15 + 0x700).toInt()].toInt() and 0xFF
        x16 = m32((x5 ushr 8))
        x12 = block[(x12 + 0x900).toInt()].toInt() and 0xFF
        x14 = x9 + ((x14 shl 4))
        x13 = x9 + ((x13 shl 4))
        x0 = m32((x6 ushr 0xC)) and 0xF
        x17 = m32((x3 ushr 8))
        x14 = x14 + x15
        x12 = x13 + x12
        x13 = (x16 and 0xF0)
        x14 = block[(x14 + 0xA00).toInt()].toInt() and 0xFF
        x12 = block[(x12 + 0xB00).toInt()].toInt() and 0xFF
        x15 = m32((x3 ushr 4))
        x13 = x13 + x0
        x0 = m32((x4 ushr 8)) and 0xF
        x16 = m32((x4 ushr 0xC)) and 0xF
        x12 = x12 or ((x14 shl 4))
        x14 = (x15 and 0xF0)
        x17 = x17 and 0xF0
        x14 = x14 + x0
        x15 = x9 + 0xC00
        x0 = m32((x5 ushr 4))
        x17 = x15 + x17
        val out1 = ((x12 and 0xFF)) and 0xFF
        x12 = m32((x6 ushr 8)) and 0xF
        x16 = block[(x17 + x16).toInt()].toInt() and 0xFF
        x14 = x9 + x14
        x17 = (x0 and 0xF0)
        x14 = block[(x14 + 0xE00).toInt()].toInt() and 0xFF
        x12 = x17 + x12
        x13 = x15 + x13
        x12 = x9 + x12
        x13 = block[(x13 + 0x100).toInt()].toInt() and 0xFF
        x15 = x9 + ((x16 shl 4))
        x12 = block[(x12 + 0xF00).toInt()].toInt() and 0xFF
        x14 = x9 + ((x14 shl 4))
        x16 = 0x1000
        x13 = x15 + x13
        x17 = m32((x6 ushr 4)) and 0xF
        x0 = (x5 and 0xF0)
        x12 = x14 + x12
        x14 = 0x1100
        x13 = block[(x13 + x16).toInt()].toInt() and 0xFF
        x12 = block[(x12 + x14).toInt()].toInt() and 0xFF
        x14 = 0x1200
        x15 = m32((x4 ushr 4)) and 0xF
        x16 = (x3 and 0xF0)
        x17 = x0 + x17
        x14 = x9 + x14
        x12 = x12 or ((x13 shl 4))
        x13 = ((x3 and 0xF)) shl 4
        x16 = x14 + x16
        val out2 = ((x12 and 0xFF)) and 0xFF
        x8 = x13 + x8
        x12 = x14 + x17
        x14 = ((x5 and 0xF)) shl 4
        x13 = block[(x16 + x15).toInt()].toInt() and 0xFF
        x8 = x9 + x8
        x15 = 0x1400
        x12 = block[(x12 + 0x100).toInt()].toInt() and 0xFF
        x8 = block[(x8 + x15).toInt()].toInt() and 0xFF
        x10 = x14 + x10
        x14 = 0x1500
        x10 = x9 + x10
        x13 = x9 + ((x13 shl 4))
        x10 = block[(x10 + x14).toInt()].toInt() and 0xFF
        x8 = x9 + ((x8 shl 4))
        x9 = x13 + x12
        x12 = 0x1600
        x8 = x8 + x10
        x10 = 0x1700
        x9 = block[(x9 + x12).toInt()].toInt() and 0xFF
        x8 = block[(x8 + x10).toInt()].toInt() and 0xFF
        x8 = x8 or ((x9 shl 4))
        val out3 = ((x8 and 0xFF)) and 0xFF
        return intArrayOf(out0, out1, out2, out3)
    }
}
