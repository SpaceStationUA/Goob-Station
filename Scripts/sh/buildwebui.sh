#!/bin/sh
# Build the Solid WebUI interfaces into game resources.
#
#   Scripts/sh/buildwebui.sh            -> build every interface (final assets)
#   Scripts/sh/buildwebui.sh dev Radio  -> run vite dev server for HMR
#
# Phase B note: while the radio page is still the hand-built
# Resources/_Pirate/WebUI/Radio/index.html, its built output goes to the
# staging dir Content.Pirate.Client/_Pirate/WebUI/dist_resources (set
# BUILDWEBUI_FINAL=1 to emit into Resources once the real port lands).
#
# Each interface lives in Content.Pirate.Client/_Pirate/WebUI/Ui/src/<Iface>
# and is emitted to Resources/_Pirate/WebUI/<Iface>/ (same layout the resource
# packager picks up; see bridge docstring in Ui/src/lib/bridge.ts for the
# transport contract).
set -eu

here=$(cd "$(dirname "$0")" && pwd)
repo=$(dirname "$(dirname "$here")")
ui="$repo/Content.Pirate.Client/_Pirate/WebUI/Ui"
res="$repo/Resources/_Pirate/WebUI"

cd "$ui"

if [ "${1:-}" = "dev" ]; then
    iface=${2:-Radio}
    TUI_IFACE="$iface" exec npx vite --clearScreen false
fi

for iface in Radio ThemePicker Uplink EvidenceBoard; do
    if [ -f "src/$iface/index.html" ]; then
        out="$res/$iface"
        [ "${BUILDWEBUI_FINAL:-}" = "1" ] || out="$ui/dist_resources"
        echo "== buildwebui: $iface -> $out"
        TUI_IFACE="$iface" \
        TUI_OUT_DIR="$out" \
            npx vite build --clearScreen false >/dev/null
        echo "== buildwebui: $iface ok"
    else
        echo "== buildwebui: skip $iface (stub not present)"
    fi
done
