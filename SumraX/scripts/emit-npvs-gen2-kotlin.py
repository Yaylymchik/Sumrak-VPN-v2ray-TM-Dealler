"""Emit NpvsGen2Math.kt gen2Sub7dc0 from Python npvs_decrypter."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PY = ROOT / "tools" / "npvs-decoder" / "src" / "npvt_decrypter" / "npvs_decrypter.py"
OUT = ROOT / "V2rayNG" / "app" / "src" / "main" / "java" / "com" / "v2ray" / "ang" / "util" / "npvs" / "NpvsGen2Math.kt"

SHIFT_ORDER = [0, 5, 10, 15, 4, 9, 14, 3, 8, 13, 2, 7, 12, 1, 6, 11]


def m32_wrap(expr: str) -> str:
    return f"m32({expr})"


def convert_expr(expr: str) -> str:
    expr = expr.strip()
    guard = 0
    while "m32(" in expr and guard < 64:
        expr = re.sub(r"m32\(([^()]*)\)", r"m32(\1)", expr, count=1)
        guard += 1
    expr = re.sub(r"\bat\(([^)]+)\)", r"block[(\1).toInt()].toInt() and 0xFF", expr)
    expr = re.sub(r"(\w+)\s*>>\s*(\w+)", r"(\1 ushr \2)", expr)
    expr = re.sub(r"(\w+)\s*<<\s*(\w+)", r"(\1 shl \2)", expr)
    expr = re.sub(r"(\w+)\s*&\s*(\w+)", r"(\1 and \2)", expr)
    expr = re.sub(r"(\w+)\s*\|\s*(\w+)", r"(\1 or \2)", expr)
    expr = re.sub(r"\b0x([0-9a-fA-F]+)\b", lambda m: f"0x{m.group(1)}u", expr)
    expr = re.sub(r"\b(\d+)\b", lambda m: f"{m.group(1)}u" if int(m.group(1)) > 0x7FFFFFFF else m.group(1), expr)
    expr = expr.replace(" and 0xFFFFFFFFu and ", " and ")
    return expr


seen_vars: set[str] = set()


def convert_line(line: str) -> list[str]:
    line = line.strip()
    if not line or line.startswith("m32 =") or line.startswith("at ="):
        return []
    if line.startswith("return "):
        return ["        return intArrayOf(out0, out1, out2, out3)"]

    if "&=" in line:
        var, _, rhs = line.partition("&=")
        var = var.strip()
        return [polish_line(f"        {var} = {var} and {convert_expr(rhs.strip())}")]
    if "+=" in line:
        var, _, rhs = line.partition("+=")
        var = var.strip()
        return [polish_line(f"        {var} = {var} + {convert_expr(rhs.strip())}")]
    if "|=" in line:
        var, _, rhs = line.partition("|=")
        var = var.strip()
        return [polish_line(f"        {var} = {var} or ({convert_expr(rhs.strip())})")]
    if "<<=" in line:
        var, _, rhs = line.partition("<<=")
        var = var.strip()
        return [polish_line(f"        {var} = {var} shl {convert_expr(rhs.strip())}")]

    if "," in line and "=" in line and line.index(",") < line.index("="):
        lhs, rhs = line.split("=", 1)
        vars_ = [v.strip() for v in lhs.split(",")]
        vals = [v.strip() for v in rhs.split(",")]
        return [f"        var {v} = {convert_expr(vals[i])}" for i, v in enumerate(vars_)]

    if "=" in line:
        var, rhs = line.split("=", 1)
        var = var.strip()
        rhs = convert_expr(rhs.strip())
        rhs = rhs.replace(".toUInt()", "")
        rhs = re.sub(r"\b0x([0-9a-fA-F]+)u\b", r"0x\1", rhs)
        rhs = re.sub(r"\b(\d+)u\b", r"\1", rhs)
        rhs = rhs.replace(" and 0xFFFFFFFFu", " and 0xFFFFFFFF")
        rhs = rhs.replace("ushr", "ushr").replace("shl", "shl")
        if var in ("out0", "out1", "out2", "out3"):
            return [polish_line(f"        val {var} = ({rhs}) and 0xFF")]
        if re.match(r"^x\d+$", var):
            if var in seen_vars:
                return [polish_line(f"        {var} = {rhs}")]
            seen_vars.add(var)
            return [polish_line(f"        var {var} = {rhs}")]
        return [polish_line(f"        {var} = {rhs}")]
    return []


def polish_line(line: str) -> str:
    line = line.replace(" & ", " and ")
    line = line.replace("<<", " shl ")
    line = line.replace(">>", " ushr ")
    line = re.sub(r"0x([0-9a-fA-F]+)u", r"0x\1", line)
    line = line.replace(" and 0xFFFFFFFFu", " and 0xFFFFFFFF")
    line = line.replace(" and 0xFu", " and 0xF")
    line = line.replace(" and 0xF0u", " and 0xF0")
    return line


def extract_sub7dc0_body(text: str) -> list[str]:
    global seen_vars
    seen_vars = set()
    start = None
    for idx, line in enumerate(text.splitlines()):
        if line.strip().startswith("def _gen2_sub7dc0("):
            start = idx + 1
            break
    if start is None:
        raise SystemExit("sub7dc0 not found")
    lines = []
    for raw in text.splitlines()[start:]:
        if raw.strip().startswith("return [out0"):
            break
        if not raw.startswith("        "):
            continue
        raw = raw.strip()
        if raw.startswith("m32") or raw.startswith("at"):
            continue
        if raw.startswith("x2 =") or raw.startswith("x3, x4"):
            continue
        lines.extend(convert_line(raw))
    return lines


def main() -> None:
    text = PY.read_text(encoding="utf-8")
    sub_lines = extract_sub7dc0_body(text)
    shift = ", ".join(str(x) for x in SHIFT_ORDER)
    sub_body = "\n".join(sub_lines)

    kt = f"""package com.v2ray.ang.util.npvs

