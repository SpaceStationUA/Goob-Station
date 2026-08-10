// Regenerates .freebuff/hfr_preview.html embedding the full 4-direction RSI
// sprites (rows S, N, E, W) as base64 data URIs.
using System.Text;

const string Template = """"
<!DOCTYPE html>
<html lang="uk">
<head>
<meta charset="UTF-8">
<title>HFR Sprite Preview</title>
<style>
  body { background:#1a1a22; color:#ddd; font-family: monospace; margin: 20px; }
  h1 { color:#7ec8ff; font-size: 20px; }
  h2 { color:#7ec8ff; margin-top: 30px; font-size: 16px; }
  .wrap { display:flex; gap:40px; flex-wrap:wrap; align-items:flex-start; }
  .card { background:#26262f; border:1px solid #3c3c4a; border-radius:6px; padding:10px; display:inline-block; }
  .card .name { font-size:11px; color:#999; margin-top:6px; text-align:center; }
  .grid { display:flex; flex-wrap:wrap; gap:16px; }
  .cell { background:#26262f; border:1px solid #3c3c4a; border-radius:6px; padding:8px; text-align:center; width: 130px; }
  .cell .name { font-size:11px; color:#999; margin-top:6px; }
  .sprite { display:inline-block; width:32px; height:32px; image-rendering: pixelated;
            background-repeat:no-repeat; }
  .sprite.full { width:96px; height:96px; }
  .lbl { font-size:9px; color:#666; }
  /* 3x3 mockup */
  .machine { position:relative; width:96px; height:96px; image-rendering: pixelated; margin:10px; }
  .tile { position:absolute; width:32px; height:32px; image-rendering: pixelated;
          background-repeat:no-repeat; }
  .note { font-size:12px; color:#aaa; max-width:420px; line-height:1.5; }
  .pulse { animation: blink 1s steps(2, start) infinite; }
  @keyframes blink { 50% { opacity: 0.35; } }
</style>
</head>
<body>
<h1>HFR (Hypertorus Fusion Reactor) — sprite preview</h1>
<p style="color:#888; font-size:12px;">Textures: <code>Resources/Textures/_Pirate/Structures/Machines/hfr_parts.rsi</code> (converted from /tg/station DMI, CC-BY-SA-3.0). Direction order in RSI: rows S, N, E, W. Animation frames run horizontally. All images embedded as data URIs.</p>

<div class="note" style="margin-bottom:18px;">
    <b>Tile layout (from TG hfr_parts.dm):</b><br>
    • North: waste_output (output pipe)<br>
    • South: interface (coolant pipe + port marker)<br>
    • West: fuel_input (fuel pipe)<br>
    • East: moderator_input (moderator pipe)<br>
    • Corners: NW→N, NE→E, SW→W, SE→S<br>
    Each part has <i>idle / active / open</i> variants; the core animates when the reactor runs.
  </div>

<h2>3×3 composed from parts (32×32 tiles)</h2>
<div class="wrap">
  <div class="card">
    <div id="machine" class="machine"></div>
    <div class="name">static parts</div>
  </div>
  <div class="card">
    <div id="machineActive" class="machine"></div>
    <div class="name">active parts (animated)</div>
  </div>
</div>

<h2>All part states (first frame of each; <span class="pulse">■</span> = animated)</h2>
<div class="grid" id="states"></div>

<script>
/*__IMAGES__*/

// Part layout: [row][col] -> {state, dir}. dir = direction the part faces
// (matches the rotation used in-game; RSI rows are S,N,E,W).
const layout = [
  [{s:'corner', d:'n'}, {s:'waste_output', d:'n'}, {s:'corner', d:'e'}],
  [{s:'fuel_input', d:'w'}, {s:'core', d:'s'}, {s:'moderator_input', d:'e'}],
  [{s:'corner', d:'w'}, {s:'interface', d:'s'}, {s:'corner', d:'s'}],
];
const dirRow = {s:0, n:1, e:2, w:3}; // RSI rows: S, N, E, W
const parts = ['core_active','core','core_open',
  'fuel_input_active','fuel_input','fuel_input_open',
  'moderator_input_active','moderator_input','moderator_input_open',
  'waste_output_active','waste_output','waste_output_open',
  'interface_active','interface','interface_coolant','interface_open',
  'corner_active','corner','corner_open','pipe',
  'box_core','box_corner','box_body','crack','box_fuel','box_moderator','box_waste','error'];

// Slice a data-URI sheet into cells. Rows = directions (S,N,E,W), cols = frames.
function sheetInfo(url) {
  return new Promise((res) => {
    const img = new Image();
    img.onload = () => res({w: img.naturalWidth, h: img.naturalHeight});
    img.onerror = () => res({w: 32, h: 32});
    img.src = url;
  });
}

function makeSprite(url, opts = {}) {
  const el = document.createElement('span');
  el.className = 'sprite' + (opts.full ? ' full' : '');
  sheetInfo(url).then(({w, h}) => {
    const cell = opts.cell || 32;
    const frames = Math.max(1, w / cell);
    const dirs = Math.max(1, h / cell / frames);
    let dir = opts.dir || 0;
    if (dirs > 1 && opts.dirName) dir = Math.min(dirRow[opts.dirName] || 0, dirs - 1);
    const fps = opts.fps || 6;
    let f = 0;
    el.style.backgroundImage = 'url("' + url + '")';
    el.style.backgroundSize = w + 'px ' + h + 'px';
    const draw = () => { el.style.backgroundPosition = (-f * cell) + 'px ' + (-dir * cell) + 'px'; };
    draw();
    if (frames > 1) {
      el.classList.add('pulse');
      setInterval(() => { f = (f + 1) % frames; draw(); }, 1000 / fps);
    }
  });
  return el;
}

const mount = (id, url, opts) => {
  const host = document.getElementById(id);
  if (!host) return;
  const el = makeSprite(url, opts);
  host.replaceChildren(el);
  host.classList.remove('pulse');
};

function buildMachine(hostId, useActive) {
  const host = document.getElementById(hostId);
  host.replaceChildren();
  for (let r = 0; r < 3; r++) {
    for (let c = 0; c < 3; c++) {
      const part = layout[r][c];
      const file = useActive ? part.s + '_active' : part.s;
      const url = IMAGES['hfr_parts.rsi_' + file];
      if (!url) continue;
      const tile = document.createElement('div');
      tile.className = 'tile';
      sheetInfo(url).then(({w, h}) => {
        const frames = Math.max(1, w / 32);
        const dirs = Math.max(1, h / 32 / frames);
        const dir = Math.min(dirRow[part.d] || 0, dirs - 1);
        let f = 0;
        tile.style.backgroundImage = 'url("' + url + '")';
        tile.style.backgroundSize = w + 'px ' + h + 'px';
        const draw = () => { tile.style.backgroundPosition = (-f * 32) + 'px ' + (-dir * 32) + 'px'; };
        draw();
        if (useActive && frames > 1) {
          setInterval(() => { f = (f + 1) % frames; draw(); }, 140);
        }
      });
      tile.style.left = (c * 32) + 'px';
      tile.style.top = (r * 32) + 'px';
      host.appendChild(tile);
    }
  }
}
buildMachine('machine', false);
buildMachine('machineActive', true);

const grid = document.getElementById('states');
(async () => {
  for (const s of parts) {
    const cell = document.createElement('div');
    cell.className = 'cell';
    const url = IMAGES['hfr_parts.rsi_' + s];
    if (!url) continue;
    const size = await sheetInfo(url);
    const frames = Math.max(1, size.w / 32);
    const dirs = Math.max(1, size.h / 32 / frames);
    const el = makeSprite(url, {fps: 6});
    cell.appendChild(el);
    const n = document.createElement('div');
    n.className = 'name';
    n.textContent = s + (frames > 1 ? ' · ' + frames + 'fr' : '') + (dirs > 1 ? ' · ' + dirs + 'dir' : '');
    cell.appendChild(n);
    grid.appendChild(cell);
  }
})();
</script>
</body>
</html>
"""";

