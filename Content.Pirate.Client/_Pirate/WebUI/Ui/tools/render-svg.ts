/**
 * Renders the chart to a standalone SVG file, using the same geometry, the same
 * transform and the same visual constants as the live page.
 *
 * Exists so the map can be looked at — and diffed between iterations — without
 * a browser or the game. Development aid, not a build step: the page remains
 * the source of truth for what ships.
 *
 *   npm run render [outfile]
 */
import { writeFileSync } from "node:fs";
import { cellsInExtent, hexCorners, hexToPixel, key, type Vec2 } from "../src/GalaxyMap/lib/hex";
import { assignCells, cellOutline, cellsByTerritory } from "../src/GalaxyMap/lib/geometry";
import { loopsToPxPath, makeTransform } from "../src/GalaxyMap/lib/transform";
import { CLAIMS, ROUTES, SYSTEMS, TERRITORIES } from "../src/GalaxyMap/lib/devmap";
import { DEFAULT_MAP } from "../src/GalaxyMap/lib/source";
import { pick, type GalaxyModel, type PatternId } from "../src/GalaxyMap/lib/model";

const LOCALE = "en";
const spec = DEFAULT_MAP;
const W = 1600;
// Square canvas on purpose: the macOS rasteriser used for quick looks forces a
// square and crops, so authoring square means nothing gets silently cut off.
// The map is fitted and centred by the transform, leaving deep space above and
// below.
const H = W;
const PAD = 56;
const t = makeTransform(spec.extentLy, W, H, PAD);

const cells = cellsInExtent(spec.extentLy.w, spec.extentLy.h, spec.hexSizeLy);
const { ownership, contested } = assignCells(CLAIMS, cells, spec.hexSizeLy, spec.unclaimedId);
const model: GalaxyModel = {
  extentLy: spec.extentLy,
  hexSizeLy: spec.hexSizeLy,
  territories: TERRITORIES,
  systems: SYSTEMS,
  routes: ROUTES,
  ownership,
  contested: new Set(contested.map(c => key(c.cell.q, c.cell.r))),
  revision: 0,
};

function mulberry(seed: number) {
  return () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let t2 = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t2 = (t2 + Math.imul(t2 ^ (t2 >>> 7), 61 | t2)) ^ t2;
    return ((t2 ^ (t2 >>> 14)) >>> 0) / 4294967296;
  };
}

const TILE = 9;
function patternBody(kind: PatternId, c: string, id: string): string {
  const body: Record<PatternId, string> = {
    solid: "",
    hatch: `<path d="M-1,1 l2,-2 M0,${TILE} l${TILE},-${TILE} M${TILE - 1},${TILE + 1} l2,-2" stroke="${c}" stroke-width="1.1" opacity="0.5"/>`,
    crosshatch: `<path d="M-1,1 l2,-2 M0,${TILE} l${TILE},-${TILE} M${TILE - 1},${TILE + 1} l2,-2" stroke="${c}" stroke-width="1" opacity="0.4"/><path d="M1,-1 l2,2 M${TILE},0 l-${TILE},${TILE} M${TILE + 1},${TILE - 1} l-2,2" stroke="${c}" stroke-width="1" opacity="0.4"/>`,
    dots: `<circle cx="${TILE / 2}" cy="${TILE / 2}" r="1.4" fill="${c}" opacity="0.5"/>`,
    grid: `<path d="M${TILE},0 L${TILE},${TILE} M0,${TILE} L${TILE},${TILE}" stroke="${c}" stroke-width="1" opacity="0.45"/>`,
    horizontal: `<path d="M0,2.5 L${TILE},2.5 M0,6.5 L${TILE},6.5" stroke="${c}" stroke-width="1.1" opacity="0.45"/>`,
    vertical: `<path d="M2.5,0 L2.5,${TILE} M6.5,0 L6.5,${TILE}" stroke="${c}" stroke-width="1.1" opacity="0.45"/>`,
    checker: `<rect x="0" y="0" width="4.5" height="4.5" fill="${c}" opacity="0.4"/><rect x="4.5" y="4.5" width="4.5" height="4.5" fill="${c}" opacity="0.4"/>`,
    starfield: "",
  };
  return `<pattern id="${id}" width="${TILE}" height="${TILE}" patternUnits="userSpaceOnUse">${body[kind]}</pattern>`;
}

