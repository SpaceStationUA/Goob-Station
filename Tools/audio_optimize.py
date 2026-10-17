#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""
Vorbis budget checker and optimiser for Resources/Audio.

The audio tree used to ship 884 MB of ogg at an average of 152 kbps, with a
small number of long music and ambience files carrying the bulk of it. Running
this at q4 brought that down to 511 MB with no audible damage that survived
measurement.

This tool enforces that budget going forward.

Policy
------
A file is a violation when it is:
  * a tracked .ogg under Resources/Audio
  * at least --min-duration seconds long (short SFX are never worth a second
    lossy generation, and there are thousands of them)
  * encoded at --min-bitrate kbps or above (below that q4 inflates rather than
    shrinks, so re-encoding is not even possible without losing quality)
  * and re-encoding to q4 would free at least --min-saving bytes

Every threshold is overridable, and Tools/audio_allowlist.txt exempts files by
path for cases that genuinely need a higher rate.

Usage
-----
    python3 Tools/audio_optimize.py                 # audit the whole tree
    python3 Tools/audio_optimize.py --changed       # audit what a PR touched
    python3 Tools/audio_optimize.py --fix           # re-encode in place
    python3 Tools/audio_optimize.py --help

Exit code is 1 when violations are present, so CI fails on a bad commit.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
AUDIO_ROOT = REPO_ROOT / "Resources" / "Audio"
ALLOWLIST = REPO_ROOT / "Tools" / "audio_allowlist.txt"

# q4 on libvorbis lands near 160 kbps stereo / 80 kbps mono.
QUALITY = 4

# Durations below this are left alone. 2674 shorter SFX/voice files total 81 MB
# and re-encoding them recovers under 30 MB, which does not justify putting a
# second lossy generation on every sound effect in the game.
MIN_DURATION = 20.0

# Below this bitrate q4 produces a *larger* file, so there is nothing to gain.
MIN_BITRATE = 160.0

# A violation must be worth acting on. Anything that frees less than this is
# reported as noise and ignored.
MIN_SAVING = 256 * 1024

# Encoded duration must match the source this closely or the output is rejected.
DURATION_TOLERANCE = 0.05

MIB = 1024 * 1024


@dataclass
class Probe:
    duration: float
    bitrate: float
    channels: int
    sample_rate: int
    codec: str

    @property
    def layout(self) -> str:
        return f"{self.channels}ch/{self.sample_rate}Hz"


@dataclass
class Result:
    rel: str
    path: Path
    size_before: int
    size_after: int
    before: Probe
    after: Probe
    status: str  # "ok" | "skip" | "error"
    detail: str = ""

    @property
    def saving(self) -> int:
        return self.size_before - self.size_after


def log(msg: str) -> None:
    print(msg, flush=True)


def git(*args: str, check: bool = False) -> str:
    proc = subprocess.run(["git", *args], cwd=REPO_ROOT, capture_output=True, text=True)
    if check and proc.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {proc.stderr.strip()}")
    return proc.stdout


def preflight() -> list[str]:
    """The external tools we shell out to. Checked once, up front, so a missing
    one is a clear message instead of a traceback from inside a worker thread."""
    missing = []
    for tool, hint in (("ffprobe", "ffmpeg"), ("ffmpeg", "ffmpeg"), ("oggenc", "vorbis-tools")):
        if shutil.which(tool) is None:
            missing.append(f"{tool} (from package: {hint})")
    return missing


def annotate(level: str, rel: str, title: str, message: str) -> None:
    """Emit a GitHub Actions annotation, and a readable line everywhere else."""
    safe = message.replace("\n", " ").replace("%", "%%").replace("::", ":")
    print(f"::{level} file={rel},title={title}::{safe}")


def probe(path: Path) -> Probe | None:
    proc = subprocess.run(
        ["ffprobe", "-v", "error",
         "-show_entries", "format=duration,bit_rate",
         "-show_entries", "stream=codec_name,channels,sample_rate",
         "-of", "json", str(path)],
        capture_output=True, text=True,
    )
    if proc.returncode != 0:
        return None
    try:
        data = json.loads(proc.stdout)
        stream = data["streams"][0]
        return Probe(
            duration=float(data["format"]["duration"]),
            bitrate=int(data["format"]["bit_rate"]) / 1000.0,
            channels=int(stream["channels"]),
            sample_rate=int(stream["sample_rate"]),
            codec=stream["codec_name"],
        )
    except (KeyError, IndexError, ValueError):
        return None