if (args.Length != 3)
{
    Console.WriteLine("Usage: previewgen <hfr.rsi dir> <hfr_parts.rsi dir> <out.html>");
    return 1;
}

var hfrDir = args[0];
var partsDir = args[1];
var outHtml = args[2];

var images = new StringBuilder();
images.AppendLine("const IMAGES = {");
var first = true;
void AddImages(string dir, string prefix)
{
    if (!Directory.Exists(dir))
        return;
    foreach (var f in Directory.GetFiles(dir, "*.png").OrderBy(Path.GetFileName, StringComparer.Ordinal))
    {
        var name = Path.GetFileNameWithoutExtension(f);
        var b64 = Convert.ToBase64String(File.ReadAllBytes(f));
        if (!first)
            images.AppendLine(",");
        images.Append($"  '{prefix}{name}': 'data:image/png;base64,{b64}'");
        first = false;
    }
}
AddImages(hfrDir, "hfr.rsi_");
AddImages(partsDir, "hfr_parts.rsi_");
images.AppendLine();
images.AppendLine("};");

var html = Template.Replace("/*__IMAGES__*/", images.ToString(), StringComparison.Ordinal);
File.WriteAllText(outHtml, html);
Console.WriteLine($"Wrote {outHtml} ({(new FileInfo(outHtml).Length / 1024)} KB)");
return 0;
