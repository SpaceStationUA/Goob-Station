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
import { DEFAULT_MAP, FixtureSource } from "../src/GalaxyMap/lib/source";
import { CLAIMS, ROUTES, SYSTEMS, TERRITORIES } from "../src/GalaxyMap/lib/devmap";
import type { Vec2 } from "../src/GalaxyMap/lib/hex";
import { pick, type GalaxyModel } from "../src/GalaxyMap/lib/model";
import { readableOnDark } from "../src/GalaxyMap/Chart";
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

console.log(`\n${failures === 0 ? "all checks passed" : `${failures} CHECK(S) FAILED`}\n`);
process.exit(failures === 0 ? 0 : 1);
