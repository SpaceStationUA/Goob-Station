# Split Yautja content into stacked branches. Run from repo root.
# Usage: python Tools/_split_yautja_prs.py

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

SOURCE = "bcd3ae60b084b0d29bd118484bf403753bc4d85f"
UA_MASTER = "FETCH_HEAD"  # after fetch SpaceStationUA master
REMOTE = "origin"

# Stacked branches: each based on previous
SLICES = [
    {
        "branch": "yautja-01-mob",
        "title": "Yautja 1/7: mob, species, names, factions",
        "files": [
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Mobs/mobs.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Species/body.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Species/damage.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Species/markings.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Species/reagents.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Species/species.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Names/names.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Shared/factions.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Audio/emotes.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Audio/audio.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Interface/status_icons.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/attributions.txt",
            "Resources/Locale/en-US/_Oskarrr/Yautja/names.ftl",
            "Resources/Locale/en-US/_Oskarrr/Yautja/reagents.ftl",
            "Resources/Locale/uk-UA/_Oskarrr/Yautja/names.ftl",
            "Resources/Locale/uk-UA/_Oskarrr/Yautja/reagents.ftl",
        ],
        "globs": [
            "Resources/Audio/_Oskarrr/Yautja/Voice/**",
        ],
    },
    {
        "branch": "yautja-02-mask",
        "title": "Yautja 2/7: masks",
        "files": [
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Equipment/masks.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Recipes/Lathes/yautja_masks_pred.yml",
            # MaskBase + vision live in devices.yml — included here; bracer entities come along (OK for stack)
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Equipment/devices.yml",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/pred_vision.wav",
        ],
        "globs": [],
    },
    {
        "branch": "yautja-03-bracer-cloak",
        "title": "Yautja 3/7: bracer and cloak",
        "files": [
            "Content.Goobstation.Shared/Yautja/YautjaBracerComponent.cs",
            "Content.Goobstation.Shared/Yautja/SharedYautjaBracerSystem.cs",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Actions/actions.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/YautjaDisappear/YautjaDisappear.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Equipment/gear.yml",
            "Resources/Locale/en-US/_Oskarrr/Yautja/bracer.ftl",
            "Resources/Locale/uk-UA/_Oskarrr/Yautja/bracer.ftl",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/pred_attach.wav",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/pred_cloakon.wav",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/pred_cloakoff.wav",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/pred_countdown.ogg",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/self_destruct_doafter.wav",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/predator_cloak_warning_01.wav",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/predator_cloak_warning_02.wav",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/predator_cloak_warning_03.wav",
        ],
        "globs": [],
    },
    {
        "branch": "yautja-04-armor",
        "title": "Yautja 4/7: armor",
        "files": [
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Equipment/armor.yml",
        ],
        "globs": [],
    },
    {
        "branch": "yautja-05-structures",
        "title": "Yautja 5/7: structures and techfab",
        "files": [
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Structures/structures.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Structures/lathe.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Structures/tiles.yml.off",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Recipes/Lathes/categories.yml",
            "Resources/Locale/en-US/_Oskarrr/Yautja/lathe.ftl",
            "Resources/Locale/uk-UA/_Oskarrr/Yautja/lathe.ftl",
            ".github/workflows/yaml-linter.yml",
        ],
        "globs": [],
    },
    {
        "branch": "yautja-06-weapons",
        "title": "Yautja 6/7: weapons, plasma, traps, smart disc",
        "files": [
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Equipment/weapons.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Equipment/plasma_projectiles.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Equipment/trophies_traps.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Equipment/items.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Effects/effects.yml",
            "Content.Goobstation.Shared/Yautja/YautjaHuntingTrapComponent.cs",
            "Content.Goobstation.Shared/Yautja/YautjaHuntingTrapSystem.cs",
            "Content.Goobstation.Shared/Yautja/YautjaSmartDiscComponent.cs",
            "Content.Goobstation.Shared/Yautja/YautjaSmartDiscSystem.cs",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Recipes/Lathes/yautja.yml",
        ],
        "globs": [
            "Resources/Audio/_Oskarrr/Yautja/Weapons/**",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/HealthShard/**",
            "Resources/Audio/_Oskarrr/Yautja/Equipment/pred_translator.ogg",
        ],
    },
    {
        "branch": "yautja-07-jobs",
        "title": "Yautja 7/7: jobs, bad blood, lathe packs, attributions audio",
        "files": [
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Roles/jobs.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/BadBlood/threat.yml",
            "Resources/Prototypes/_Pirate/_Oskarrr/Yautja/Recipes/Lathes/Packs/yautja.yml",
            "Resources/Audio/_Oskarrr/Yautja/attributions.yml",
        ],
        "globs": [],
    },
]


def run(cmd: list[str], check: bool = True) -> subprocess.CompletedProcess:
    print("+", " ".join(cmd))
    return subprocess.run(cmd, check=check, text=True, encoding="utf-8", errors="replace")


def git_output(cmd: list[str]) -> str:
    return subprocess.check_output(cmd, text=True, encoding="utf-8", errors="replace").strip()


def expand_globs(patterns: list[str]) -> list[str]:
    out: list[str] = []
    for pat in patterns:
        # Use git ls-tree from SOURCE for tracked paths
        # Convert ** glob to git path list via Python pathlib against working tree at SOURCE via git ls-tree
        pass
    # Simpler: ls-tree and filter
    all_files = git_output(["git", "ls-tree", "-r", "--name-only", SOURCE]).splitlines()
    for pat in patterns:
        if pat.endswith("/**"):
            prefix = pat[:-3]
            out.extend(f for f in all_files if f.startswith(prefix) and Path(f).suffix.lower() in {".wav", ".ogg", ".yml"})
        else:
            if pat in all_files:
                out.append(pat)
    return out


def main() -> int:
    run(["git", "fetch", "https://github.com/SpaceStationUA/Goob-Station.git", "master"])
    base = git_output(["git", "rev-parse", "FETCH_HEAD"])
    print("UA master", base)
    print("SOURCE", SOURCE)

    prev = base
    for i, sl in enumerate(SLICES):
        branch = sl["branch"]
        files = list(sl["files"])
        files.extend(expand_globs(sl.get("globs") or []))
        # unique preserve order
        seen = set()
        uniq = []
        for f in files:
            if f not in seen:
                seen.add(f)
                uniq.append(f)
        files = uniq

        missing = []
        for f in files:
            r = subprocess.run(["git", "cat-file", "-e", f"{SOURCE}:{f}"], capture_output=True)
            if r.returncode != 0:
                missing.append(f)
        if missing:
            print("MISSING in SOURCE:", *missing, sep="\n  ")
            # drop missing
            files = [f for f in files if f not in missing]

        print(f"\n=== {branch} ({len(files)} files) ===")
        run(["git", "checkout", "-B", branch, prev])
        if files:
            run(["git", "checkout", SOURCE, "--", *files])
            run(["git", "add", "--", *files])
            # only commit if staged
            staged = git_output(["git", "diff", "--cached", "--name-only"])
            if not staged:
                print("nothing staged, skip commit")
            else:
                msg = sl["title"]
                run(["git", "commit", "-m", msg])
        prev = git_output(["git", "rev-parse", "HEAD"])
        print("HEAD", prev)

    print("\nDone. Push with:")
    for sl in SLICES:
        print(f"  git push -u {REMOTE} {sl['branch']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
