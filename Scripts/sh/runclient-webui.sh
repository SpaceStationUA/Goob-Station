# SPDX-FileCopyrightText: 2026 Pirate Development Team
# SPDX-License-Identifier: AGPL-3.0-or-later

#!/usr/bin/env bash
# Pirate: build + run the client with the Robust.Client.WebView (CEF) module
# enabled for the WebUI spike.
#
# macOS notes:
#  - CEF natives are NOT shipped by upstream for macos. Framework goes into
#    bin/Content.Client/Chromium Embedded Framework.framework and the
#    robust-native-webview shim is compiled to librobust_native_webview.dylib
#    (see Content.Pirate.Client/_Pirate/WebUI/PLAN.md).
#  - Any engine dll update requires regenerating the .app bundle: the bundle
#    hardlinks binaries, so a rebuilt engine leaves stale copies inside.

set -euo pipefail

cd "$(dirname "$0")/../../"

OS="$(uname -s)"
REPO="$PWD"
BIN="$REPO/bin/Content.Client"
MODULE_DIR="$REPO/bin/modules/Robust.Client.WebView"
CEF_BIN="RobustToolbox/Robust.Client.WebView/bin/Tools/net10.0"

echo "==> building..."
dotnet build Content.Pirate.Client/Content.Pirate.Client.csproj -c Tools -p:BuildWebUI=false

echo "==> staging engine module (isolated dir, never next to content assemblies)..."
mkdir -p "$MODULE_DIR"
cp -f "$CEF_BIN/Robust.Client.WebView.dll" \
      "$CEF_BIN/Robust.Client.WebView.pdb" \
      "$CEF_BIN/Robust.Client.WebView.deps.json" \
      "$CEF_BIN/Robust.Client.WebView.runtimeconfig.json" \
      "$CEF_BIN/SpaceWizards.CefGlue.dll" \
      "$CEF_BIN/SpaceWizards.CefGlue.pdb" \
      "$MODULE_DIR/"

if [ "$OS" = "Darwin" ]; then
  echo "==> creating macOS .app bundle (hardlinks engine binaries)..."
  rm -rf "$BIN/Content.Client.app"
  python3 RobustToolbox/Tools/macos_make_appbundle.py \
    --webview \
    --name "Content.Client" \
    --directory "$BIN" \
    --apphost Content.Client \
    --identifier org.pirate.webui

  # The bundle packer only links *.dll/*.json/*.pdb files; the CEF webview
  # native shim must be present next to the main executable as well.
  cp -f "$BIN/librobust_native_webview.dylib" \
    "$BIN/Content.Client.app/Contents/MacOS/" 2>/dev/null || true

  # CEF helper processes each need the module binaries next to their helper
  # executable (they are separate .NET hosts). Content's own process keeps the
  # module ONLY in MODULE_DIR to avoid double-assembly identity issues.
  helper_names=("Content.Client helper" "Content.Client helper (GPU)" "Content.Client helper (Renderer)" "Content.Client helper (Alerts)")
  for helper_name in "${helper_names[@]}"; do
    helper_dir="$BIN/Content.Client.app/Contents/Frameworks/$helper_name.app/Contents/MacOS"
    cp -f "$REPO/RobustToolbox/Robust.Client.WebView/bin/Tools/net10.0/Robust.Client.WebView" \
      "$helper_dir/Robust.Client.WebView" 2>/dev/null || true
    for f in Robust.Client.WebView.dll Robust.Client.WebView.deps.json Robust.Client.WebView.runtimeconfig.json SpaceWizards.CefGlue.dll robust_native_webview.dylib; do
      cp -f "$MODULE_DIR/$f" "$helper_dir/$f" 2>/dev/null || true
    done
  done

  echo "==> launching client (app bundle)..."
  ROBUST_MODULE_ROBUST_CLIENT_WEBVIEW="$MODULE_DIR" \
    exec "$BIN/Content.Client.app/Contents/MacOS/Content.Client" "$@"
else
  echo "==> launching client..."
  ROBUST_MODULE_ROBUST_CLIENT_WEBVIEW="$MODULE_DIR" \
    exec "$BIN/Content.Client/Content.Client" "$@"
fi
