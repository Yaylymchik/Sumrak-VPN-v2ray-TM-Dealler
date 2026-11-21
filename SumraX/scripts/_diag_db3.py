import os, sqlite3, binascii

appdata = os.environ["APPDATA"]
db = os.path.join(appdata, "Sumrak VPN", "sumrax.db")
con = sqlite3.connect(f"file:{db}?mode=ro", uri=True)
cur = con.cursor()

rows = list(cur.execute("select id, name, payload from servers limit 3"))
for rid, name, payload in rows:
    t = type(payload).__name__
    b = payload if isinstance(payload, (bytes, bytearray)) else str(payload).encode("utf-8", "replace")
    print(f"id={rid!r} name={name!r} type={t} len={len(b)}")
    print("  head:", binascii.hexlify(b[:48]).decode())
    print("  printable:", b[:60])

print()
print("count distinct names:", cur.execute("select count(distinct name) from servers").fetchone()[0])
for r in cur.execute("select name from servers order by id desc limit 12"):
    print("  newest:", (r[0] or "")[:60])
con.close()
