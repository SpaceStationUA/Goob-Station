import json

c = json.load(open(r"C:\Users\Admin\AppData\Local\Temp\pr845_rc.json", encoding="utf-8"))
out = []
for x in c:
    if x["user"]["login"] == "CyberLanos":
        out.append(
            f"--- {x.get('path')} line {x.get('line') or x.get('original_line')}\n{x['body']}\n"
        )
ic = json.load(open(r"C:\Users\Admin\AppData\Local\Temp\pr845_ic.json", encoding="utf-8"))
for x in ic:
    if x["user"]["login"] in ("CyberLanos", "v0idRift"):
        out.append(f"ISSUE {x['user']['login']}:\n{x['body']}\n")
path = r"C:\Users\Admin\AppData\Local\Temp\pr845_summary.txt"
open(path, "w", encoding="utf-8").write("\n".join(out))
print("wrote", len(out), "items")
