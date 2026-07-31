import json
import re
import urllib.request

req = urllib.request.Request(
    "https://api.github.com/repos/SpaceStationUA/Goob-Station/pulls/780",
    headers={"Accept": "application/vnd.github+json", "User-Agent": "cursor"},
)
body = json.load(urllib.request.urlopen(req)).get("body") or ""

new_cl = (
    ":cl:\n"
    "- add: Додано расу Яутжа з мобом, масками, наручником (кігті/плащ/самознищення), "
    "бронею, структурами/техфабом, зброєю та ролями.\n"
)

if ":cl:" in body:
    body = re.sub(r":cl:\s*\n(?:- .+\n?)*", new_cl, body, count=1)
else:
    body = body.rstrip() + "\n\n" + new_cl

path = r"D:\X-тп\Goob-Station\Tools\_tmp_pr780_body.md"
with open(path, "w", encoding="utf-8") as f:
    f.write(body)
print("ok", path)
print(new_cl)