import android.content.Context
import java.util.zip.Inflater

/**
 * Gen2 AppKey KDF tables and white-box mix (NPVS v5). Ported from npvs-decoder.
 */
internal object NpvsGen2Math {{

    private val SHIFT_ORDER = intArrayOf({shift})
    private var tables: ByteArray? = null

    private fun m32(v: Int): Int = (v.toLong() and 0xFFFFFFFFL).toInt()

    fun ensureTables(context: Context): ByteArray {{
        tables?.let {{ return it }}
        val compressed = context.assets.open("npv/gen2_tables.bin.z").use {{ it.readBytes() }}
        val inflater = Inflater()
        inflater.setInput(compressed)
        val out = ByteArray(13 * 57344 + 4096)
        val n = inflater.inflate(out)
        inflater.end()
        require(n == out.size) {{ "gen2 tables size mismatch: $n" }}
        tables = out
        return out
    }}

    fun gen2A16(tables: ByteArray, salt: ByteArray): ByteArray {{
        val state = IntArray(16) {{ salt[it].toInt() and 0xFF }}
        for (b in 0 until 13) {{
            val block = tables.copyOfRange(b * 57344, (b + 1) * 57344)
            for (i in 0 until 16) {{
                state[i] = state[SHIFT_ORDER[i]]
            }}
            for (j in 0 until 4) {{
                gen2Group(state, block, j)
            }}
        }}
        val page = tables.copyOfRange(13 * 57344, tables.size)
        return ByteArray(16) {{ i ->
            page[0x100 * i + state[SHIFT_ORDER[i]]].toByte()
        }}
    }}

    private fun gen2Be32(block: ByteArray, off: Int): Int {{
        return ((block[off].toInt() and 0xFF) shl 24) or
            ((block[off + 1].toInt() and 0xFF) shl 16) or
            ((block[off + 2].toInt() and 0xFF) shl 8) or
            (block[off + 3].toInt() and 0xFF)
    }}

    private fun gen2Group(state: IntArray, block: ByteArray, j: Int) {{
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
    }}

    private fun gen2Sub7dc0(block: ByteArray, i: Int, x3In: Int, x4In: Int, x5In: Int, x6In: Int): IntArray {{
        var x2 = i
        var x3 = m32(x3In)
        var x4 = m32(x4In)
        var x5 = m32(x5In)
        var x6 = m32(x6In)
{sub_body}
    }}
}}
"""
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(kt, encoding="utf-8")
    print("Wrote", OUT, "lines", len(sub_lines))


if __name__ == "__main__":
    main()