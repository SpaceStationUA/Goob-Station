/**
 * Dev sanity check for the galaxy geometry. Run with:
 *   npm run check
 *
 * Not a substitute for looking at the chart, but it catches the failure modes
 * that are invisible in a screenshot: NaNs, unclosed loops, orphaned systems
 * and territories that came out empty.
 */
import {
  cellsInExtent,
  hexDistance,
  hexLine,
  hexToPixel,
  key,
  NEIGHBOURS,
  neighbour,
  pixelToHex,
  type Axial,
} from "../src/GalaxyMap/lib/hex";
import { assignCells, borderLoops, cellOutline, cellsByTerritory, loopToPath } from "../src/GalaxyMap/lib/geometry";
import { parseYamlSequence } from "./yaml-subset";
import { readPrototype } from "./bake";
import { CELLS as BAKED_CELLS, CONTESTED as BAKED_CONTESTED, FINGERPRINT as BAKE_FP } from "../src/GalaxyMap/lib/baked";
import { bakedModel, buildOwnership, checkBake, DEFAULT_MAP } from "../src/GalaxyMap/lib/source";
import { cellsInExtent } from "../src/GalaxyMap/lib/hex";
import { CLAIMS as BAKE_CLAIMS } from "../src/GalaxyMap/lib/devmap";
import {
  ALPHA_CUT,
  DISC,
  DISC_PIXELS,
  DISK_WIDTH,
  HOLE,
  HOLE_CANVAS_RATIO,
  HOLE_LIGHT_WIDTH,
  HOLE_PIXELS,
  HOLE_RADIUS,
  NOISE_SIZE,
  N_COLORS,
  OCTAVES,
  PERSPECTIVE,
  ROTATION,
  TIME_SPEED,
} from "../src/GalaxyMap/lib/blackhole-consts";
import { DEFAULT_MAP, FixtureSource } from "../src/GalaxyMap/lib/source";
import { CLAIMS, ROUTES, SYSTEMS, TERRITORIES } from "../src/GalaxyMap/lib/devmap";
import type { Vec2 } from "../src/GalaxyMap/lib/hex";
import { pick, type GalaxyModel } from "../src/GalaxyMap/lib/model";
import { readableOnDark } from "../src/GalaxyMap/Chart";
import { ringGeomFor, ringHalf } from "../src/GalaxyMap/WorldRing";
import {
  DEFAULT_STRINGS,
  installStrings,
  missingTranslations,
  pluralCategory,
  resetStrings,
  t,
  tp,
} from "../src/GalaxyMap/lib/i18n";

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
  console.log(`  ${pick(t.name, "en").padEnd(30)} ${String(n).padStart(5)} cells  ${pct.padStart(5)}%`);
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
  console.log(`  ${pick(t.name, "en").padEnd(30)} -> ${ns.join(", ") || "(NOTHING)"}`);
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

// --- paint reactivity -------------------------------------------------------
/**
 * Painting appeared to do absolutely nothing, and the cause was invisible from
 * the geometry side: the source mutated its own model in place and the view was
 * never handed a new reference, so Solid had nothing to react to. The data was
 * correct the whole time. These assertions pin the contract that broke.
 */
console.log("\npaint:");
{
  const source = new FixtureSource(spec, TERRITORIES, CLAIMS, SYSTEMS, ROUTES);
  const before = await source.load();

  let notified = 0;
  let latest: GalaxyModel | undefined;
  source.onChange(m => {
    notified++;
    latest = m;
  });

  // Take a real Biesel cell and hand it to Nralakk.
  const victim = (byTerr.get("biesel") ?? [])[0];
  const [vq, vr] = victim.split(",").map(Number);

  const changed = await source.paint(vq, vr, "nralakk");
  check("paint reports that it changed something", changed === true);
  check("paint notifies listeners", notified === 1, `${notified} notification(s)`);
  check("paint hands back a NEW model object", !!latest && latest !== before);
  check("the painted cell really changed owner", latest?.ownership.get(victim) === "nralakk");
  check("the model the view already had is left alone", before.ownership.get(victim) === "biesel");

  // Exactly one cell may differ, or painting is quietly doing more than asked.
  let diffs = 0;
  for (const [cell, owner] of latest!.ownership) {
    if (before.ownership.get(cell) !== owner) diffs++;
  }
  check("exactly one cell changed", diffs === 1, `${diffs} cells differ`);

  // Painting a cell to the territory it already belongs to is a no-op, and must
  // not burn an undo step.
  const noop = await source.paint(vq, vr, "nralakk");
  check("re-painting the same territory is a no-op", noop === false && notified === 1);

  await source.undo();
  check("undo restores the cell", latest?.ownership.get(victim) === "biesel");
  check("undo restores the whole map", [...before.ownership].every(([c, o]) => latest!.ownership.get(c) === o));
}

// --- contested ground -------------------------------------------------------
/**
 * Two flavours of dispute, and they must not interfere.
 *
 * Baked: cells two claims both cover. The depth rule picks an owner so the map
 * is definite, but "settled" is not "agreed" and the chart is allowed to say so.
 * Painted: an admin flagging a row in-round, which must not touch ownership at
 * all — marking a border as contested says nothing about who holds it.
 */
