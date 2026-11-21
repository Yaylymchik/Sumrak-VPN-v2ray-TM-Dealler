import os, sqlite3, glob, json, collections

appdata = os.environ["APPDATA"]
db = os.path.join(appdata, "Sumrak VPN", "sumrax.db")
con = sqlite3.connect(f"file:{db}?mode=ro", uri=True)
cur = con.cursor()

rows = list(cur.execute("select id, name, payload from servers"))
print("rows:", len(rows))

sample = json.loads(rows[0][2])
print("payload keys:", sorted(sample.keys()))

bysub = collections.Counter()
names = collections.defaultdict(list)
for rid, name, payload in rows:
    try:
        p = json.loads(payload)
    except Exception:
        bysub["<bad-json>"] += 1
        continue
    sub = p.get("subscriptionUrl") or p.get("SubscriptionUrl") or "<none>"
    bysub[sub] += 1
    names[sub].append(p.get("name") or name or "")

print()
for sub, n in bysub.most_common():
    host = sub
    if sub.startswith("http"):
        from urllib.parse import urlparse
        u = urlparse(sub)
        host = f"{u.scheme}://{u.hostname}/... (len={len(sub)})"
    print(f"sub={host}  count={n}")
    for nm in names[sub][:6]:
        print("    ", nm[:60])

# column types / any timestamps?
print()
print("other cols types:", [c[2] for c in cur.execute("pragma table_info('servers')")])
con.close()