def allowlisted() -> set[str]:
    if not ALLOWLIST.exists():
        return set()
    out = set()
    for line in ALLOWLIST.read_text(encoding="utf-8").splitlines():
        line = line.split("#", 1)[0].strip()
        if line:
            out.add(line.replace("\\", "/"))
    return out


def candidate_files(mode: str, base: str) -> list[str]:
    """Paths under Resources/Audio to consider, repo-relative."""
    if mode == "changed":
        # Two-dot on purpose: diffs base against the working tree, so this covers
        # committed work, staged work and loose files alike. CI checks out the PR
        # head, so the two forms agree there; locally they do not.
        names = git("diff", "--name-only", "--diff-filter=AMR", base, check=True)
    else:
        names = git("ls-files", "Resources/Audio", check=True)
    skip = allowlisted()
    return [
        n for n in names.split()
        if n.lower().endswith(".ogg") and n not in skip
    ]


def encode(src: Path, dest: Path, info: Probe) -> None:
    """src -> 16-bit PCM -> oggenc. libvorbis, the reference encoder."""
    ffmpeg = [
        "ffmpeg", "-v", "error", "-nostdin", "-i", str(src),
        "-c:a", "pcm_s16le", "-f", "s16le", "-ar", str(info.sample_rate),
        "-ac", str(info.channels), "-",
    ]
    oggenc = [
        "oggenc", "-q", str(QUALITY), "-B", "16",
        "-C", str(info.channels), "-R", str(info.sample_rate),
        "-o", str(dest), "-",
    ]
    with open(os.devnull, "wb") as null:
        head = subprocess.Popen(ffmpeg, stdout=subprocess.PIPE, stderr=null)
        tail = subprocess.Popen(oggenc, stdin=head.stdout, stdout=null, stderr=null)
        assert head.stdout is not None
        head.stdout.close()
        tail.communicate()
        head.wait()
    if head.returncode != 0:
        raise RuntimeError("ffmpeg decode failed")
    if tail.returncode != 0:
        raise RuntimeError("oggenc failed")


def process(rel: str, workdir: Path, apply: bool) -> Result:
    path = REPO_ROOT / rel
    before = probe(path)
    if before is None:
        return Result(rel, path, 0, 0, Probe(0, 0, 0, 0, "?"), Probe(0, 0, 0, 0, "?"),
                      "error", "ffprobe could not read the file")
    size_before = path.stat().st_size

    if before.codec != "vorbis":
        return Result(rel, path, size_before, size_before, before, before, "skip",
                      f"{before.codec}, not vorbis")
    if before.duration < MIN_DURATION:
        return Result(rel, path, size_before, size_before, before, before, "skip", "shorter than threshold")
    if before.bitrate < MIN_BITRATE:
        return Result(rel, path, size_before, size_before, before, before, "skip", "under bitrate threshold")

    dest = workdir / rel
    dest.parent.mkdir(parents=True, exist_ok=True)
    try:
        encode(path, dest, before)
    except Exception as exc:  # noqa: BLE001 - report, never abort the run
        dest.unlink(missing_ok=True)
        return Result(rel, path, size_before, size_before, before, before, "error", str(exc))

    after = probe(dest)
    if after is None:
        dest.unlink(missing_ok=True)
        return Result(rel, path, size_before, size_before, before, before, "error", "output unreadable")
    if (after.channels, after.sample_rate) != (before.channels, before.sample_rate):
        dest.unlink(missing_ok=True)
        return Result(rel, path, size_before, size_before, before, after, "error",
                      f"layout changed {before.layout} -> {after.layout}")
    if abs(after.duration - before.duration) > DURATION_TOLERANCE:
        dest.unlink(missing_ok=True)
        return Result(rel, path, size_before, size_before, before, after, "error",
                      f"duration drifted {before.duration:.3f}s -> {after.duration:.3f}s")

    size_after = dest.stat().st_size
    if size_after >= size_before:
        dest.unlink(missing_ok=True)
        return Result(rel, path, size_before, size_before, before, after, "skip",
                      "q4 would not be smaller")

    if apply:
        os.replace(dest, path)
    else:
        dest.unlink(missing_ok=True)

    return Result(rel, path, size_before, size_after, before, after, "ok")