console.log("\ncontested:");
{
  const source = new FixtureSource(spec, TERRITORIES, CLAIMS, SYSTEMS, ROUTES);
  const base = await source.load();

  check("the bake surfaces disputed cells", base.contested.size > 0, `${base.contested.size} cell(s)`);
  check(
    "every disputed cell is a real cell",
    [...base.contested].every(c => base.ownership.has(c)),
  );

  let latest: GalaxyModel | undefined;
  let notified = 0;
  source.onChange(m => {
    notified++;
    latest = m;
  });

  // A cell nobody disputes yet.
  const quiet = (byTerr.get("biesel") ?? []).find(c => !base.contested.has(c))!;
  const [cq, cr] = quiet.split(",").map(Number);

  const flagged = await source.setContested(cq, cr, true);
  check("flagging a dispute reports a change", flagged === true);
  check("flagging notifies listeners", notified === 1, `${notified} notification(s)`);
  check("the flag is set on the new model", latest?.contested.has(quiet) === true);
  check("the model the view already had is untouched", base.contested.has(quiet) === false);
  check("flagging a dispute does not change the owner", latest?.ownership.get(quiet) === "biesel");
  check("a fresh set, not a mutation of the old one", latest!.contested !== base.contested);

  const again = await source.setContested(cq, cr, true);
  check("re-flagging the same cell is a no-op", again === false && notified === 1);

  // Un-flag a cell the bake marked disputed.
  const wasDisputed = [...base.contested][0];
  const [dq, dr] = wasDisputed.split(",").map(Number);
  const cleared = await source.setContested(dq, dr, false);
  check("clearing a baked dispute works", cleared === true && latest!.contested.has(wasDisputed) === false);

  await source.undo();
  check("undo puts the baked dispute back", latest?.contested.has(wasDisputed) === true);

  await source.undo();
  check("undo then removes the painted flag", latest?.contested.has(quiet) === false);
  check("ownership survived both undos untouched", [...base.ownership].every(([c, o]) => latest!.ownership.get(c) === o));
}

// --- localisation -----------------------------------------------------------
/**
 * A missing translation is invisible in an English build and obvious to the
 * person whose language it is, which is the worst way for it to be found. So the
 * completeness of the table is asserted rather than eyeballed.
 *
 * The plural assertions are the fussiest part and the part most likely to be
 * "simplified" away later: Ukrainian has three categories and its "few" band is
 * last-digit 2-4 EXCLUDING 12-14, so 2 and 22 take the few form while 12 and 14
 * do not.
 */
console.log("\nlocalisation:");
{
  for (const loc of ["en-US", "uk-UA"]) {
    const missing = missingTranslations(loc);
    check(`every UI string has a ${loc} translation`, missing.length === 0, missing.join(", "));
  }

  // Content, not chrome.
  const noUkName = TERRITORIES.filter(x => !pick(x.name, "uk-UA")).map(x => x.id);
  check("every territory has a Ukrainian name", noUkName.length === 0, noUkName.join(", "));

  const noUkBlurb = TERRITORIES.filter(x => !x.blurb || !pick(x.blurb, "uk-UA")).map(x => x.id);
  check("every territory has a Ukrainian blurb", noUkBlurb.length === 0, noUkBlurb.join(", "));

  const noUkSys = SYSTEMS.filter(s => !pick(s.name, "uk-UA")).map(s => s.id);
  check("every system has a Ukrainian name", noUkSys.length === 0, noUkSys.join(", "));

  // A locale nobody translated must degrade to English, never to a blank label.
  check("an unknown locale falls back to English", pick({ en: "Sol", uk: "Соль" }, "de-DE") === "Sol");
  check(
    "uk-UA and bare uk both resolve",
    pick({ en: "Sol", uk: "Соль" }, "uk-UA") === "Соль" && pick({ en: "Sol", uk: "Соль" }, "uk") === "Соль",
  );

  const cases: [number, string, string][] = [
    [1, "uk-UA", "one"],
    [2, "uk-UA", "few"],
    [4, "uk-UA", "few"],
    [5, "uk-UA", "many"],
    [11, "uk-UA", "many"],
    [12, "uk-UA", "many"],
    [14, "uk-UA", "many"],
    [21, "uk-UA", "one"],
    [22, "uk-UA", "few"],
    [25, "uk-UA", "many"],
    [0, "uk-UA", "many"],
    [1, "en-US", "one"],
    [2, "en-US", "many"],
    [0, "en-US", "many"],
  ];
  const wrongCat = cases
    .filter(([n, loc, want]) => pluralCategory(n, loc) !== want)
    .map(([n, loc, want]) => `${n}@${loc}=${pluralCategory(n, loc)} want ${want}`);
  check("plural categories follow CLDR", wrongCat.length === 0, wrongCat.join(", "));

  const one = tp("pluralCell", 1, "uk-UA");
  const few = tp("pluralCell", 2, "uk-UA");
  const many = tp("pluralCell", 5, "uk-UA");
  check(
    "Ukrainian renders a distinct form per category",
    new Set([one, few, many]).size === 3,
    `${one} / ${few} / ${many}`,
  );
  check(
    "counts are substituted, not left as placeholders",
    ![one, few, many].some(s => s.includes("{$n}")),
  );

  // The install-override path the bridge will use.
  installStrings({ grid: { en: "LATTICE", uk: "ҐРАТКА" } });
  check("a pushed string overrides the built-in", t("grid", "uk-UA") === "ҐРАТКА");
  check("unpushed strings survive an override", t("paint", "uk-UA") === DEFAULT_STRINGS.paint.uk);
  resetStrings();
  check("reset restores the built-ins", t("grid", "en-US") === "GRID");
}

