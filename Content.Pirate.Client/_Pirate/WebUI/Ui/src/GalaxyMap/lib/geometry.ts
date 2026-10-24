import { type Axial, type Vec2, hexCorners, hexToPixel, key, neighbour, pixelToHex } from "./hex";
import type { GalaxyModel, Ownership, TerritoryClaim } from "./model";

/* ------------------------------------------------------------------ *
 * Polygons -> cells
 * ------------------------------------------------------------------ */

/** Ray-casting point-in-polygon. Edges and vertices are not special-cased. */
export function pointInPolygon(p: Vec2, poly: Vec2[]): boolean {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[i];
    const b = poly[j];
    if (a.y > p.y !== b.y > p.y && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) {
      inside = !inside;
    }
  }
  return inside;
}

export interface AssignResult {
  ownership: Ownership;
  /**
   * Cells claimed by more than one polygon — contested ground. Expected where
   * two nations share a border, since claims are drawn by hand and cannot
   * perfectly tile. See `resolveContested`.
   */
  contested: { cell: Axial; claimants: string[] }[];
  /** Cells no claim covered. These are unclaimed space, not an error. */
  unclaimedCells: number;
}

/** Shortest distance from a point to a polygon's outline. */
export function distToPolygon(p: Vec2, poly: Vec2[]): number {
  let best = Infinity;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[i];
    const b = poly[j];
    const vx = b.x - a.x;
    const vy = b.y - a.y;
    const L = vx * vx + vy * vy;
    let t = L > 0 ? ((p.x - a.x) * vx + (p.y - a.y) * vy) / L : 0;
    t = Math.max(0, Math.min(1, t));
    best = Math.min(best, Math.hypot(a.x + vx * t - p.x, a.y + vy * t - p.y));
  }
  return best;
}

/**
 * Turn hand-drawn claims into a partition.
 *
 * Three rules, and only three:
 *
 *   - claimed by one   -> it wins
 *   - claimed by many  -> contested; the cell goes to the claim it sits
 *                        DEEPEST inside (largest distance to that outline).
 *                        Overlapping claims are how two nations share a border
 *                        when drawn by hand, and this rule splits the contested
 *                        band down the middle, which is what produces the
 *                        clean shared edge.
 *   - claimed by none  -> unclaimed space
 *
 * Requiring claims not to overlap does not work: two outlines a fraction of a
 * light-year apart still leave a cell centre in the gap, and a single unclaimed
 * cell severs the shared border along its entire length.
 */
export function assignCells(
  claims: TerritoryClaim[],
  cells: Axial[],
  hexSizeLy: number,
  unclaimedId = "",
): AssignResult {
  const ownership: Ownership = new Map();
  const contested: AssignResult["contested"] = [];
  let unclaimedCells = 0;

  for (const cell of cells) {
    const centre = hexToPixel(cell, hexSizeLy);
    const k = key(cell.q, cell.r);

    const hits: TerritoryClaim[] = [];
    for (const claim of claims) {
      if (pointInPolygon(centre, claim.polygon)) hits.push(claim);
    }

    if (hits.length === 0) {
      ownership.set(k, unclaimedId);
      unclaimedCells++;
      continue;
    }

    if (hits.length === 1) {
      ownership.set(k, hits[0].id);
      continue;
    }

    contested.push({ cell, claimants: hits.map(h => h.id) });
    let best = hits[0];
    let bestDepth = -1;
    for (const claim of hits) {
      const depth = distToPolygon(centre, claim.polygon);
      if (depth > bestDepth) {
        bestDepth = depth;
        best = claim;
      }
    }
    ownership.set(k, best.id);
  }

  return { ownership, contested, unclaimedCells };
}

/* ------------------------------------------------------------------ *
 * Cell sets -> outlines
 * ------------------------------------------------------------------ */

interface Edge {
  a: Vec2;
  b: Vec2;
}