def main() -> int:
    global MIN_DURATION, MIN_BITRATE, MIN_SAVING
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--fix", action="store_true",
                        help="re-encode violators in place instead of only reporting them")
    parser.add_argument("--changed", action="store_true",
                        help="only consider files touched by this branch, not the whole tree")
    parser.add_argument("--base", default="origin/master",
                        help="base ref for --changed (default: origin/master)")
    parser.add_argument("--min-duration", type=float, default=MIN_DURATION)
    parser.add_argument("--min-bitrate", type=float, default=MIN_BITRATE)
    parser.add_argument("--min-saving", type=int, default=MIN_SAVING)
    parser.add_argument("--jobs", type=int, default=min(8, (os.cpu_count() or 2)))
    args = parser.parse_args()

    MIN_DURATION, MIN_BITRATE, MIN_SAVING = args.min_duration, args.min_bitrate, args.min_saving

    missing = preflight()
    if missing:
        log("error: missing required tools: " + ", ".join(missing))
        return 2

    if not AUDIO_ROOT.exists():
        log("Resources/Audio not found; nothing to do.")
        return 0

    try:
        files = candidate_files("changed" if args.changed else "all", args.base)
    except RuntimeError as exc:
        log(f"error: {exc}")
        return 2

    log(f"policy: q{QUALITY}, >= {MIN_DURATION:g}s, >= {MIN_BITRATE:g} kbps, "
        f"must save >= {MIN_SAVING // 1024} KiB")
    log(f"scanning {len(files)} .ogg files under Resources/Audio "
        f"({'changed only' if args.changed else 'whole tree'}, {args.jobs} jobs)\n")

    with tempfile.TemporaryDirectory(prefix="audio-opt-") as tmp:
        with ThreadPoolExecutor(max_workers=args.jobs) as pool:
            results = list(pool.map(lambda rel: process(rel, Path(tmp), args.fix), files))

    violators = [r for r in results if r.status == "ok"]
    errors = [r for r in results if r.status == "error"]
    skipped = [r for r in results if r.status == "skip"]

    log(f"checked {len(results)}   violations {len(violators)}   "
        f"skipped {len(skipped)}   errors {len(errors)}\n")

    if violators:
        log(f"{'file':<62}{'before':>9}{'after':>9}{'saved':>9}{'kbps':>13}")
        log("-" * 102)
        for r in sorted(violators, key=lambda r: -r.saving):
            log(f"{r.rel.replace('Resources/Audio/', ''):<62}"
                f"{r.size_before / MIB:>8.2f}M{r.size_after / MIB:>8.2f}M"
                f"{r.saving / MIB:>8.2f}M"
                f"{r.before.bitrate:>6.0f}->{r.after.bitrate:<6.0f}")
        total = sum(r.saving for r in violators)
        log("-" * 102)
        log(f"{len(violators)} files, {total / MIB:.1f} MB wasted"
            f"{'' if args.fix else '  (run with --fix to reclaim it)'}\n")

    for r in violators:
        annotate("error", r.rel, "Audio over Vorbis budget",
                 f"{r.before.bitrate:.0f} kbps for {r.before.duration:.0f}s; "
                 f"q{QUALITY} would give {r.after.bitrate:.0f} kbps and save "
                 f"{r.saving / MIB:.2f} MB. Run: python3 Tools/audio_optimize.py --fix")
    for r in errors:
        annotate("error", r.rel, "Audio could not be processed", r.detail)

    if errors:
        log(f"{len(errors)} file(s) could not be processed; see annotations above.")
    if violators and args.fix:
        log("done: all violations re-encoded in place.")
        return 0
    return 1 if violators or errors else 0


if __name__ == "__main__":
    sys.exit(main())