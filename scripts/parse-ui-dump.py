import re
import sys

path = sys.argv[1]
s = open(path, encoding="utf-8").read()
keys = ("tv_home", "tv_days", "btn_home", "chip_home", "search_view", "rv_home", "fab", "layout_days")
for m in re.finditer(
    r'text="([^"]*)"[^>]*resource-id="com\.sumrax\.app:id/([^"]+)"', s
):
    rid = m.group(2)
    if any(k in rid for k in keys):
        text = m.group(1).replace("\n", " | ")
        print(f"{rid}: {text}")