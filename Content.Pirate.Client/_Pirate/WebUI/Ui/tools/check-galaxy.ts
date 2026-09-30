/**
 * Dev sanity check for the galaxy geometry. Run with:
 *   npm run check
 *
 * Not a substitute for looking at the chart, but it catches the failure modes
 * that are invisible in a screenshot: NaNs, unclosed loops, orphaned systems
 * and territories that came out empty.
 */
import { cellsInExtent, hexToPixel, key, neighbour, pixelToHex, type Axial } from "../src/GalaxyMap/lib/hex";
import { assignCells, borderLoops, cellOutline, cellsByTerritory, loopToPath } from "../src/GalaxyMap/lib/geometry";
import { DEFAULT_MAP } from "../src/GalaxyMap/lib/source";
import { CLAIMS, ROUTES, SYSTEMS, TERRITORIES } from "../src/GalaxyMap/lib/devmap";
import type { Vec2 } from "../src/GalaxyMap/lib/hex";

let failures = 0;
const check = (name: string, ok: boolean, detail = "") => {
  if (!ok) failures++;
  console.log(`${ok ? "  ok  " : " FAIL "} ${name}${detail ? ` — ${detail}` : ""}`);
};

const spec = DEFAULT_MAP;
const cells = cellsInExtent(spec.extentLy.w, spec.extentLy.h, spec.hexSizeLy);

console.log(`\ngrid: ${spec.extentLy.w} x ${spec.extentLy.h} LY, cell ${spec.hexSizeLy} LY`);
console.log(`cells: ${cells.length}\n`);

// --- grid ------------------------------------------------------------------
const bad = cells.filter(c => !Number.isFinite(hexToPixel(c, spec.hexSizeLy).x));
check("every cell centre is finite", bad.length === 0, `${bad.length} bad`);

let rt = 0;
for (const c of cells) {
  const p = hexToPixel(c, spec.hexSizeLy);
  const back = pixelToHex(p, spec.hexSizeLy);
  if (back.q !== c.q || back.r !== c.r) rt++;
}
check("pixel<->hex round trips", rt === 0, `${rt}/${cells.length} mismatched`);

// --- claims -> ownership ---------------------------------------------------
const { ownership, contested, unclaimedCells } = assignCells(
  CLAIMS,
  cells,
  spec.hexSizeLy,
  spec.unclaimedId,
);
// Overlap is legitimate — it is how two hand-drawn claims share a border — so
// this is reported, not failed. The thing that must hold is that it happened
// somewhere, otherwise no two nations actually meet.
check(
  "some cells are contested (nations share borders)",
  contested.length > 0,
  `${contested.length} contested, ${unclaimedCells} unclaimed of ${cells.length}`,
);
check("every cell is owned", ownership.size === cells.length, `${ownership.size}/${cells.length}`);

const byTerr = cellsByTerritory(ownership);
const empty = TERRITORIES.filter(t => (byTerr.get(t.id)?.length ?? 0) === 0);
check("no territory came out empty", empty.length === 0, empty.map(t => t.id).join(","));

console.log("\nterritory sizes:");
for (const t of TERRITORIES) {
  const n = byTerr.get(t.id)?.length ?? 0;
  const pct = ((n / cells.length) * 100).toFixed(1);
  console.log(`  ${t.name.padEnd(30)} ${String(n).padStart(5)} cells  ${pct.padStart(5)}%`);
}

// --- outlines --------------------------------------------------------------
const hasNaN = (pts: Vec2[]) => pts.some(p => !Number.isFinite(p.x) || !Number.isFinite(p.y));

let outlineLoops = 0;
let badLoops = 0;
for (const [id, list] of byTerr) {
  const loops = cellOutline(new Set(list), spec.hexSizeLy);
  outlineLoops += loops.length;
  for (const l of loops) {
    if (hasNaN(l) || l.length < 3) badLoops++;
  }
  const d = loops.map(l => loopToPath(l)).join(" ");
  if (/NaN|Infinity|undefined/.test(d)) badLoops++;
}
check("outlines are finite and closable", badLoops === 0, `${badLoops} bad, ${outlineLoops} loops total`);

// --- borders ---------------------------------------------------------------
const model = {
  extentLy: spec.extentLy,
  hexSizeLy: spec.hexSizeLy,
  territories: TERRITORIES,
  systems: SYSTEMS,
  routes: ROUTES,
  ownership,
  revision: 0,
};
const borders = borderLoops(model);
const badBorders = borders.filter(b => hasNaN(b.loop) || b.loop.length < 2).length;
check("border chains are finite", badBorders === 0, `${badBorders} bad of ${borders.length}`);