const out: string[] = [];
const push = (x: string) => out.push(x);
const esc = (s: string) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
const MONO = "ui-monospace,Menlo,Consolas,monospace";

push(`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">`);
push(
  `<defs>` +
    `<radialGradient id="deep" cx="50%" cy="42%" r="78%">` +
    `<stop offset="0%" stop-color="#0d1526"/><stop offset="55%" stop-color="#070c17"/>` +
    `<stop offset="100%" stop-color="#03050a"/></radialGradient>` +
    `<radialGradient id="nebA" cx="30%" cy="35%" r="42%">` +
    `<stop offset="0%" stop-color="#2a3f7a" stop-opacity="0.5"/><stop offset="100%" stop-color="#2a3f7a" stop-opacity="0"/></radialGradient>` +
    `<radialGradient id="nebB" cx="72%" cy="66%" r="46%">` +
    `<stop offset="0%" stop-color="#5a2a5e" stop-opacity="0.38"/><stop offset="100%" stop-color="#5a2a5e" stop-opacity="0"/></radialGradient>` +
    TERRITORIES.map(x => patternBody(x.pattern, x.color, `pat-${x.id}`)).join("") +
    `</defs>`,
);

push(`<rect width="${W}" height="${H}" fill="url(#deep)"/>`);
push(`<rect width="${W}" height="${H}" fill="url(#nebA)"/>`);
push(`<rect width="${W}" height="${H}" fill="url(#nebB)"/>`);

{
  const rnd = mulberry(90210);
  let stars = "";
  for (let i = 0; i < 900; i++) {
    const b = rnd();
    stars += `<circle cx="${(rnd() * W).toFixed(1)}" cy="${(rnd() * H).toFixed(1)}" r="${(0.4 + b * b * 1.9).toFixed(2)}" fill="#dfe9f5" opacity="${(0.14 + b * 0.7).toFixed(2)}"/>`;
  }
  push(`<g>${stars}</g>`);
}

{
  let g = "";
  for (const c of cells) {
    const pts = hexCorners(hexToPixel(c, spec.hexSizeLy), spec.hexSizeLy).map(t.toPx);
    g += `<path d="M${pts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join("L")}Z"/>`;
  }
  push(`<g fill="none" stroke="#82a5c8" stroke-opacity="0.09" stroke-width="0.7" pointer-events="none">${g}</g>`);
}

const byTerr = cellsByTerritory(ownership);
const fillD = new Map<string, string>();
for (const terr of TERRITORIES) {
  const set = new Set(byTerr.get(terr.id) ?? []);
  if (!set.size) continue;
  fillD.set(terr.id, loopsToPxPath(cellOutline(set, spec.hexSizeLy), t));
}

for (const terr of TERRITORIES) {
  const d = fillD.get(terr.id);
  if (!d) continue;
  push(`<path d="${d}" fill-rule="evenodd" fill="${terr.color}" opacity="${terr.unclaimed ? 0.14 : 0.26}"/>`);
  push(`<path d="${d}" fill-rule="evenodd" fill="url(#pat-${terr.id})" opacity="${terr.unclaimed ? 0.22 : 0.8}"/>`);
}

for (const terr of TERRITORIES) {
  const d = fillD.get(terr.id);
  if (!d) continue;
  push(`<path d="${d}" fill="none" stroke="${terr.color}" stroke-width="${terr.unclaimed ? 2 : 9}" opacity="${terr.unclaimed ? 0.16 : 0.28}" stroke-linejoin="round"/>`);
  push(`<path d="${d}" fill="none" stroke="${terr.color}" stroke-width="${terr.unclaimed ? 1.2 : 2.4}" opacity="${terr.unclaimed ? 0.4 : 0.95}" stroke-linejoin="round"/>`);
}

// Border highlight, stroked from the same loops as the fills (see Chart.tsx).
for (const terr of TERRITORIES) {
  if (terr.unclaimed) continue;
  const d = fillD.get(terr.id);
  if (!d) continue;
  push(`<path d="${d}" fill="none" stroke="#eaf3fb" stroke-width="2.4" opacity="0.85" stroke-linejoin="round"/>`);
}