const QUANT = 1e4;
const ptKey = (p: Vec2) => `${Math.round(p.x * QUANT)},${Math.round(p.y * QUANT)}`;
/** Unordered endpoint key: reaching an edge from the far cell must hash the same. */
const edgeKey = (a: Vec2, b: Vec2) => {
  const ka = ptKey(a);
  const kb = ptKey(b);
  return ka < kb ? `${ka}|${kb}` : `${kb}|${ka}`;
};

export interface Chain {
  loop: Vec2[];
  /** The edges that formed it, in walk order. */
  edges: Edge[];
  /** True when the walk came back to where it started. See the note below. */
  closed: boolean;
}

/**
 * Stitch boundary edges into closed loops. Holes come back as their own loop.
 *
 * Matching is tolerant of direction: a chain may continue from either end of an
 * edge. Adjacent hexes wind their own edges the same way, but once a chain has
 * picked an orientation the next edge may be stored pointing back along it, and
 * reversing on the fly beats normalising every edge up front.
 *
 * `keyOf` matters more than it looks. Where three cells meet with three
 * *different* owners — three nations at a point — three border edges share the
 * vertex, and picking a turn arbitrarily splits one border into fragments that
 * dead-end instead of closing. Supplying a key (for borders, the unordered pair
 * of territories the edge separates) lets the walk keep following the same
 * border through the junction, which is both topologically right and the
 * straight-through choice visually.
 */
function chainEdges(edges: Edge[], keyOf?: (e: Edge) => string | undefined): Chain[] {
  const byPoint = new Map<string, Edge[]>();
  const add = (p: Vec2, e: Edge) => {
    const k = ptKey(p);
    const list = byPoint.get(k);
    if (list) list.push(e);
    else byPoint.set(k, [e]);
  };
  for (const e of edges) {
    add(e.a, e);
    add(e.b, e);
  }

  const used = new Set<Edge>();
  const chains: Chain[] = [];

  for (const seed of edges) {
    if (used.has(seed)) continue;
    const startPt = seed.a;
    const startKey = keyOf?.(seed);
    const loop: Vec2[] = [startPt];
    const walked: Edge[] = [];
    let at = startPt;
    let cur = seed;

    // Bounded by the edge count so a malformed set can never spin here.
    for (let guard = 0; guard <= edges.length; guard++) {
      used.add(cur);
      walked.push(cur);
      at = ptKey(cur.a) === ptKey(at) ? cur.b : cur.a;
      loop.push(at);
      if (ptKey(at) === ptKey(startPt)) break;

      const candidates = (byPoint.get(ptKey(at)) ?? []).filter(e => !used.has(e));
      if (candidates.length === 0) break;
      let next: Edge;
      if (startKey === undefined) {
        next = candidates[0];
      } else {
        // Only continue along an edge separating the same two territories.
        // Where three nations meet, no such edge exists — the border genuinely
        // terminates at that vertex — so stop instead of guessing. Falling back
        // to "any candidate" lets a walk hop onto a different nation's border
        // and carry on, which traces one outline and then draws a long straight
        // chord across the map to somewhere else entirely. The edges left here
        // are picked up by their own pair's chains, and since the branches all
        // share this vertex the drawn result stays continuous.
        const match = candidates.find(e => keyOf?.(e) === startKey);
        if (!match) break;
        next = match;
      }
      cur = next;
    }
    // Two points is a single-edge chain, which is a legitimate border segment
    // where one nation touches another along exactly one hex edge. Dropping
    // those silently leaves a hole in the map.
    if (loop.length >= 2) {
      chains.push({
        loop,
        edges: walked,
        closed: ptKey(loop[loop.length - 1]) === ptKey(startPt),
      });
    }
  }
  return chains;
}