/**
 * Every segment actually drawn must be at most one hex edge long.
 *
 * Asserted on `raw`, and only over the segments that get emitted: an open chain
 * is drawn without `Z`, so its last-to-first gap is never rendered. Checking
 * the wrap-around of an open chain is exactly the bug that put a straight white
 * line across the chart — `loopToPath` was closing fragments, and SVG drew the
 * gap as a segment. Checking the smoothed loop instead is also useless: the
 * wobble subdivides everything to `wobbleStepLy`, so a 50 LY jump comes out as
 * a long run of short, entirely plausible steps.
 */
const maxEdgeLy = 2 * spec.hexSizeLy;
let worst = 0;
let worstChain = -1;
let drawn = 0;
borders.forEach((b, i) => {
  const n = b.raw.length;
  const last = b.closed ? n : n - 1;
  for (let j = 0; j < last; j++) {
    const p = b.raw[j];
    const q = b.raw[(j + 1) % n];
    drawn++;
    const d = Math.hypot(q.x - p.x, q.y - p.y);
    if (d > worst) {
      worst = d;
      worstChain = i;
    }
  }
});
check(
  "no drawn border segment spans more than one hex edge",
  worst <= maxEdgeLy + 1e-3,
  `longest ${worst.toFixed(2)} LY of ${maxEdgeLy.toFixed(2)} allowed, chain ${worstChain}, ${drawn} segments`,
);

const openChains = borders.filter(b => !b.closed).length;
console.log(`  (${borders.length} chains, ${openChains} open at three-way junctions)`);

/**
 * Every open chain end must have at least one other border edge at that vertex,
 * or the border has a visible gap.
 *
 * Note this is about EDGES at the point, not about other chains ending there.
 * Where three nations meet the vertex has odd degree: one chain runs straight
 * through it (the point is interior, not an endpoint) and another terminates on
 * it. Counting endpoint touches reports one, which looks like a dangling end
 * and is not.
 */
const vkey = (p: { x: number; y: number }) => `${Math.round(p.x * 1e4)},${Math.round(p.y * 1e4)}`;
const incident = new Map<string, number>();
for (const b of borders) {
  for (let j = 0; j + 1 < b.raw.length; j++) {
    incident.set(vkey(b.raw[j]), (incident.get(vkey(b.raw[j])) ?? 0) + 1);
    incident.set(vkey(b.raw[j + 1]), (incident.get(vkey(b.raw[j + 1])) ?? 0) + 1);
  }
}
const gaps: string[] = [];
for (const b of borders) {
  if (b.closed) continue;
  for (const end of [b.raw[0], b.raw[b.raw.length - 1]]) {
    if ((incident.get(vkey(end)) ?? 0) >= 2) continue;
    // The chart rectangle cuts the lattice, so a cell near the edge can have a
    // neighbour outside the map. Those edges have no owner and are dropped,
    // which can leave a legitimate degree-1 vertex. Only interior points are
    // required to continue.
    const margin = spec.hexSizeLy * 2;
    const atEdge =
      Math.abs(end.x) > spec.extentLy.w / 2 - margin ||
      Math.abs(end.y) > spec.extentLy.h / 2 - margin;
    if (!atEdge) gaps.push(`${end.x.toFixed(2)},${end.y.toFixed(2)}`);
  }
}
check("open chains meet another border edge", gaps.length === 0, gaps.join(" "));

/**
 * The invariant that actually matters: a chain traces ONE border between ONE
 * pair of territories.
 *
 * A walker that hops onto a different nation's border at a three-way junction
 * still produces short, plausible segments — every one of them a real hex edge —
 * so the per-segment check above sails straight through. The tell is a chain
 * whose edges belong to more than one territory pair, and the resulting chord
 * across the chart.
 */
const multiPair = borders.filter(b => {
  const pairs = new Set<string>();
  for (let j = 0; j + 1 < b.raw.length; j++) {
    const p = b.raw[j];
    const q = b.raw[j + 1];
    const ca = pixelToHex(p, spec.hexSizeLy);
    const cb = pixelToHex(q, spec.hexSizeLy);
    for (const c of [ca, cb]) {
      for (let e = 0; e < 6; e++) {
        const nb = neighbour(c, e);
        if (pixelToHex(hexToPixel(nb, spec.hexSizeLy), spec.hexSizeLy) !== ca &&
            pixelToHex(hexToPixel(nb, spec.hexSizeLy), spec.hexSizeLy) !== cb) continue;
        const o = ownership.get(key(nb.q, nb.r));
        if (o !== undefined && o !== ownership.get(key(c.q, c.r))) {
          pairs.add([ownership.get(key(c.q, c.r)) ?? "", o].sort().join("|"));
        }
      }
    }
  }
  return pairs.size > 1;
});
check("each chain traces a single border", multiPair.length === 0, `${multiPair.length} chains span multiple pairs`);