for (const r of ROUTES) {
  const a = SYSTEMS.find(x => x.id === r.from);
  const b = SYSTEMS.find(x => x.id === r.to);
  if (!a || !b) continue;
  const A = t.toPx({ x: a.xLy, y: a.yLy });
  const B = t.toPx({ x: b.xLy, y: b.yLy });
  const cx = (A.x + B.x) / 2 + (B.y - A.y) * 0.16;
  const cy = (A.y + B.y) / 2 - (B.x - A.x) * 0.16;
  push(
    `<path d="M${A.x.toFixed(1)},${A.y.toFixed(1)} Q${cx.toFixed(1)},${cy.toFixed(1)} ${B.x.toFixed(1)},${B.y.toFixed(1)}" fill="none" stroke="#7fd4c8" stroke-width="1.3" opacity="0.6" stroke-dasharray="${r.kind === "gate" ? "7 5" : "3 5"}"/>`,
  );
}

const terrColor = new Map(TERRITORIES.map(x => [x.id, x.color]));

/* Contested ground, matching Chart.tsx: one amber cross-hatch plus a light
   per-cell stroke, drawn over the fills and under everything else. The offline
   renderer has no painting, so this only ever shows the cells the bake flagged. */
if (model.contested.size > 0) {
  const parts: string[] = [];
  for (const k of model.contested) {
    const [q, r] = k.split(",");
    const pts = hexCorners(hexToPixel({ q: +q, r: +r }, spec.hexSizeLy), spec.hexSizeLy).map(t.toPx);
    parts.push("M" + pts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join("L") + "Z");
  }
  const d = parts.join(" ");
  push(
    `<defs><pattern id="pat-contested" width="8" height="8" patternUnits="userSpaceOnUse">` +
      `<rect width="8" height="8" fill="#ffb454" fill-opacity="0.1"/>` +
      `<path d="M0,8 L8,0" stroke="#ffb454" stroke-width="1.6" stroke-opacity="0.85"/>` +
      `<path d="M-2,2 L2,-2 M6,10 L10,6" stroke="#ffb454" stroke-width="1.6" stroke-opacity="0.85"/>` +
      `</pattern></defs>`,
  );
  push(`<path d="${d}" fill="url(#pat-contested)" fill-rule="evenodd"/>`);
  push(`<path d="${d}" fill="none" stroke="#ffb454" stroke-width="1.1" opacity="0.6"/>`);
}


/**
 * Pick the spot for a centred label that keeps it furthest from every system
 * marker. Mirrors place() in Chart.tsx — the centroid of a cell set routinely
 * lands on a capital, and a letterspaced uppercase title is a third of the map
 * wide, so nudging the anchor clear is nowhere near enough. Scores the label's
 * whole box, not its anchor point.
 */
function place(
  centre: { x: number; y: number },
  name: string,
  size: number,
): { x: number; y: number } {
  const halfW = (name.length * size * 0.65) / 2;
  const halfH = size * 0.7;
  const obstacles = SYSTEMS.map(s => t.toPx({ x: s.xLy, y: s.yLy }));

  const candidates: { x: number; y: number }[] = [centre];
  for (let ring = 1; ring <= 4; ring++) {
    for (let a = 0; a < 12; a++) {
      const th = (a / 12) * Math.PI * 2 + ring * 0.3;
      candidates.push({
        x: centre.x + Math.cos(th) * ring * 17,
        y: centre.y + Math.sin(th) * ring * 12,
      });
    }
  }

  let best = centre;
  let bestScore = -Infinity;
  for (const q of candidates) {
    let clearance = Infinity;
    for (const o of obstacles) {
      const dx = Math.max(0, Math.abs(o.x - q.x) - halfW);
      const dy = Math.max(0, Math.abs(o.y - q.y) - halfH);
      clearance = Math.min(clearance, Math.hypot(dx, dy));
    }
    const score = Math.min(clearance, 34) - Math.hypot(q.x - centre.x, q.y - centre.y) * 0.3;
    if (score > bestScore) {
      bestScore = score;
      best = q;
    }
  }
  return best;
}

