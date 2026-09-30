import { createEffect, createMemo, createSignal, For, onCleanup, Show } from "solid-js";
import { cellsInExtent, hexCorners, hexToPixel, type Axial } from "./lib/hex";
import { cellsByTerritory, cellOutline } from "./lib/geometry";
import { planetTypeFor, planetUri, seedFromId } from "./lib/planet";
import { loopToPxPath, loopsToPxPath, makeTransform } from "./lib/transform";
import { pick, type GalaxyModel, type PatternId, type Route, type Territory } from "./lib/model";

/* ------------------------------------------------------------------ *
 * Fill patterns — the single biggest thing separating a political map
 * from a set of coloured blobs. One per territory, colour baked in.
 * Tiles are sized in px so they do not change weight with the viewport.
 * ------------------------------------------------------------------ */

const TILE = 9;

function PatternBody(props: { kind: PatternId; color: string }) {
  const c = props.color;
  switch (props.kind) {
    case "hatch":
      return <path d="M-1,1 l2,-2 M0,9 l9,-9 M8,10 l2,-2" stroke={c} stroke-width="1.1" opacity="0.5" />;
    case "crosshatch":
      return (
        <>
          <path d="M-1,1 l2,-2 M0,9 l9,-9 M8,10 l2,-2" stroke={c} stroke-width="1" opacity="0.4" />
          <path d="M1,-1 l2,2 M9,0 l-9,9 M10,8 l-2,2" stroke={c} stroke-width="1" opacity="0.4" />
        </>
      );
    case "dots":
      return <circle cx="4.5" cy="4.5" r="1.4" fill={c} opacity="0.5" />;
    case "grid":
      return <path d="M9,0 L9,9 M0,9 L9,9" stroke={c} stroke-width="1" opacity="0.45" />;
    case "horizontal":
      return <path d="M0,2.5 L9,2.5 M0,6.5 L9,6.5" stroke={c} stroke-width="1.1" opacity="0.45" />;
    case "vertical":
      return <path d="M2.5,0 L2.5,9 M6.5,0 L6.5,9" stroke={c} stroke-width="1.1" opacity="0.45" />;
    case "checker":
      return (
        <>
          <rect x="0" y="0" width="4.5" height="4.5" fill={c} opacity="0.4" />
          <rect x="4.5" y="4.5" width="4.5" height="4.5" fill={c} opacity="0.4" />
        </>
      );
    default:
      return null;
  }
}