// Every border edge must be drawn exactly once.
const drawnEdges = borders.reduce((n, b) => n + Math.max(0, b.raw.length - 1), 0);
let trueEdges = 0;
for (const [cellId, owner] of ownership) {
  const [q, r] = cellId.split(",");
  const c: Axial = { q: +q, r: +r };
  for (let e = 0; e < 6; e++) {
    const nb = neighbour(c, e);
    const other = ownership.get(key(nb.q, nb.r));
    if (other === undefined || other === owner) continue;
    // Count each shared edge once.
    if (c.q < nb.q || (c.q === nb.q && c.r < nb.r)) trueEdges++;
  }
}
check(
  "every border edge is drawn",
  drawnEdges === trueEdges,
  `${drawnEdges} drawn vs ${trueEdges} on the lattice`,
);

/**
 * Adjacency computed straight from ownership, not inferred from the border
 * chains. A chain that wanders at a vertex where three cells meet can span two
 * different territory pairs, so the chains are the wrong place to read this off
 * — they exist for drawing, not for querying.
 */
const pairs = new Set<string>();
const neighboursOf = new Map<string, Set<string>>();
for (const [cellId, owner] of ownership) {
  const [q, r] = cellId.split(",");
  const cell: Axial = { q: +q, r: +r };
  for (let e = 0; e < 6; e++) {
    const nb = neighbour(cell, e);
    const other = ownership.get(key(nb.q, nb.r));
    if (other === undefined || other === owner) continue;
    pairs.add([owner, other].sort().join("|"));
    if (!neighboursOf.has(owner)) neighboursOf.set(owner, new Set());
    if (!neighboursOf.has(other)) neighboursOf.set(other, new Set());
    neighboursOf.get(owner)!.add(other);
    neighboursOf.get(other)!.add(owner);
  }
}

console.log(`\nborders: ${borders.length} chains`);
console.log(`adjacency: ${pairs.size} pairs\n`);
for (const t of TERRITORIES) {
  if (t.unclaimed) continue;
  const ns = [...(neighboursOf.get(t.id) ?? [])].sort();
  console.log(`  ${t.name.padEnd(30)} -> ${ns.join(", ") || "(NOTHING)"}`);
}

// A nation with no neighbours at all is invisible on the chart.
const isolated = TERRITORIES.filter(t => !t.unclaimed && (neighboursOf.get(t.id)?.size ?? 0) === 0);
check("every territory touches something", isolated.length === 0, isolated.map(t => t.id).join(","));

const sovereignPairs = [...pairs].filter(p => !p.includes(spec.unclaimedId));
check("nations border other nations", sovereignPairs.length > 0, `${sovereignPairs.length} nation-to-nation borders`);

// --- systems ---------------------------------------------------------------
const orphans = SYSTEMS.filter(s => s.territory && !TERRITORIES.some(t => t.id === s.territory));
check("every system names a real territory", orphans.length === 0, orphans.map(s => s.id).join(","));

const offMap = SYSTEMS.filter(s => Math.abs(s.xLy) > spec.extentLy.w / 2 || Math.abs(s.yLy) > spec.extentLy.h / 2);
check("every system is on the chart", offMap.length === 0, offMap.map(s => s.id).join(","));

// A system should sit inside the territory it claims.
const mismatched = SYSTEMS.filter(s => {
  if (!s.territory) return false;
  const cell = pixelToHex({ x: s.xLy, y: s.yLy }, spec.hexSizeLy);
  return ownership.get(key(cell.q, cell.r)) !== s.territory;
});
check("systems sit in the territory they claim", mismatched.length === 0,
  mismatched.map(s => `${s.id}->${ownership.get(key(pixelToHex({ x: s.xLy, y: s.yLy }, spec.hexSizeLy).q, pixelToHex({ x: s.xLy, y: s.yLy }, spec.hexSizeLy).r))}`).join(", "));

// --- routes ----------------------------------------------------------------
const badRoutes = ROUTES.filter(r => !SYSTEMS.some(s => s.id === r.from) || !SYSTEMS.some(s => s.id === r.to));
check("every route endpoint exists", badRoutes.length === 0, badRoutes.map(r => `${r.from}->${r.to}`).join(","));

console.log(`\n${failures === 0 ? "all checks passed" : `${failures} CHECK(S) FAILED`}\n`);
process.exit(failures === 0 ? 0 : 1);