/* Territory names first, systems over them — same z-order as the page. */
for (const terr of TERRITORIES) {
  const list = byTerr.get(terr.id) ?? [];
  if (list.length < 8) continue;
  let sx = 0;
  let sy = 0;
  for (const k of list) {
    const [q, r] = k.split(",");
    const c = hexToPixel({ q: +q, r: +r }, spec.hexSizeLy);
    sx += c.x;
    sy += c.y;
  }
  const centre = t.toPx({ x: sx / list.length, y: sy / list.length });

  // Unclaimed space is a hole, not a nation, and its centroid is the dead centre
  // of the chart. Small and dim beats a headline there.
  if (terr.unclaimed) {
    push(
      `<text x="${centre.x.toFixed(1)}" y="${centre.y.toFixed(1)}" font-family="${MONO}" font-size="12" letter-spacing="1" fill="${terr.color}" stroke="#000" stroke-width="3" paint-order="stroke" text-anchor="middle" opacity="0.3">${esc(pick(terr.name, LOCALE))}</text>`,
    );
    continue;
  }

  const size = Math.max(11, Math.min(22, Math.sqrt(list.length) * 0.95));
  const name = pick(terr.name, LOCALE);
  const P = place(centre, name, size);
  push(
    `<text x="${P.x.toFixed(1)}" y="${P.y.toFixed(1)}" font-family="${MONO}" font-size="${size.toFixed(1)}" letter-spacing="${(size * 0.3).toFixed(1)}" fill="${terr.color}" stroke="#000" stroke-width="4" paint-order="stroke" text-anchor="middle" opacity="0.82">${esc(name)}</text>`,
  );
}

for (const sys of SYSTEMS) {
  const c = terrColor.get(sys.territory) ?? "#94a3b8";
  const big = sys.importance >= 2;
  const P = t.toPx({ x: sys.xLy, y: sys.yLy });
  if (sys.importance === 3) {
    push(`<circle cx="${P.x.toFixed(1)}" cy="${P.y.toFixed(1)}" r="10" fill="none" stroke="${c}" stroke-width="1.6" opacity="0.9"/>`);
  }
  if (sys.kind === "station" || sys.kind === "outpost") {
    push(`<rect x="${(P.x - 3.5).toFixed(1)}" y="${(P.y - 3.5).toFixed(1)}" width="7" height="7" fill="#cbd8e6" opacity="0.9"/>`);
  } else {
    const r = big ? 5 : 3.2;
    push(`<circle cx="${P.x.toFixed(1)}" cy="${P.y.toFixed(1)}" r="${r}" fill="${c}" stroke="#0a0f18" stroke-width="1"/>`);
  }
  push(
    `<text x="${(P.x + (sys.importance === 3 ? 15 : 9)).toFixed(1)}" y="${(P.y - 5).toFixed(1)}" font-family="${MONO}" font-size="${sys.importance === 3 ? 12 : 10}" letter-spacing="${sys.importance === 3 ? 1.4 : 0.6}" fill="#e6eef7" stroke="#000" stroke-width="2.6" paint-order="stroke">${esc(pick(sys.name, LOCALE))}</text>`,
  );
}

const frame = {
  x: t.ox - (spec.extentLy.w / 2) * t.scale,
  y: t.oy - (spec.extentLy.h / 2) * t.scale,
  width: spec.extentLy.w * t.scale,
  height: spec.extentLy.h * t.scale,
};
push(
  `<rect x="${frame.x.toFixed(1)}" y="${frame.y.toFixed(1)}" width="${frame.width.toFixed(1)}" height="${frame.height.toFixed(1)}" fill="none" stroke="#8cafd2" stroke-opacity="0.28" stroke-width="1"/>`,
);
push(
  `<text x="${(frame.x + 4).toFixed(1)}" y="${(frame.y - 16).toFixed(1)}" font-family="${MONO}" font-size="13" letter-spacing="5" fill="#9fc0dd" opacity="0.85">ORION SPUR</text>`,
);
push(
  `<text x="${(frame.x + frame.width - 4).toFixed(1)}" y="${(frame.y - 16).toFixed(1)}" text-anchor="end" font-family="${MONO}" font-size="11" letter-spacing="2" fill="#6d8299">${spec.extentLy.w} x ${spec.extentLy.h} LY · ${cells.length} CELLS @ ${spec.hexSizeLy} LY</text>`,
);
push(`</svg>`);

const dest = process.argv[2] ?? "galaxy-preview.svg";
writeFileSync(dest, out.join("\n"));
console.log(
  `wrote ${dest} (${W}x${H}, scale ${t.scale.toFixed(2)} px/LY, ${cells.length} cells, ${cells.length} outline loops)`,
);
