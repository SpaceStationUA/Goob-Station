import { createEffect, createSignal, For, onCleanup, Show } from "solid-js";
import { cellsInExtent, hexCorners, hexToPixel, type Axial } from "./lib/hex";
import { cellsByTerritory, cellOutline } from "./lib/geometry";
import { loopToPxPath, loopsToPxPath, makeTransform } from "./lib/transform";
import type { GalaxyModel, PatternId, Territory } from "./lib/model";

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
  showCells: boolean;
  selected: string | undefined;
  hoverCell: Axial | undefined;
  painting: boolean;
  onHover: (ly: { x: number; y: number }) => void;
  onClick: (ly: { x: number; y: number }) => void;
  onLeave: () => void;
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
  const terrById = (): Map<string, Territory> => new Map(props.model.territories.map(x => [x.id, x]));

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

  /** Territory name placed at the centroid of its cells. */
  const labels = () => {
    const byTerr = cellsByTerritory(props.model.ownership);
    const out: { id: string; name: string; p: { x: number; y: number }; size: number }[] = [];
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
      out.push({
        id: terr.id,
        name: terr.name,
        p: t().toPx({ x: sx / cells.length, y: sy / cells.length }),
        // Bigger territories earn bigger type, but within a narrow band.
        size: Math.max(12, Math.min(27, Math.sqrt(cells.length) * 1.15)),
      });
    }
    return out;
  };

  function local(ev: MouseEvent): { x: number; y: number } | null {
    const svg = svgRef;
    if (!svg) return null;
    const ctm = svg.getScreenCTM();
    if (!ctm) return null;
    const pt = new DOMPoint(ev.clientX, ev.clientY).matrixTransform(ctm.inverse());
    return { x: pt.x, y: pt.y };
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
        classList={{ painting: props.painting }}
        width={size().w}
        height={size().h}
        viewBox={`0 0 ${size().w} ${size().h}`}
        onMouseMove={ev => {
          const p = local(ev);
          if (p) props.onHover(t().toLy(p));
        }}
        onClick={ev => {
          const p = local(ev);
          if (p) props.onClick(t().toLy(p));
        }}
        onMouseLeave={props.onLeave}
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
          <g class="graticule">
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

        {/* Routes. FTL is not wired up; drawing the links is free and adds a
            lot of the "real chart" read. */}
        <g>
          <For each={props.model.routes}>
            {r => {
              const a = props.model.systems.find(s => s.id === r.from);
              const b = props.model.systems.find(s => s.id === r.to);
              if (!a || !b) return null;
              const A = t().toPx({ x: a.xLy, y: a.yLy });
              const B = t().toPx({ x: b.xLy, y: b.yLy });
              const cx = (A.x + B.x) / 2 + (B.y - A.y) * 0.16;
              const cy = (A.y + B.y) / 2 - (B.x - A.x) * 0.16;
              return (
                <path
                  d={`M${A.x},${A.y} Q${cx},${cy} ${B.x},${B.y}`}
                  fill="none"
                  stroke="#7fd4c8"
                  stroke-width="1.3"
                  opacity="0.6"
                  stroke-dasharray={r.kind === "gate" ? "7 5" : "3 5"}
                />
              );
            }}
          </For>
        </g>

        {/* Systems. Four silhouettes, not four sizes. */}
        <g>
          <For each={props.model.systems}>
            {s => {
              const c = terrById().get(s.territory)?.color ?? "#94a3b8";
              const big = s.importance >= 2;
              const P = t().toPx({ x: s.xLy, y: s.yLy });
              return (
                <>
                  <Show when={s.importance === 3}>
                    <circle cx={P.x} cy={P.y} r="10" fill="none" stroke={c} stroke-width="1.6" opacity="0.9" />
                  </Show>
                  <Show when={s.kind === "station" || s.kind === "outpost"}>
                    <rect x={P.x - 3.5} y={P.y - 3.5} width="7" height="7" fill="#cbd8e6" opacity="0.9" />
                  </Show>
                  <Show when={s.kind === "star" || s.kind === "planet"}>
                    <circle cx={P.x} cy={P.y} r={big ? 5 : 3.2} fill={c} stroke="#0a0f18" stroke-width="1" />
                  </Show>
                  <text
                    x={P.x + (s.importance === 3 ? 15 : 9)}
                    y={P.y - 5}
                    class="system-label"
                    classList={{ capital: s.importance === 3 }}
                  >
                    {s.name}
                  </text>
                </>
              );
            }}
          </For>
        </g>

        {/* Territory naming: the big letterspaced tier. */}
        <For each={labels()}>
          {l => (
            <text
              x={l.p.x}
              y={l.p.y}
              font-size={`${l.size}px`}
              letter-spacing={`${l.size * 0.4}px`}
              class="terr-name"
              classList={{ hot: props.selected === l.id, dim: !!props.selected && props.selected !== l.id }}
              fill={terrById().get(l.id)?.color ?? "#fff"}
            >
              {l.name}
            </text>
          )}
        </For>

        {/* Hover cell, for the paint tool. */}
        <Show when={props.hoverCell}>
          {c => {
            const pts = hexCorners(hexToPixel(c(), props.model.hexSizeLy), props.model.hexSizeLy).map(
              t().toPx,
            );
            return <polygon points={pts.map(p => `${p.x},${p.y}`).join(" ")} class="cell-hover" />;
          }}
        </Show>

        <rect {...frame()} class="frame-line" />
      </svg>
    </div>
  );
}