export interface OutlineOptions {
  /**
   * Lattice-corner rounding radius, in light-years. This is what turns the hex
   * staircase into something that reads as geography.
   *
   * Applied to real corners ONLY, never to the wobble samples. Rounding every
   * vertex of an already-wobbled polyline collapses the outline into a chain of
   * lozenges, which is a distinctive and entirely wrong-looking failure mode.
   */
  roundLy: number;
  /** Sample count per rounded corner. 3-5 is plenty; the output is a polyline. */
  arcSamples: number;
  /** Peak displacement of the coastline wobble, in light-years. 0 disables. */
  wobbleLy: number;
  /** Sample spacing of the wobble, in light-years. */
  wobbleStepLy: number;
  /**
   * How often the wobble repeats along a border, in light-years. This is the
   * knob that matters most for how the map reads.
   *
   * A straight edge of a hand-drawn claim quantises into a long staircase of hex
   * cells, and the corner rounding then flattens that staircase into a perfectly
   * straight line — dead straight for thirty light-years, which looks nothing
   * like a coastline. Driving the wobble from distance along the border rather
   * than from the point index is what breaks those long runs up: the noise is
   * coherent over several light-years, so a straight run bends into a slow S
   * instead of jittering imperceptibly.
   */
  wobbleWavelengthLy: number;
  /** Varies the wobble without moving the lattice. Must be stable across clients. */
  seed: number;
}

export const DEFAULT_OUTLINE: OutlineOptions = {
  roundLy: 0.34,
  arcSamples: 4,
  wobbleLy: 0.3,
  wobbleStepLy: 0.5,
  wobbleWavelengthLy: 9,
  seed: 1337,
};

/**
 * Smoothed closed loops of a cell set.
 *
 * Order matters: corners are rounded into a dense polyline first and the wobble
 * is applied to *that*. The other way round leaves the rounding to chew on the
 * wobble samples.
 */
export function cellOutline(
  cells: Set<string>,
  hexSizeLy: number,
  opts: OutlineOptions = DEFAULT_OUTLINE,
): Vec2[][] {
  return chainEdges(boundaryEdges(cells, hexSizeLy)).map(c => {
    const rounded = opts.roundLy > 0 ? roundCorners(c.loop, opts.roundLy, opts.arcSamples) : c.loop;
    return opts.wobbleLy > 0 ? wobble(rounded, opts) : rounded;
  });
}

/**
 * Replace each corner of a closed loop with a sampled quadratic arc.
 *
 * Emitting a dense polyline rather than SVG path commands keeps the pipeline
 * uniform: fills, borders and the wobble all operate on plain point lists, and
 * a border chain stays geometrically identical to the fill edge it sits on.
 */
export function roundCorners(loop: Vec2[], radius: number, samples: number): Vec2[] {
  const n = loop.length;
  if (n < 3) return loop.slice();
  const out: Vec2[] = [];

  for (let i = 0; i < n; i++) {
    const prev = loop[(i - 1 + n) % n];
    const cur = loop[i];
    const next = loop[(i + 1) % n];

    const dIn = Math.hypot(cur.x - prev.x, cur.y - prev.y);
    const dOut = Math.hypot(next.x - cur.x, next.y - cur.y);
    const r = Math.min(radius, dIn / 2, dOut / 2);
    if (r <= 1e-4) {
      out.push(cur);
      continue;
    }

    const a = { x: cur.x + ((prev.x - cur.x) / dIn) * r, y: cur.y + ((prev.y - cur.y) / dIn) * r };
    const b = { x: cur.x + ((next.x - cur.x) / dOut) * r, y: cur.y + ((next.y - cur.y) / dOut) * r };
    out.push(a);
    for (let s = 1; s < samples; s++) {
      const t = s / samples;
      const it = 1 - t;
      out.push({
        x: it * it * a.x + 2 * it * t * cur.x + t * t * b.x,
        y: it * it * a.y + 2 * it * t * cur.y + t * t * b.y,
      });
    }
    out.push(b);
  }
  return out;
}

/** Edges of `cells` facing a cell outside the set. Scaffolding only, never drawn. */
function boundaryEdges(cells: Set<string>, hexSizeLy: number): Edge[] {
  const out: Edge[] = [];
  for (const k of cells) {
    const [q, r] = k.split(",");
    const cell: Axial = { q: +q, r: +r };
    const corners = hexCorners(hexToPixel(cell, hexSizeLy), hexSizeLy);
    for (let e = 0; e < 6; e++) {
      const nb = neighbour(cell, e);
      if (cells.has(key(nb.q, nb.r))) continue;
      out.push({ a: corners[e], b: corners[(e + 1) % 6] });
    }
  }
  return out;
}

