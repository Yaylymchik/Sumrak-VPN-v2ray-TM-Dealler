import os, sqlite3, json

appdata = os.environ["APPDATA"]
db = os.path.join(appdata, "Sumrak VPN", "sumrax.db")
con = sqlite3.connect(f"file:{db}?mode=ro", uri=True)
rows = [{"id": r[0], "name": r[1], "payload": r[2]} for r in con.execute("select id,name,payload from servers")]
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_dump.json")
with open(out, "w", encoding="utf-8") as f:
    json.dump(rows, f, ensure_ascii=False)
print("dumped", len(rows), "rows ->", out)
con.close()