/** Deterministic PRNG so the starfield is identical on every client. */
function mulberry(seed: number) {
  return () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function starfield(w: number, h: number, count: number, seed: number) {
  const rnd = mulberry(seed);
  const out: { x: number; y: number; r: number; o: number }[] = [];
  for (let i = 0; i < count; i++) {
    const b = rnd();
    out.push({
      x: rnd() * w,
      y: rnd() * h,
      r: 0.4 + b * b * 1.7,
      o: 0.14 + b * 0.66,
    });
  }
  return out;
}

export interface ChartProps {
  model: GalaxyModel;
  locale: string;
  showCells: boolean;
  selected: string | undefined;
  hoverCell: Axial | undefined;
  painting: boolean;
  /** False for players: the chart is read-only and the brushes are hidden. */
  canPaint: boolean;
  brushArmed: boolean;
  /** A drag is in progress. */
  stroking: boolean;
  /** Cells the in-progress drag has covered, as cell keys. */
  pendingCells: ReadonlySet<string> | undefined;
  /** Colour to preview the in-progress stroke in. */
  pendingColour: string | undefined;
  /**
   * Spike only: draw star systems as generated planets instead of dots.
   * Same on/off switch the comparison panel drives, so the two cannot disagree.
   */
  planets: boolean;
  onHover: (ly: { x: number; y: number }) => void;
  onClick: (ly: { x: number; y: number }) => void;
  /** Pointer went down with a brush armed — begin a stroke. */
  onStrokeStart: (ly: { x: number; y: number }) => void;
  /** Pointer moved with the button down — extend the stroke. */
  onStrokeMove: (ly: { x: number; y: number }) => void;
  /** Pointer released or left the chart — commit the stroke. */
  onStrokeEnd: () => void;
  onLeave: () => void;
}

/**
 * Lift a colour until it is legible on the dark chart.
 *
 * Territory names are drawn in the territory's own colour, which keeps the
 * colour-coding meaningful — but a mid-tone fill swallows its own label. Gold on
 * gold and cyan on cyan were noticeably harder to read than white on silver,
 * which made the map's legibility depend on which nation you happened to be
 * looking at.
 *
 * Raising lightness preserves the hue, so "this is Biesel's blue" still reads,
 * while guaranteeing the label is brighter than the fill it sits on. Saturation
 * is trimmed as it lightens, because a fully saturated pastel is the classic way
 * to make text look washed out rather than bright.
 */
export function readableOnDark(hex: string, minL = 0.74): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return hex;
  const n = parseInt(m[1], 16);
  const r = ((n >> 16) & 255) / 255;
  const g = ((n >> 8) & 255) / 255;
  const b = (n & 255) / 255;
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;
  const d = max - min;
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1));
  let h = 0;
  if (d !== 0) {
    if (max === r) h = ((g - b) / d) % 6;
    else if (max === g) h = (b - r) / d + 2;
    else h = (r - g) / d + 4;
    h *= 60;
    if (h < 0) h += 360;
  }
  const L = Math.max(l, minL);
  // Give back saturation as the colour lightens, so it does not go neon.
  const S = l >= minL ? s : s * Math.max(0.45, 1 - (L - l) * 1.4);
  const c = (1 - Math.abs(2 * L - 1)) * S;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const mm = L - c / 2;
  let rgb: [number, number, number];
  if (h < 60) rgb = [c, x, 0];
  else if (h < 120) rgb = [x, c, 0];
  else if (h < 180) rgb = [0, c, x];
  else if (h < 240) rgb = [0, x, c];
  else if (h < 300) rgb = [x, 0, c];
  else rgb = [c, 0, x];
  return (
    "#" +
    rgb
      .map(v =>
        Math.round((v + mm) * 255)
          .toString(16)
          .padStart(2, "0"),
      )
      .join("")
  );
}