/* ------------------------------------------------------------------ *
 * Borders
 * ------------------------------------------------------------------ */

export interface BorderLoop {
  loop: Vec2[];
  /**
   * The chain as it came off the lattice, before rounding and wobble.
   *
   * Kept because it is the only honest place to assert the chain is sound: the
   * smoothed loop subdivides every segment to roughly `wobbleStepLy`, so a chain
   * that jumped between unrelated vertices still comes out as a long run of
   * short, entirely plausible steps.
   */
  raw: Vec2[];
  /**
   * Whether the chain closed on itself. Where three nations meet at a lattice
   * point the border network has an odd-degree vertex and a walk may dead-end;
   * such a chain MUST be emitted without `Z`, or the closing command draws a
   * straight line from its end back to its start across the whole chart.
   */
  closed: boolean;
  /** The two territories this loop separates. */
  owners: [string, string];
}

/**
 * Edges separating two *different* owners — the visible political borders.
 *
 * Deliberately NOT derived per territory. A shared border computed once cannot
 * disagree with itself, which is what keeps the map watertight however the fill
 * underneath is smoothed. Each geometric edge is emitted exactly once, keyed by
 * its unordered endpoints, so the two cells sharing it contribute one edge
 * between them rather than two.
 */
export function borderLoops(
  model: GalaxyModel,
  opts: OutlineOptions = DEFAULT_OUTLINE,
): BorderLoop[] {
  const edges: Edge[] = [];
  // Both sides of every border edge, not just the contributing cell's owner.
  // A territory that fully encloses an enclave contributes every edge of that
  // border itself, so attributing to the contributor alone would report the
  // chain as bordering itself.
  const sidesOf = new Map<string, [string, string]>();
  const seen = new Set<string>();

  for (const [cellId, owner] of model.ownership) {
    const [q, r] = cellId.split(",");
    const cell: Axial = { q: +q, r: +r };
    const corners = hexCorners(hexToPixel(cell, model.hexSizeLy), model.hexSizeLy);

    for (let e = 0; e < 6; e++) {
      const nb = neighbour(cell, e);
      const other = model.ownership.get(key(nb.q, nb.r));
      if (other === undefined || other === owner) continue;

      const a = corners[e];
      const b = corners[(e + 1) % 6];
      const ek = edgeKey(a, b);
      if (seen.has(ek)) continue;
      seen.add(ek);
      edges.push({ a, b });
      sidesOf.set(ek, [owner, other]);
    }
  }

  return chainEdges(edges, e => {
    const pair = sidesOf.get(edgeKey(e.a, e.b));
    return pair ? [pair[0], pair[1]].sort().join("|") : undefined;
  }).map(chain => {
    // A border separates exactly two territories, but read them off the edges
    // rather than assuming a winding order.
    const ids: string[] = [];
    for (const e of chain.edges) {
      for (const id of sidesOf.get(edgeKey(e.a, e.b)) ?? []) {
        if (!ids.includes(id)) ids.push(id);
      }
    }
    // Same rounding-then-wobble pipeline as the fills, so a border traces the
    // exact edge of the two territories it separates.
    const rounded = opts.roundLy > 0 ? roundCorners(chain.loop, opts.roundLy, opts.arcSamples) : chain.loop;
    return {
      raw: chain.loop,
      loop: opts.wobbleLy > 0 ? wobble(rounded, opts) : rounded,
      closed: chain.closed,
      owners: [ids[0] ?? "", ids[1] ?? ids[0] ?? ""],
    };
  });
}

/* ------------------------------------------------------------------ *
 * Smoothing
 * ------------------------------------------------------------------ */

/** Deterministic 1D value noise. Same inputs -> same curve on every client. */
function hash1(i: number, seed: number): number {
  let h = (i * 374761393 + seed * 668265263) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return ((h >>> 0) / 4294967295) * 2 - 1;
}