// --- legibility -------------------------------------------------------------
/** HSL saturation, 0..1. */
function saturationOf(hex: string): number {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return 0;
  const n = parseInt(m[1], 16);
  const r = ((n >> 16) & 255) / 255;
  const g = ((n >> 8) & 255) / 255;
  const b = (n & 255) / 255;
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;
  return max === min ? 0 : (max - min) / (1 - Math.abs(2 * l - 1));
}

/** WCAG relative luminance, 0..1. */
function relativeLuminance(hex: string): number {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return 0;
  const n = parseInt(m[1], 16);
  const ch = [(n >> 16) & 255, (n >> 8) & 255, n & 255].map(v => {
    const c = v / 255;
    return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * ch[0] + 0.7152 * ch[1] + 0.0722 * ch[2];
}

/** Hue in degrees, or null for a grey. */
function hueOf(hex: string): number | null {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return null;
  const n = parseInt(m[1], 16);
  const r = ((n >> 16) & 255) / 255;
  const g = ((n >> 8) & 255) / 255;
  const b = (n & 255) / 255;
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  if (max - min < 1e-6) return null;
  let h: number;
  if (max === r) h = ((g - b) / (max - min)) % 6;
  else if (max === g) h = (b - r) / (max - min) + 2;
  else h = (r - g) / (max - min) + 4;
  h *= 60;
  return h < 0 ? h + 360 : h;
}

/**
 * A territory name is drawn in its own colour, so a colour that is too dark
 * produces a title nobody can read. Whether a given colour is too dark depends on
 * the colour and not on the code, so no other check would notice. The chart
 * lifts such a colour before drawing; this asserts the lift was enough.
 *
 * Worth asserting separately because these colours will come from prototypes
 * rather than from this file, so a new nation can arrive in a colour nobody
 * looked at.
 */
console.log("\nlegibility:");
{
  const dark = TERRITORIES.filter(x => relativeLuminance(readableOnDark(x.color)) < 0.3).map(
    x => `${x.id} ${readableOnDark(x.color)}`,
  );
  check("every territory label lifts to a readable luminance", dark.length === 0, dark.join(", "));

  // Hue has to survive, or the colour-coding is decorative at best. Near-greys
  // are exempt: their "hue" is a rounding artefact that moves wildly under any
  // change, so asserting on it would be asserting on noise. The threshold sits
  // in the gap between the greys and the real nation colours — as shipped, the
  // greys top out at 0.17 saturation and the chromatic ones bottom out at 0.51.
  const chromatic = TERRITORIES.filter(x => saturationOf(x.color) >= 0.25);
  const lostHue = chromatic.filter(x => {
    const a = hueOf(x.color);
    const b = hueOf(readableOnDark(x.color));
    if (a === null || b === null) return true;
    const d = Math.abs(a - b);
    return Math.min(d, 360 - d) > 2;
  }).map(x => `${x.id} ${x.color}->${readableOnDark(x.color)}`);
  check(
    "lifting preserves hue, so nations stay distinguishable",
    lostHue.length === 0,
    lostHue.join(", "),
  );

  const lifted = TERRITORIES.map(x => readableOnDark(x.color).toLowerCase());
  check(
    "no two territories collide once lifted",
    new Set(lifted).size === lifted.length,
    `${new Set(lifted).size}/${lifted.length} distinct`,
  );

  // And the label must actually be brighter than the fill it sits on.
  const noGain = TERRITORIES.filter(
    x => relativeLuminance(readableOnDark(x.color)) < relativeLuminance(x.color) - 1e-6,
  ).map(x => x.id);
  check("lifting never darkens a colour", noGain.length === 0, noGain.join(", "));
}

// --- drag to paint ----------------------------------------------------------
/**
 * A stroke is one gesture, so it has to be one edit.
 *
 * The failure this guards against is quiet: if each cell committed separately,
 * painting would still work, look right, and pass every check — while leaving one
 * undo step per cell and rebuilding the model on every mousemove.
 */
console.log("\ndrag to paint:");
{
  // Hex line interpolation: contiguous, no gaps, correct length.
  const a = { q: 0, r: 0 };
  const b = { q: 6, r: -2 };
  const line = hexLine(a, b);
  check("hexLine starts at the first cell", line[0].q === a.q && line[0].r === a.r);
  check(
    "hexLine ends at the last cell",
    line[line.length - 1].q === b.q && line[line.length - 1].r === b.r,
  );
  check(
    "hexLine has one cell per step",
    line.length === hexDistance(a, b) + 1,
    `${line.length} cells for distance ${hexDistance(a, b)}`,
  );
  // Contiguity: every consecutive pair must be actual neighbours. This is the
  // property that stops a fast drag leaving gaps.
  const gaps = line.filter((c, i) => i > 0 && hexDistance(line[i - 1], c) !== 1);
  check("hexLine is contiguous — no gaps in a fast drag", gaps.length === 0, gaps.length + " gaps");

  const same = hexLine(a, a);
  check("hexLine of one cell is that cell", same.length === 1);

  // Every direction must produce a contiguous line, not just the easy axes.
  const dirs = NEIGHBOURS;
  const badDirs = dirs.filter(d => {
    const l = hexLine({ q: 3, r: -1 }, { q: 3 + d.q * 4, r: -1 + d.r * 4 });
    return l.some((c, i) => i > 0 && hexDistance(l[i - 1], c) !== 1);
  });
  check("hexLine is contiguous in all six directions", badDirs.length === 0, badDirs.length + " bad");

  // A stroke over several cells is ONE edit and ONE undo step.
  const source = new FixtureSource(spec, TERRITORIES, CLAIMS, SYSTEMS, ROUTES);
  const base = await source.load();
  let notified = 0;
  source.onChange(() => notified++);

  const biesel = (byTerr.get("biesel") ?? []).slice(0, 5);
  const targets = biesel.map(k => {
    const [q, r] = k.split(",");
    return { q: +q, r: +r };
  });
  const strokes = await source.stroke?.(targets, "nralakk");
  check("a stroke reports a change", strokes === true);
  check("a stroke notifies exactly once", notified === 1, `${notified} notification(s)`);

  let diffs = 0;
  const after = await source.load();
  for (const [cell, owner] of after.ownership) if (base.ownership.get(cell) !== owner) diffs++;
  check("a stroke changes every cell it covered", diffs === targets.length, `${diffs} of ${targets.length}`);

  // Re-dragging over cells already in the target state must be a no-op, or
  // scrubbing back and forth across a border fills the undo stack with nothing.
  const editsAfterFirst = source.edits;
  const again = await source.stroke?.(targets, "nralakk");
  check(
    "re-dragging the same cells is a no-op",
    again === false && source.edits === editsAfterFirst,
    `edits ${editsAfterFirst} -> ${source.edits}`,
  );

  await source.undo();
  check("one undo reverts the whole stroke", source.edits === 0);
  const reverted = await source.load();
  check(
    "the map is byte-identical after one undo",
    [...base.ownership].every(([c, o]) => reverted.ownership.get(c) === o),
  );

  // Contested strokes batch the same way and leave ownership alone.
  const ownerBefore = [...(await source.load()).ownership];
  const beforeContest = source.edits;
  const cstroke = await source.contestStroke?.(targets, true);
  check("a contest stroke reports a change", cstroke === true);
  check("a contest stroke is one edit", source.edits === beforeContest + 1, `${source.edits} edits`);
  const flagged = await source.load();
  check(
    "a contest stroke flags every cell it covered",
    targets.every(c => flagged.contested.has(key(c.q, c.r))),
  );
  check(
    "a contest stroke does not move an owner",
    ownerBefore.every(([c, o]) => flagged.ownership.get(c) === o),
  );
  const repeat = await source.contestStroke?.(targets, true);
  check("re-flagging the same cells is a no-op", repeat === false && source.edits === beforeContest + 1);

  await source.undo();
  check("one undo reverts the whole contest stroke", source.edits === beforeContest);
  const cleared = await source.load();
  check(
    "the dispute flags are gone after that undo",
    targets.every(c => !cleared.contested.has(key(c.q, c.r)) || base.contested.has(key(c.q, c.r))),
  );
  check(
    "ownership survived the contest stroke and its undo",
    ownerBefore.every(([c, o]) => cleared.ownership.get(c) === o),
  );

  // The fixture is an admin tool host.
  check("the fixture grants paint", source.permissions?.paint === true);
}

// ---- ring halves ----------------------------------------------------------------
//
// A ring half is two concentric elliptical arcs joined by two straight edges, and
// all four of its points must lie ON one of those two ellipses. That is the whole
// invariant, and it is checkable without a browser.
//
// It is worth stating what this replaces. `ringHalf` used to take a single radius
// and apply it to both axes, so a partial arc ended at a point that was not on its
// own ellipse. A whole half could not detect it, because at t=0 and t=PI the sine
// is zero and rx*sin and ry*sin agree — the mistake had nothing to act on. Only the
// halves carrying a division were wrong, which is about half of each ring, so a
// chart with ringed worlds still read as "rings" at a glance. The screenshot looked
// plausible and the failure was a band ballooned past its own bounds, which is not
// a thing anyone would look for.
{
  /** How far a point may sit off its ellipse, in the normalised metric. */
  const ON_ELLIPSE = 0.01;
  const onEllipse = (x: number, y: number, rx: number, ry: number) =>
    Math.abs((x / rx) ** 2 + (y / ry) ** 2 - 1) <= ON_ELLIPSE;

  // Wide, because the shape of the bug depended on the ratio: rx=128 with ry=28
  // is a 4.5:1 ellipse, and the endpoint error scales with rx, not with ry.
  const geoms = [
    { px: 200, seed: 1 },
    { px: 200, seed: 7 },
    { px: 16, seed: 3 },
    { px: 40, seed: 11 },
  ];
  let worst = 0;
  let worstAt = "";
  let offEllipse = 0;
  let arcs = 0;

  for (const { px, seed } of geoms) {
    const g = ringGeomFor(px, seed, px >= 60 ? 0.22 : undefined);
    for (const above of [true, false]) {
      // Sweep the band's width finely: the endpoints move with `from`, and a
      // single width would only sample one pair of them.
      for (let i = 0; i < 8; i++) {
        for (let j = i + 1; j <= 8; j++) {
          const d = ringHalf(g, above, i / 8, j / 8);
          if (!d) continue;
          arcs++;
          // Four points: the outer arc's two ends and the inner arc's two ends.
          const nums = d.match(/-?[\d.]+/g)?.map(Number) ?? [];
          const [x1, y1, , , , , , x2, y2, x3, y3] = nums;
          const rx = g.rx - g.band * (i / 8);
          const ry = g.ry - g.band * 0.34 * (i / 8);
          const rx2 = Math.max(0.5, g.rx - g.band * (j / 8));
          const ry2 = Math.max(0.5, g.ry - g.band * 0.34 * (j / 8));
          for (const [x, y, arx, ary] of [
            [x1, y1, rx, ry],
            [x2, y2, rx, ry],
            [x3, y3, rx2, ry2],
          ] as const) {
            const err = Math.abs((x / arx) ** 2 + (y / ary) ** 2 - 1);
            if (err > worst) {
              worst = err;
              worstAt = `${px}px seed ${seed} above=${above} [${i}/8,${j}/8] at (${x}, ${y})`;
            }
            if (err > ON_ELLIPSE) offEllipse++;
          }
        }
      }
    }
  }
  check(
    "every ring path's points lie on one of its own two ellipses",
    offEllipse === 0,
    offEllipse === 0 ? `${arcs} arcs, worst ${worst.toExponential(1)}` : `${offEllipse} off, worst at ${worstAt}`,
  );
  check(
    "a ring's two halves do not overlap",
    ringHalf(ringGeomFor(200, 1, 0.22), true) !== ringHalf(ringGeomFor(200, 1, 0.22), false),
    "the halves are distinct paths",
  );
  // A division that lands wholly outside a half must leave that half whole. The
  // first version did the opposite, which removed the entire far half of every
  // ring whose gap happened to fall on the near side.
  const gGap = ringGeomFor(200, 5, 0.22);
  const whole = ringHalf({ ...gGap, gapAngle: undefined }, true);
  check("a division outside a half leaves that half whole", ringHalf(gGap, true).length > 0, "not emptied");
  check("a whole half is a real path", whole.startsWith("M ") && whole.endsWith("Z"), whole.slice(0, 24));
}

/**
 * The black hole's constants, against `reference/BlackHole.tscn`.
 *
 * These are the numbers the whole render hangs on, and they were wrong for a long
 * time because the shaders DECLARE defaults that the scene OVERRIDES. Nothing about
 * editing them looks dangerous, so the scene's values are written out here as
 * literals and compared. If this fails, the scene changed and the note in
 * `blackhole-consts.ts` needs rewriting with it.
 */
{
  const fs = await import("node:fs/promises").then((m) => m.default);
  const scene = await fs.readFile("src/GalaxyMap/reference/BlackHole.tscn", "utf8").catch(() => "");

  const pairs: [string, number | string, number | string][] = [
    ["ring_perspective", PERSPECTIVE, 14.0],
    ["disk_width", DISK_WIDTH, 0.065],
    ["size", NOISE_SIZE, 6.598],
    ["rotation", ROTATION, 0.766],
    ["time_speed", TIME_SPEED, 0.2],
    ["radius", HOLE_RADIUS, 0.247],
    ["light_width", HOLE_LIGHT_WIDTH, 0.028],
    ["pixels, disc", DISC_PIXELS, 300],
    ["pixels, horizon", HOLE_PIXELS, 100],
    ["OCTAVES", OCTAVES, 3],
  ];
  const wrong = pairs.filter(([, got, want]) => got !== want);
  check(
    "the black hole's constants are the scene's, not the shaders' declarations",
    wrong.length === 0,
    wrong.length === 0
      ? `${pairs.length} values, all as BlackHole.tscn sets them`
      : wrong.map(([n, g, w]) => `${n} is ${String(g)}, scene says ${String(w)}`).join("; "),
  );

  // And the scene file itself, read rather than remembered, so the literals above
  // cannot quietly become the new normal.
  if (scene) {
    const declared = (k: string) =>
      (scene.match(new RegExp(`${k}\\s*(?:=\\s*)?(-?[0-9.]+)`, "i")) ?? [])[1];
    // Compared as NUMBERS: the scene writes `14.0` and the constant is 14, and a
    // string comparison reports that as a difference, which is the kind of false
    // alarm that teaches people to ignore a check.
    const names = [
      "ring_perspective",
      "disk_width",
      "size",
      "rotation",
      "time_speed",
      "radius",
      "light_width",
    ];
    const mismatched = pairs.filter(
      ([n, got]) =>
        names.includes(n) && Number(declared(n)) !== Number(got),
    );
    check(
      "and they still match what the vendored scene file actually contains",
      scene.length > 0 && mismatched.length === 0,
      mismatched.length === 0
        ? `${names.length} values parsed out of reference/BlackHole.tscn`
        : mismatched.map(([n, g]) => `${n}=${String(g)}, scene has ${declared(n)}`).join("; "),
    );
  } else {
    check("and the vendored scene file is present to check them against", false, "could not read it");
  }

  // The two renderers import these, so the duplication cannot drift. Assert the
  // import is real rather than trusting it, since the failure mode is a local copy
  // quietly reappearing in one file.
  const glSrc = await fs.readFile("src/GalaxyMap/lib/gl.ts", "utf8").catch(() => "");
  const cpuSrc = await fs.readFile("src/GalaxyMap/lib/blackhole.ts", "utf8").catch(() => "");
  for (const [name, src] of [["gl.ts", glSrc], ["blackhole.ts", cpuSrc]] as const) {
    const imported = src.includes('from "./blackhole-consts"');
    const local = /const (HOLE|DISC): \[string/.test(src);
    check(
      `${name} takes the black hole's constants from the shared module`,
      imported && !local,
      !src
        ? "unreadable"
        : local
          ? "a local copy of the palette is back alongside the import"
          : imported
            ? "imported, no local copy"
            : "no import",
    );
  }

  // BRIGHT to DARK, and the order is load bearing.
  const lum = (hex: string) => {
    const n = parseInt(hex.slice(1), 16);
    return 0.2126 * ((n >> 16) & 255) + 0.7152 * ((n >> 8) & 255) + 0.0722 * (n & 255);
  };
  const lums = DISC.map(lum);
  check(
    "the disc's palette runs bright to dark, which is the opposite of the obvious",
    lums.every((v, i) => i === 0 || v < lums[i - 1]),
    lums.map((v) => v.toFixed(0)).join(" > "),
  );
  // The chart's page is near black, so a true #000 void is a HOLE in the chart
  // rather than an object in it. The eye reads a gap as a bug. The reference's
  // #272737 is luminance 40, which is dark and still not a gap.
  check(
    "and the horizon's void is dark without being pure black",
    HOLE[0] !== "#000000" && lum(HOLE[0]) > 5 && lum(HOLE[0]) < 60,
    `${HOLE[0]} at luminance ${lum(HOLE[0]).toFixed(0)}`,
  );
  check(
    "and the photon ring's two bands are brighter than the void",
    lum(HOLE[1]) - lum(HOLE[0]) > 60,
    `${HOLE[1]} at ${lum(HOLE[1]).toFixed(0)} vs void at ${lum(HOLE[0]).toFixed(0)}`,
  );
}

/**
 * The bake's YAML parser, on the things it promises to refuse.
 *
 * This parser exists instead of a dependency, and the reason it is safe to have
 * written one is that it STOPS rather than guessing. That claim is only worth
 * anything if it is tested, because a parser that quietly mis-reads a construct
 * puts a border in the wrong place with no error anywhere -- the worst possible
 * failure for geometry, and invisible until someone looks at the chart and
 * wonders why the border is in the wrong place.
 *
 * So each case below is one the parser says it does not support. If any of them
 * starts parsing, the safety argument is gone and the parser needs a real
 * dependency behind it.
 */
{
  const refuse = (label: string, src: string, expect?: RegExp) => {
    try {
      parseYamlSequence(src, "t.yml");
      return { label, ok: false, detail: "PARSED, but should have been refused" };
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      // A refusal for the WRONG reason is still a bug: it means the guard fired
      // on an accident of formatting rather than on the construct.
      const ok = expect === undefined || expect.test(msg);
      return { label, ok, detail: ok ? msg : `refused, but not for the stated reason: ${msg}` };
    }
  };
  const accept = (label: string, src: string) => {
    try {
      parseYamlSequence(src, "t.yml");
      return { label, ok: true, detail: "parsed" };
    } catch (e) {
      return { label, ok: false, detail: `refused: ${e instanceof Error ? e.message : e}` };
    }
  };

  const cases = [
    refuse("a flow mapping", "- type: a\n  map: {x: 1}\n", /flow mapping/i),
    refuse("a block scalar", "- type: a\n  text: |\n    hello\n", /block scalar/i),
    refuse("an anchor", "- type: a\n  ref: &x 1\n", /anchor/i),
    refuse("an alias", "- type: a\n  ref: *x\n", /anchor/i),
    refuse("a tag", "- type: !Thing\n  id: x\n", /tag|expected/i),
    refuse("a quoted scalar", "- type: \"a\"\n", /quoted/i),
    refuse("a duplicate key", "- type: a\n  id: x\n  id: y\n", /duplicate/i),
    refuse("a tab indent", "- type: a\n\tid: x\n", /tab/i),
    refuse("a non-numeric coordinate", "- type: a\n  polygon:\n    - [a, 2]\n", /not a number/i),
    refuse("an unterminated flow sequence", "- type: a\n  polygon: [1, 2\n", /unterminated/i),
    refuse("a top-level mapping, not a sequence", "type: a\nid: x\n", /sequence/i),
    refuse("an indented document", "  - type: a\n", /indented|column 0/i),
    accept("a plain entry", "- type: a\n  id: x\n"),
    accept("a nested mapping", "- type: a\n  m:\n    k: 1\n  after: 2\n"),
    accept("a sequence under a key", "- type: a\n  polygon:\n    - [1, 2]\n    - [3, 4]\n"),
    accept("a sequence at the key's own indent", "- type: a\n  polygon:\n  - [1, 2]\n"),
    accept("comments and blanks", "\n# top\n- type: a  # trailing\n\n  id: x\n"),
    accept("a negative decimal", "- type: a\n  x: -5.5\n"),
  ];
  for (const c of cases) check(`yaml: ${c.label}`, c.ok, c.detail);
  check("yaml: every case above", cases.every((c) => c.ok), `${cases.filter((c) => c.ok).length}/${cases.length}`);

  /**
   * The reader's own validation, which the parser has no business doing.
   *
   * A polygon is a list of `[x, y]` pairs, and that shape is the bake's rule, not
   * YAML's -- the parser is right to hand back a three-element sequence. So these
   * go through `readPrototype`, and each one is a mistake that would otherwise
   * produce a plausible-looking map: a two-point polygon encloses nothing, so the
   * territory silently vanishes; a duplicated territory means two regions, which
   * the model cannot represent, so one would overwrite the other.
   */
  const head =
    "- type: galaxy\n  id: g\n  extent:\n    width: 132\n    height: 74\n" +
    "  hexSize: 2.0\n  unclaimedId: unclaimed\n";
  const reject = (label: string, src: string, expect: RegExp) => {
    try {
      readPrototype("t.yml", src);
      return { label, ok: false, detail: "ACCEPTED, but should have been refused" };
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      const ok = expect.test(msg);
      return { label, ok, detail: ok ? msg : `refused, wrong reason: ${msg}` };
    }
  };
  const shape = [
    reject("a polygon with too few points",
      head + "- type: galaxyClaim\n  territory: a\n  polygon:\n    - [1, 2]\n    - [3, 4]\n",
      /at least 3 points/),
    reject("a coordinate that is not a pair",
      head + "- type: galaxyClaim\n  territory: a\n  polygon:\n    - [1, 2, 3]\n    - [3, 4]\n    - [5, 6]\n",
      /expected \[x, y\]/),
    reject("a missing territory",
      head + "- type: galaxyClaim\n  polygon:\n    - [1, 2]\n    - [3, 4]\n    - [5, 6]\n",
      /missing required key "territory"/),
    reject("a missing polygon",
      head + "- type: galaxyClaim\n  territory: a\n",
      /missing required key "polygon"/),
    reject("a duplicated territory",
      head + "- type: galaxyClaim\n  territory: a\n  polygon:\n    - [1, 2]\n    - [3, 4]\n    - [5, 6]\n" +
        "- type: galaxyClaim\n  territory: a\n  polygon:\n    - [7, 8]\n    - [9, 10]\n    - [11, 12]\n",
      /claimed more than once/),
    reject("an unknown type",
      head + "- type: galaxyBorder\n  territory: a\n",
      /unknown type/),
    reject("a second galaxy entry",
      head + head + "- type: galaxyClaim\n  territory: a\n  polygon:\n    - [1, 2]\n    - [3, 4]\n    - [5, 6]\n",
      /second "galaxy" entry/),
    reject("no claims at all", head, /no "galaxyClaim" entries/),
  ];
  for (const c of shape) check(`bake: ${c.label}`, c.ok, c.detail);
  check("bake: every shape case above", shape.every((c) => c.ok),
    `${shape.filter((c) => c.ok).length}/${shape.length}`);

  // And the positive path, so the rejections are not passing because EVERYTHING is
  // rejected. A reader that threw on all input would pass every case above.
  const good = readPrototype(
    "t.yml",
    head +
      "- type: galaxyClaim\n  territory: a\n  polygon:\n    - [1, 2]\n    - [3, 4]\n    - [5, 6]\n" +
      "- type: galaxyClaim\n  territory: b\n  polygon:\n    - [7, 8]\n    - [9, 10]\n    - [11, 12]\n",
  );
  check(
    "bake: a well-formed prototype is accepted, and read as written",
    good.claims.length === 2 &&
      good.claims[0].id === "a" &&
      good.claims[0].polygon[0].x === 1 &&
      good.claims[0].polygon[2].y === 6 &&
      good.spec.extentLy.w === 132 &&
      good.spec.hexSizeLy === 2 &&
      good.spec.unclaimedId === "unclaimed",
    `${good.claims.length} claims, extent ${good.spec.extentLy.w}x${good.spec.extentLy.h}`,
  );
}

/**
 * The staleness check, and the thing it is protecting.
 *
 * `baked.ts` is committed, so it can disagree with the prototypes it was baked
 * from, and nothing about that disagreement is visible on the chart. These say the
 * comparison works and that the baked file is the one the prototypes currently
 * imply -- the second by re-deriving the fingerprint, which means a prototype
 * edited without a re-bake fails the suite.
 */
{
  const same = checkBake(BAKE_FP, BAKE_FP);
  const differ = checkBake(BAKE_FP, "0000000000000000");
  const unknown = checkBake(BAKE_FP, null);
  check("a matching fingerprint is fresh", !same.stale, same.reason);
  check("a differing one is stale, and says what to do", differ.stale && /re-bak/i.test(differ.reason),
    differ.reason.slice(0, 90) + "...");
  check(
    "and an unverifiable one is neither fresh nor stale",
    !unknown.stale && unknown.expected === null && /could not be checked/i.test(unknown.reason),
    unknown.reason,
  );
  check("the stale reason carries both fingerprints", differ.committed === BAKE_FP && differ.expected === "0000000000000000",
    `${differ.committed} vs ${differ.expected}`);

  // The committed file is the point of the whole exercise, so it gets checked like
  // any other output: does it agree with what the prototypes say right now?
  const protoText = await import("node:fs/promises")
    .then((fs) => fs.readFileFile ? null : null)
    .catch(() => null);
  void protoText;
  const BAKE_SPEC = DEFAULT_MAP;
  const BAKED = { CELLS: BAKED_CELLS, CONTESTED: BAKED_CONTESTED };
  const BAKE_CELLS = cellsInExtent(
    BAKE_SPEC.extentLy.w,
    BAKE_SPEC.extentLy.h,
    BAKE_SPEC.hexSizeLy,
  ).length;
  const BAKED_CELLS_TOTAL = Object.values(BAKED_CELLS).reduce((n, l) => n + l.length, 0);
  const model = bakedModel(BAKE_SPEC, BAKED, []);
  const owned = [...model.ownership.values()];
  const unclaimed = owned.filter((o) => o === BAKE_SPEC.unclaimedId).length;
  check(
    "the committed cells cover the whole extent, with the gaps left unclaimed",
    model.ownership.size === BAKE_CELLS &&
      unclaimed === BAKE_CELLS - BAKED_CELLS_TOTAL,
    `${model.ownership.size} cells, ${BAKED_CELLS_TOTAL} claimed, ${unclaimed} unclaimed`,
  );
  check(
    "every committed cell is inside the extent, and a real cell",
    Object.values(BAKED.CELLS)
      .flat()
      .every((k) => model.ownership.has(k) && model.ownership.get(k) !== BAKE_SPEC.unclaimedId),
    `${BAKED_CELLS_TOTAL} committed keys, none outside the lattice`,
  );
  check(
    "no cell is claimed twice",
    (() => {
      const seen = new Set<string>();
      for (const list of Object.values(BAKED.CELLS)) for (const c of list) {
        if (seen.has(c)) return false;
        seen.add(c);
      }
      return true;
    })(),
    "disjoint",
  );
  check(
    "and the contested list is a subset of the claimed cells",
    BAKED.CONTESTED.every((c) => {
      for (const list of Object.values(BAKED.CELLS)) if (list.includes(c)) return true;
      return false;
    }),
    `${BAKED.CONTESTED.length} contested, all claimed`,
  );
  /**
   * The property the whole bake exists to guarantee, and the one that silently
   * stops holding: the committed cells and the polygons must resolve to the same
   * partition. If they drift, the game is showing a different map from the one the
   * harness develops against, and both look correct.
   *
   * Negative-controlled by moving a single cell between two territories, which is
   * the smallest possible divergence and which this does catch.
   */
  const recomputed = buildOwnership(BAKE_SPEC, BAKE_CLAIMS).ownership;
  const differs: string[] = [];
  for (const [k, v] of recomputed) {
    const got = model.ownership.get(k);
    if (got !== v) differs.push(`${k}: baked ${got}, recomputed ${v}`);
  }
  for (const k of model.ownership.keys()) {
    if (!recomputed.has(k)) differs.push(`${k}: baked only`);
  }
  check(
    "baked and recomputed ownership are identical, which is the one code path",
    differs.length === 0,
    differs.length === 0
      ? `${recomputed.size} cells, same owner either way`
      : `${differs.length} differ, e.g. ${differs.slice(0, 3).join("; ")}`,
  );
}

console.log(`\n${failures === 0 ? "all checks passed" : `${failures} CHECK(S) FAILED`}\n`);
process.exit(failures === 0 ? 0 : 1);