/** Pure SVG presentation. Owns only its measured size. */
export default function Chart(props: ChartProps) {
  const [size, setSize] = createSignal({ w: 1200, h: 700 });
  let hostRef: HTMLDivElement | undefined;
  let svgRef: SVGSVGElement | undefined;

  createEffect(() => {
    const el = hostRef;
    if (!el) return;
    const ro = new ResizeObserver(entries => {
      const r = entries[0].contentRect;
      if (r.width > 0 && r.height > 0) setSize({ w: r.width, h: r.height });
    });
    ro.observe(el);
    onCleanup(() => ro.disconnect());
  });

  const PAD = 56;
  const t = () => makeTransform(props.model.extentLy, size().w, size().h, PAD);
  const terrById = createMemo(() => new Map(props.model.territories.map(x => [x.id, x])));

  /* ------------------------------------------------------------------ *
   * Reactive geometry
   *
   * Everything that depends on the transform is built HERE, in a memo, and the
   * JSX below only reads plain fields off the result.
   *
   * The rule exists because of a bug that cost real time. Solid runs the body of
   * a <For>/<Show> child inside untrack(), so a reactive read hoisted into a
   * local there is evaluated exactly once and never again. Hoisting
   * `const P = t().toPx(...)` inside a <For> is therefore not a style
   * preference — it silently freezes the geometry at whatever the viewport
   * happened to be on first paint. Every star stayed nailed to the position it
   * got for the initial 1200x700 default while the territory fills, which are
   * read through memos, re-laid-out correctly on resize. The chart looked
   * *plausible* at the one window size that happened to match, and read as
   * "systems are in the wrong place" everywhere else. Zooming the browser is
   * just another way to change the viewport, so it moved the fills and not the
   * markers, and the two drifted apart.
   *
   * Precomputing in memos makes that mistake structurally impossible here:
   * there is no reactive read left in a control-flow body to hoist.
   * ------------------------------------------------------------------ */

  /** Markers, pre-projected. Stable across a paint; rebuilt on resize. */
  /**
   * Generated sprites, keyed by system.
   *
   * Built here rather than inside the `<For>` because rendering a planet calls
   * `toDataURL`, and doing that inline in an attribute would regenerate every
   * sprite on every re-render. The cache inside `planetUri` means the second
   * call is a map hit, but the memo is what keeps the first call from happening
   * in the wrong order relative to the transform.
   *
   * Every star and every world gets one, sized by importance. The earlier cut
   * drew capitals only, on the theory that fifteen small sprites would be noise;
   * turned out the halo was what made them noisy, and without it the full set
   * reads fine and the size ramp does the hierarchy work the ring used to.
   */
  const planetSprites = createMemo(() => {
    if (!props.planets) return new Map<string, { href: string; size: number; ring: number }>();
    const dpr = window.devicePixelRatio || 1;
    const out = new Map<string, { href: string; size: number; ring: number }>();
    for (const s of props.model.systems) {
      // Stations and outposts keep their own silhouettes. A square is a
      // different shape on purpose, and replacing it with a small grey rock
      // would throw away the distinction.
      if (s.kind !== "star" && s.kind !== "planet") continue;
      const size = s.importance >= 3 ? 40 : s.importance >= 2 ? 30 : s.importance >= 1 ? 21 : 16;
      out.set(s.id, {
        href: planetUri({
          seed: seedFromId(s.id),
          type: s.planetType ?? planetTypeFor(s.kind, s.id),
          px: size,
          dpr,
          // No tint. Pulling the palette toward a nation colour desaturated each
          // planet into a muddy version of the territory it already sits inside,
          // and the owner is unambiguous from the fill behind it.
          tint: undefined,
          tintAmount: 0,
        }),
        size,
        // The capital ring goes outside the sprite, clear of the corona. Inside
        // it, the sprite simply covers it and capitals stop reading as capitals.
        ring: size / 2 + 7,
      });
    }
    return out;
  });

  const systemNodes = createMemo(() =>
    props.model.systems.map(s => ({
      id: s.id,
      system: s,
      colour: terrById().get(s.territory)?.color ?? "#94a3b8",
      P: t().toPx({ x: s.xLy, y: s.yLy }),
    })),
  );

  /** Route arcs, pre-projected. */
  const routeNodes = createMemo(() => {
    const byId = new Map(props.model.systems.map(s => [s.id, s]));
    const out: { id: string; d: string; kind: Route["kind"] }[] = [];
    for (const r of props.model.routes) {
      const a = byId.get(r.from);
      const b = byId.get(r.to);
      if (!a || !b) continue;
      const A = t().toPx({ x: a.xLy, y: a.yLy });
      const B = t().toPx({ x: b.xLy, y: b.yLy });
      // Bow each link perpendicular to its own axis, so the bundle reads as a
      // set of deliberate curves rather than a starburst of straight spokes.
      const cx = (A.x + B.x) / 2 + (B.y - A.y) * 0.16;
      const cy = (A.y + B.y) / 2 - (B.x - A.x) * 0.16;
      out.push({ id: `${r.from}>${r.to}`, d: `M${A.x},${A.y} Q${cx},${cy} ${B.x},${B.y}`, kind: r.kind });
    }
    return out;
  });

  /** One path covering every disputed cell, as a single even-odd subpath soup. */
  const contestedPath = createMemo(() => {
    const sizeLy = props.model.hexSizeLy;
    const parts: string[] = [];
    for (const k of props.model.contested) {
      const [q, r] = k.split(",");
      const pts = hexCorners(hexToPixel({ q: +q, r: +r }, sizeLy), sizeLy).map(t().toPx);
      parts.push("M" + pts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join("L") + "Z");
    }
    return parts.join(" ");
  });

  /**
   * Cells the in-progress drag has covered, as one flat hex path.
   *
   * Drawn from raw hex corners with no wobble and no pattern, which is the whole
   * point: a drag fires mousemove far faster than the model can be rebuilt, and
   * the rebuild is the expensive part (it re-runs the wobble over every
   * territory outline). So the stroke previews cheaply and the real render
   * arrives once on pointerup.
   */
  const pendingPath = createMemo(() => {
    const cells = props.pendingCells;
    if (!cells || cells.size === 0) return "";
    const sizeLy = props.model.hexSizeLy;
    const parts: string[] = [];
    for (const k of cells) {
      const [q, r] = k.split(",");
      const pts = hexCorners(hexToPixel({ q: +q, r: +r }, sizeLy), sizeLy).map(t().toPx);
      parts.push("M" + pts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join("L") + "Z");
    }
    return parts.join(" ");
  });

  /**
   * Hover outline.
   *
   * The polygon is always in the DOM rather than behind a <Show>. Solid's <Show>
   * normalises its condition to truthiness (`equals: (a, b) => !a === !b`) and
   * runs the child body untracked, so `<Show when={someObject}>{c => <poly
   * points={work(c())} />}</Show>` evaluates `work` once and then never again —
   * the highlight locks onto the first cell the pointer ever entered. A memo
   * plus a permanent element has neither problem.
   */
  const hoverPts = createMemo(() => {
    const c = props.hoverCell;
    if (!c) return "";
    const sizeLy = props.model.hexSizeLy;
    return hexCorners(hexToPixel(c, sizeLy), sizeLy)
      .map(t().toPx)
      .map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`)
      .join(" ");
  });

  /** One path per territory, all its loops concatenated (even-odd fills holes). */
  const fills = (): Map<string, string> => {
    const byTerr = cellsByTerritory(props.model.ownership);
    const out = new Map<string, string>();
    for (const terr of props.model.territories) {
      const cells = new Set(byTerr.get(terr.id) ?? []);
      if (!cells.size) continue;
      const d = loopsToPxPath(cellOutline(cells, props.model.hexSizeLy), t());
      if (d) out.set(terr.id, d);
    }
    return out;
  };

  const graticule = () =>
    cellsInExtent(props.model.extentLy.w, props.model.extentLy.h, props.model.hexSizeLy).map(c => {
      const pts = hexCorners(hexToPixel(c, props.model.hexSizeLy), props.model.hexSizeLy).map(t().toPx);
      return "M" + pts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join("L") + "Z";
    });

  const stars = () => starfield(size().w, size().h, 460, 90210);

  /**
   * Territory names.
   *
   * The centroid of a cell set is a poor place for a label: it routinely lands
   * on a capital, and a letterspaced uppercase title like "REPUBLIC OF BIESEL"
   * is a third of the map wide, so nudging the anchor point clear is nowhere
   * near enough. Placement therefore scores candidate spots against the label's
   * actual box, not its anchor.
   *
   * Unclaimed space is handled separately. Its centroid is the centre of the
   * whole map, which is the single worst pixel on the chart — and it is a hole,
   * not a nation, so it gets a small dim watermark rather than a title.
   */
  const labels = () => {
    const byTerr = cellsByTerritory(props.model.ownership);
    const systems = props.model.systems.map(s => t().toPx({ x: s.xLy, y: s.yLy }));
    const out: {
      id: string;
      name: string;
      p: { x: number; y: number };
      size: number;
      faint: boolean;
      color: string;
    }[] = [];

    for (const terr of props.model.territories) {
      const cells = byTerr.get(terr.id);
      if (!cells || cells.length < 8) continue;

      let sx = 0;
      let sy = 0;
      for (const k of cells) {
        const [q, r] = k.split(",");
        const c = hexToPixel({ q: +q, r: +r }, props.model.hexSizeLy);
        sx += c.x;
        sy += c.y;
      }
      const centre = t().toPx({ x: sx / cells.length, y: sy / cells.length });

      if (terr.unclaimed) {
        out.push({
          id: terr.id,
          name: pick(terr.name, props.locale),
          p: centre,
          size: 12,
          faint: true,
          color: readableOnDark(terr.color),
        });
        continue;
      }

      const size = Math.max(11, Math.min(22, Math.sqrt(cells.length) * 0.95));
      const name = pick(terr.name, props.locale);
      out.push({
        id: terr.id,
        name,
        p: place(centre, name, size, systems),
        size,
        faint: false,
        color: readableOnDark(terr.color),
      });
    }
    return out;
  };

  /**
   * Pick the spot for a centred label that keeps it furthest from every system
   * marker, without wandering far from the centroid.
   *
   * Scoring rather than a spiral-until-clear: "first candidate that fits" tends
   * to stop at the first marginally-acceptable spot and jam the label against
   * the edge of a territory. Rewarding clearance and charging for distance
   * gives a stable, sensible result every time.
   */
  function place(
    centre: { x: number; y: number },
    name: string,
    size: number,
    obstacles: { x: number; y: number }[],
  ): { x: number; y: number } {
    // Mono, uppercase, with letter-spacing of ~0.3em: roughly one advance per
    // character per em, so half the run is half the width.
    const halfW = (name.length * size * 0.65) / 2;
    const halfH = size * 0.7;

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
      // Cap the reward so a single very distant marker cannot drag the label
      // right across the territory; then charge for leaving the centroid.
      const score = Math.min(clearance, 34) - Math.hypot(q.x - centre.x, q.y - centre.y) * 0.3;
      if (score > bestScore) {
        bestScore = score;
        best = q;
      }
    }
    return best;
  }

  /**
   * Pointer position in the SVG's own user units.
   *
   * Deliberately NOT `getScreenCTM()`. That matrix is specified to include the
   * document's current zoom, so inverting it and feeding the result client
   * coordinates divides the point by the zoom factor and the click lands in the
   * wrong place — which is exactly the "the chart is broken when I Cmd+ the
   * browser" report. `getBoundingClientRect` is in unzoomed CSS pixels, which is
   * the same space as `clientX`/`clientY`, and the SVG is laid out 1:1 with those
   * pixels (its viewBox is its pixel size), so the subtraction is exact at any
   * zoom and under any CSS transform on an ancestor.
   */
  function local(ev: MouseEvent): { x: number; y: number } | null {
    const svg = svgRef;
    if (!svg) return null;
    const r = svg.getBoundingClientRect();
    if (r.width === 0 || r.height === 0) return null;
    return { x: ev.clientX - r.left, y: ev.clientY - r.top };
  }

  const frame = () => ({
    x: t().ox - (props.model.extentLy.w / 2) * t().scale,
    y: t().oy - (props.model.extentLy.h / 2) * t().scale,
    width: props.model.extentLy.w * t().scale,
    height: props.model.extentLy.h * t().scale,
  });

  return (
    <div class="chart-host" ref={hostRef}>
      <svg
        ref={svgRef}
        class="chart"
        classList={{ painting: props.painting, stroking: props.stroking }}
        width={size().w}
        height={size().h}
        viewBox={`0 0 ${size().w} ${size().h}`}
        onMouseMove={ev => {
          const p = local(ev);
          if (!p) return;
          const ly = t().toLy(p);
          if (props.stroking) props.onStrokeMove(ly);
          props.onHover(ly);
        }}
        onMouseDown={ev => {
          // Only the primary button, and only when a brush is armed: a plain
          // click has to stay a click, and a right-click is a context menu.
          if (ev.button !== 0 || !props.canPaint || !props.brushArmed) return;
          const p = local(ev);
          if (p) props.onStrokeStart(t().toLy(p));
        }}
        onMouseUp={ev => {
          if (!props.stroking) return;
          ev.preventDefault();
          props.onStrokeEnd();
        }}
        onClick={ev => {
          // A drag ends with a click event too. Let the stroke commit handle it,
          // or the last cell of every stroke is painted twice and a swipe across
          // a border leaves one cell behind.
          if (props.stroking) return;
          const p = local(ev);
          if (p) props.onClick(t().toLy(p));
        }}
        onMouseLeave={() => {
          props.onLeave();
          // Dragging off the edge ends the stroke rather than leaving it hanging
          // until the next click somewhere else.
          if (props.stroking) props.onStrokeEnd();
        }}
      >
        <defs>
          <radialGradient id="deep" cx="50%" cy="42%" r="78%">
            <stop offset="0%" stop-color="#0d1526" />
            <stop offset="55%" stop-color="#070c17" />
            <stop offset="100%" stop-color="#03050a" />
          </radialGradient>
          <radialGradient id="nebulaA" cx="30%" cy="35%" r="42%">
            <stop offset="0%" stop-color="#2a3f7a" stop-opacity="0.5" />
            <stop offset="100%" stop-color="#2a3f7a" stop-opacity="0" />
          </radialGradient>
          <radialGradient id="nebulaB" cx="72%" cy="66%" r="46%">
            <stop offset="0%" stop-color="#5a2a5e" stop-opacity="0.38" />
            <stop offset="100%" stop-color="#5a2a5e" stop-opacity="0" />
          </radialGradient>

          <For each={props.model.territories}>
            {terr => (
              <pattern
                id={`pat-${terr.id}`}
                width={TILE}
                height={TILE}
                patternUnits="userSpaceOnUse"
              >
                <PatternBody kind={terr.pattern} color={terr.color} />
              </pattern>
            )}
          </For>

          {/* Chart content is clipped to the frame. `cellsInExtent` keeps every
              cell whose CENTRE is inside the extent, so the outermost row of
              hexes overhangs the frame by half a cell — which put a stray hex
              outline up under the toolbar, right across the GRID button. */}
          <clipPath id="frame-clip">
            <rect {...frame()} />
          </clipPath>

          {/* One shared pattern for every disputed cell. Contested ground has no
              owner to take a colour from, so it gets its own: a tight amber
              cross-hatch that reads at map scale and never competes with a
              nation's fill. */}
          <pattern id="pat-contested" width="8" height="8" patternUnits="userSpaceOnUse">
            <rect width="8" height="8" fill="#ffb454" fill-opacity="0.1" />
            <path d="M0,8 L8,0" stroke="#ffb454" stroke-width="1.6" stroke-opacity="0.85" />
            <path d="M-2,2 L2,-2 M6,10 L10,6" stroke="#ffb454" stroke-width="1.6" stroke-opacity="0.85" />
          </pattern>
        </defs>

        {/* Background. Depth first, so nothing reads as flat black. */}
        <rect x="0" y="0" width={size().w} height={size().h} fill="url(#deep)" />
        <rect x="0" y="0" width={size().w} height={size().h} fill="url(#nebulaA)" />
        <rect x="0" y="0" width={size().w} height={size().h} fill="url(#nebulaB)" />
        <For each={stars()}>
          {s => <circle cx={s.x} cy={s.y} r={s.r} fill="#dfe9f5" opacity={s.o} />}
        </For>

        {/* Chart graticule — faint, deliberate, toggleable. */}
        <Show when={props.showCells}>
          <g class="graticule" clip-path="url(#frame-clip)">
            <For each={graticule()}>{d => <path d={d} />}</For>
          </g>
        </Show>

        {/* Territory fills: flat tint, then the pattern on top. */}
        <For each={props.model.territories}>
          {terr => (
            <Show when={fills().get(terr.id)}>
              <path
                d={fills().get(terr.id)}
                fill-rule="evenodd"
                fill={terr.color}
                opacity={terr.unclaimed ? 0.14 : 0.26}
              />
              <path
                d={fills().get(terr.id)}
                fill-rule="evenodd"
                fill={`url(#pat-${terr.id})`}
                opacity={terr.unclaimed ? 0.22 : 0.8}
              />
            </Show>
          )}
        </For>

        {/* Territory outline: a wide soft pass under a crisp one — the cheap
            version of a distance-field gradient border. */}
        <For each={props.model.territories}>
          {terr => (
            <Show when={fills().get(terr.id)}>
              <path
                d={fills().get(terr.id)}
                fill="none"
                stroke={terr.color}
                stroke-width={terr.unclaimed ? 2 : 9}
                opacity={terr.unclaimed ? 0.16 : 0.28}
                stroke-linejoin="round"
              />
              <path
                d={fills().get(terr.id)}
                fill="none"
                stroke={terr.color}
                stroke-width={terr.unclaimed ? 1.2 : 2.4}
                opacity={terr.unclaimed ? 0.4 : 0.95}
                stroke-linejoin="round"
              />
            </Show>
          )}
        </For>

        {/* Border highlight. Deliberately stroked from the SAME loops that
            produce each fill rather than from separately-computed border
            chains: a territory outline already traces its entire boundary, so
            this cannot disagree with the fill beneath it. A shared border is
            stroked once from each side, which just makes nation-to-nation lines
            slightly stronger than frontier lines — desirable on a political
            chart, and impossible to get wrong. */}
        <g>
          <For each={props.model.territories}>
            {terr => (
              <Show when={!terr.unclaimed && fills().get(terr.id)}>
                <path
                  d={fills().get(terr.id)}
                  fill="none"
                  stroke="#eaf3fb"
                  stroke-width="2.4"
                  opacity="0.85"
                  stroke-linejoin="round"
                />
              </Show>
            )}
          </For>
        </g>

        {/* The in-progress drag.
            Drawn from raw hexes with no wobble so it can keep up with the
            pointer; the committed render replaces it on pointerup. */}
        <Show when={props.stroking}>
          <g class="stroke-pending">
            <path
              d={pendingPath()}
              fill={props.pendingColour ?? "#ffd479"}
              fill-rule="evenodd"
              opacity="0.5"
            />
            <path
              d={pendingPath()}
              fill="none"
              stroke={props.pendingColour ?? "#ffd479"}
              stroke-width="1.4"
              opacity="0.9"
            />
          </g>
        </Show>

        {/* Contested ground.
            Drawn over the fills and under everything else, because it is a
            property OF the territory rather than a thing beside it: a hatched
            amber cell should still read as Biesel's land with a dispute on it.
            Deliberately not a colour change — recolouring would imply the cell
            belongs to whoever the new colour belongs to. */}
        <g class="contested-layer" clip-path="url(#frame-clip)">
          <path d={contestedPath()} fill="url(#pat-contested)" fill-rule="evenodd" />
          {/* Per-cell stroke, deliberately light: a run of disputed cells should
              read as one band, not as a row of separately outlined tiles. */}
          <path d={contestedPath()} fill="none" stroke="#ffb454" stroke-width="1.1" opacity="0.6" />
        </g>

        {/* Routes. FTL is not wired up; drawing the links is free and adds a
            lot of the "real chart" read. */}
        <g>
          <For each={routeNodes()}>
            {r => (
              <path
                d={r.d}
                fill="none"
                stroke="#7fd4c8"
                stroke-width="1.3"
                opacity="0.6"
                stroke-dasharray={r.kind === "gate" ? "7 5" : "3 5"}
              />
            )}
          </For>
        </g>

        {/* Territory naming, UNDER the systems.
            Drawn first so place names win. A territory title is large and
            letterspaced and will happily bury the capital sitting under it;
            place names are the ones you actually look up. */}
        <For each={labels()}>
          {l => (
            <text
              x={l.p.x}
              y={l.p.y}
              font-size={`${l.size}px`}
              letter-spacing={l.faint ? "1px" : `${l.size * 0.3}px`}
              class="terr-name"
              classList={{
                hot: props.selected === l.id,
                dim: !!props.selected && props.selected !== l.id,
                faint: l.faint,
              }}
              fill={l.color}
            >
              {l.name}
            </text>
          )}
        </For>

        {/* Systems. Four silhouettes, not four sizes. */}
        <g>
          <For each={systemNodes()}>
            {n => {
              const sprite = () => planetSprites().get(n.system.id);
              return (
              <>
                <Show when={n.system.importance === 3}>
                  <circle
                    cx={n.P.x}
                    cy={n.P.y}
                    r={sprite()?.ring ?? 10}
                    fill="none"
                    stroke={n.colour}
                    stroke-width="1.6"
                    opacity="0.9"
                  />
                </Show>
                <Show when={n.system.kind === "station" || n.system.kind === "outpost"}>
                  <rect
                    x={n.P.x - 3.5}
                    y={n.P.y - 3.5}
                    width="7"
                    height="7"
                    fill="#cbd8e6"
                    opacity="0.9"
                  />
                </Show>
                <Show when={n.system.kind === "star" || n.system.kind === "planet"}>
                  <Show
                    when={sprite()}
                    fallback={
                      <circle
                        cx={n.P.x}
                        cy={n.P.y}
                        r={n.system.importance >= 2 ? 5 : 3.2}
                        fill={n.colour}
                        stroke="#0a0f18"
                        stroke-width="1"
                      />
                    }
                  >
                    {sp => (
                      <image
                        class="planet-mark"
                        href={sp().href}
                        x={n.P.x - sp().size / 2}
                        y={n.P.y - sp().size / 2}
                        width={sp().size}
                        height={sp().size}
                      />
                    )}
                  </Show>
                </Show>
                <text
                  x={n.P.x + (n.system.importance === 3 ? 15 : 9)}
                  y={n.P.y - 5}
                  class="system-label"
                  classList={{ capital: n.system.importance === 3 }}
                >
                  {pick(n.system.name, props.locale)}
                </text>
              </>
              );
            }}
          </For>
        </g>

        {/* Hover cell, for the paint tool. */}
        <polygon class="cell-hover" classList={{ off: !props.hoverCell }} points={hoverPts()} />

        <rect {...frame()} class="frame-line" />
      </svg>
    </div>
  );
}
