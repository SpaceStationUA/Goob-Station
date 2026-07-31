import json
import urllib.request

sha = "eca3910f4ae92ec46c367ef1169f8112c3e3c61a"  # yautja-01-mob; will also check others
shas = {
    "01": "eca3910f4ae92ec46c367ef1169f8112c3e3c61a",
    "02": "2c7d1ffa79750125d597c4d4c56a96c2afecdba6",
    "03": "61766a9c8a0605adc4a2637da7d92766ecb50355",
    "07": "75884df5b71eac7fd219fd3f1b510992b566dfd4",
}

for label, sha in shas.items():
    url = f"https://api.github.com/repos/SpaceStationUA/Goob-Station/commits/{sha}/check-runs?per_page=50"
    req = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json", "User-Agent": "cursor"})
    d = json.load(urllib.request.urlopen(req))
    print(f"\n=== PR slice {label} ===")
    for cr in d.get("check_runs", []):
        if "YAML" in cr["name"] or cr["conclusion"] == "failure":
            print(f"  {cr['conclusion'] or cr['status']:12} {cr['name']} id={cr['id']}")
            if cr["name"] == "YAML Linter" and cr.get("conclusion") == "failure":
                aurl = f"https://api.github.com/repos/SpaceStationUA/Goob-Station/check-runs/{cr['id']}/annotations"
                anns = json.load(urllib.request.urlopen(urllib.request.Request(aurl, headers={"Accept": "application/vnd.github+json", "User-Agent": "cursor"})))
                for a in anns:
                    if a.get("annotation_level") == "failure":
                        print("   FAIL:", (a.get("message") or "")[:500])
