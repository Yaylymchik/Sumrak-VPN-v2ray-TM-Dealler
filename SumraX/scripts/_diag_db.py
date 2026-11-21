import os, sqlite3, glob, sys

appdata = os.environ["APPDATA"]
cands = glob.glob(os.path.join(appdata, "Sumrak VPN", "*.db"))
print("db files:", [os.path.basename(c) for c in cands])
if not cands:
    sys.exit(1)

db = max(cands, key=os.path.getsize)
print("using:", os.path.basename(db), os.path.getsize(db), "bytes")

con = sqlite3.connect(f"file:{db}?mode=ro", uri=True)
cur = con.cursor()
tables = [r[0] for r in cur.execute("select name from sqlite_master where type='table'")]
print("tables:", tables)
for t in tables:
    cols = [c[1] for c in cur.execute(f"pragma table_info('{t}')")]
    n = cur.execute(f"select count(*) from '{t}'").fetchone()[0]
    print(f"  {t}: rows={n}")
    print(f"     cols={cols}")
con.close()