function valueNoise(t: number, seed: number): number {
  const i = Math.floor(t);
  const s = t - i;
  const sm = s * s * (3 - 2 * s);
  return hash1(i, seed) * (1 - sm) + hash1(i + 1, seed) * sm;
}

/**
 * Subdivide each segment and push interior points along the segment normal.
 * Vertices stay put; corners are rounded separately by the path emitter.
 *
 * Walking the loop continuously and driving the noise from distance travelled —
 * not from the point index — is what makes this read as coastline. Index-driven
 * noise is uncorrelated between neighbouring samples, so it can only ever add
 * sub-pixel jitter; it cannot bend a long straight run, which is precisely the
 * case that needs bending. Distance is measured along this loop, which is
 * deterministic, so every client computes an identical curve.
 */
function wobble(loop: Vec2[], opts: OutlineOptions): Vec2[] {
  const out: Vec2[] = [];
  const n = loop.length;
  const wavelength = Math.max(1e-3, opts.wobbleWavelengthLy);
  let travelled = 0;

  for (let i = 0; i < n; i++) {
    const p = loop[i];
    const next = loop[(i + 1) % n];
    out.push(p);
    const dx = next.x - p.x;
    const dy = next.y - p.y;
    const len = Math.hypot(dx, dy);
    const steps = Math.max(1, Math.round(len / opts.wobbleStepLy));
    if (steps === 1) continue;
    const nx = -dy / len;
    const ny = dx / len;
    for (let s = 1; s < steps; s++) {
      const t = s / steps;
      const along = travelled + len * t;
      // Two octaves: a slow primary bend plus a finer ripple riding on it.
      const d =
        (valueNoise(along / wavelength, opts.seed) +
          0.4 * valueNoise(along / (wavelength / 3.5), opts.seed + 91)) *
        opts.wobbleLy;
      out.push({ x: p.x + dx * t + nx * d, y: p.y + dy * t + ny * d });
    }
    travelled += len;
  }
  return out;
}

/* ------------------------------------------------------------------ *
 * SVG path emission
 * ------------------------------------------------------------------ */

const f = (n: number) => (Math.abs(n) < 1e-4 ? "0" : n.toFixed(3));

/**
 * Point list -> SVG path.
 *
 * No rounding happens here. Loops arrive already smoothed by
 * `cellOutline`/`borderLoops`, and rounding again at draw time would
 * reintroduce exactly the lozenge artefact the pipeline exists to avoid.
 */
export function loopToPath(loop: Vec2[], close = true): string {
  if (loop.length < 2) return "";
  const head = `M${f(loop[0].x)},${f(loop[0].y)}`;
  const body = loop.slice(1).map(p => `L${f(p.x)},${f(p.y)}`).join("");
  // Only emit Z when the caller says the loop is genuinely closed. A two-point
  // chain is a single edge, and Z on it is a no-op at best.
  return head + body + (close && loop.length > 2 ? "Z" : "");
}

/** Open polyline -> SVG path. Used for border chains and routes. */
export function polylineToPath(loop: Vec2[]): string {
  if (loop.length < 2) return "";
  return `M${f(loop[0].x)},${f(loop[0].y)}` + loop.slice(1).map(p => `L${f(p.x)},${f(p.y)}`).join("");
}

/* ------------------------------------------------------------------ *
 * Derived views
 * ------------------------------------------------------------------ */

/** Cell key -> territory id, inverted into territory id -> cell keys. */
export function cellsByTerritory(ownership: Ownership): Map<string, string[]> {
  const out = new Map<string, string[]>();
  for (const [cell, id] of ownership) {
    const list = out.get(id);
    if (list) list.push(cell);
    else out.set(id, [cell]);
  }
  return out;
}

/** Which territory owns the cell at a point, for hit testing. */
export function territoryAt(model: GalaxyModel, p: Vec2): string | undefined {
  const cell = pixelToHex(p, model.hexSizeLy);
  return model.ownership.get(key(cell.q, cell.r));
}
