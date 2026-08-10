# Freebuff preview — run doc

The project is **Space Station 14 (Goob-Station / Pirate fork)**, a .NET game — it has no web
frontend or dev server. The Preview tab shows a static sprite preview page for the HFR
(Hypertorus Fusion Reactor) machine textures being ported from /tg/station.

## How to reproduce the artifact

The preview file `.freebuff/hfr_preview.html` is a **self-contained HTML page** with all 30
sprite sheets embedded as base64 data URIs (the preview static server 404s external texture
requests, so images must be inlined).

Regenerate after the source textures change (e.g. new HFR sprites in
`Resources/Textures/_Pirate/Structures/Machines/`):

1. From `Resources/Textures/_Pirate/Structures/Machines/`, build the base64 map:
   ```bash
   { echo "const IMAGES = {"; \
     for f in hfr.rsi/*.png hfr_parts.rsi/*.png; do \
       b64=$(base64 -w0 "$f"); key=$(echo "$f" | sed 's/\//_/; s/\.png//'); \
       echo "  '$key': 'data:image/png;base64,$b64',"; \
     done; echo "};"; } > /tmp/hfr_images.js
   ```
2. Recreate `.freebuff/hfr_preview.html` from the template (the `/*__IMAGES__*/` placeholder
   in the `<script>` block) and inject the map:
   ```bash
   awk '{ if ($0 ~ /\/\*__IMAGES__\*\//) { while ((getline line < "/tmp/hfr_images.js") > 0) print line; close("/tmp/hfr_images.js"); next } print }' \
     hfr_preview.html > hfr_preview.new.html && mv hfr_preview.new.html hfr_preview.html
   ```
3. Verify: `grep -c "data:image/png;base64" .freebuff/hfr_preview.html` → `30`.

Sheet layout assumptions encoded in the page: RSI cells are 32×32; direction rows stack
vertically in the order S, E, N, W; animation frames run horizontally; `hfr.rsi/hfr.png` is
the full 96×96 machine (3×3 tiles), `hfr_active.png` is 384×96 (4 frames).

## How to run the server

No server or dependency install is needed. Register the page directly in Freebuff:

- `register_preview` with `htmlPath = C:\Users\fentanil\Documents\GitHub\Goob-Station\.freebuff\hfr_preview.html`

The app serves it on a loopback URL (currently `http://127.0.0.1:62746/hfr_preview.html`)
without a separate process. It reloads from the source file on refresh.

> Note: `preview_screenshot` may fail with "webview is not being composited" in headless
> contexts; the page itself is verified via DOM/JS checks (all 30 sheets resolve, gallery 27
> cells, both 3×3 machines 9 tiles each).
