#!/bin/bash
# Pirate: apply the two local dev-only WebView hunks into the RobustToolbox
# submodule AFTER a submodule update.
#
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# This is a MacOS/local-development helper for Daniil. It applies
# WebUIEngineDiff_v277.patch (ModLoader dev-context hunk; the res://
# host re-attach hunk is GONE — content now loads pages with the
# content prefix inside the URL path, e.g. res://webres/_Pirate/...,
# which the stock upstream module resolves as-is).
# default-ALC module resolution) to RobustToolbox. It is NOT needed on
# Windows, Linux, the production launcher, or CI — those load the WebView
# module the standard launcher way. After the next Space Wizards engine
# bump, run this script once to restore dev-run behavior.
#
# Idempotent: if the hunks are already present (git apply exits 0 because
# the patch already landed), it just reports so.

set -euo pipefail
cd "$(dirname "$0")/../.."   # repo root

# ABSOLUTE, and it has to be.
#
# `git -C RobustToolbox` changes git's working directory to the SUBMODULE, so a
# relative patch path is resolved from there -- and the patch lives at the repo root.
# The relative form therefore always failed with "can't open patch", which the script
# reported as "Patch no longer applies cleanly AND no local edits present", i.e. it
# blamed the engine for its own path bug. It could not ever have worked on a checkout
# where the submodule is present.
ENGINE_PATCH="$(cd "$(dirname "$0")/../.." && pwd)/WebUIEngineDiff_v277.patch"

if ! git -C RobustToolbox rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "RobustToolbox is not a git worktree here; nothing to patch." >&2
    exit 1
fi

if [ ! -f "$ENGINE_PATCH" ]; then
    echo "No engine patch at $ENGINE_PATCH." >&2
    echo "  -> it is excluded by .git/info/exclude, so it must be copied in from a" >&2
    echo "     worktree that has it (or reconstructed). Without it the WebView" >&2
    echo "     module is never loaded and every WebViewControl throws" >&2
    echo "     FileNotFoundException at construction." >&2
    exit 3
fi

if git -C RobustToolbox apply --check "$ENGINE_PATCH" 2>/dev/null; then
    git -C RobustToolbox apply "$ENGINE_PATCH"
    echo "Patched RobustToolbox with $ENGINE_PATCH (local dev hunks)."
elif git -C RobustToolbox diff --quiet -- Robust.Client.WebView/Cef/WebViewManagerCef.cs \
     && git -C RobustToolbox diff --quiet -- Robust.Shared/ContentPack/ModLoader.cs; then
    echo "Patch no longer applies cleanly AND no local edits present." >&2
    echo "  -> engine may have changed these files; update $ENGINE_PATCH." >&2
    exit 2
else
    echo "Already patched (hunks present). Nothing to do."
fi
