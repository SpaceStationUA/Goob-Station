/**
 * Remnant marks, for the chart.
 *
 * ## Why this file exists
 *
 * Pulsars and quasars were added to the OVERLAY first and the chart second, and the
 * chart half was missing for two full rounds of work. The overlay tests passed the
 * whole time, because they opened the overlay. Nothing on the chart asserted that a
 * remnant was drawn at all, so a `SystemKind` that matched no marker branch rendered
 * as nothing and every check was green.
 *
 * That is the same shape of mistake as the nebula behind an opaque rect, and the
 * lesson is now written down twice: a feature added to one surface needs an
 * assertion on the OTHER surface, or it does not exist.
 *
 * ## Why SVG here and canvas in the overlay
 *
 * Because this is 16 to 40 pixels and the overlay is 190. Four octaves of fbm and a
 * sweeping beam are both invisible at 16px, and an SVG path costs nothing per
 * marker where a canvas costs a compositing layer each. The shapes are simplified on
 * purpose: what has to survive the reduction is the silhouette, and for both of
 * these the silhouette IS the distinguishing feature.
 */

import { For, Show } from "solid-js";

export interface RemnantMarkProps {
  /** Marker centre, in chart pixels. */
  x: number;
  y: number;
  /** Marker box in pixels. Matches `bhOf`'s sizing so the two agree. */
  size: number;
  kind: "pulsar" | "quasar";
  /** Per-system seed, so no two are identical. */
  seed: number;
}

export default function RemnantMark(props: RemnantMarkProps) {
  const r = () => props.size / 2;
  // Beams and jets at their own angles, seeded. A pulsar's magnetic axis is not
  // obliged to point anywhere in particular, and four systems with the axis
  // vertical would read as a printing error rather than as four objects.
  const axis = () => (props.seed % 360) * (Math.PI / 180);
  const isPulsar = () => props.kind === "pulsar";

  return (
    <g
      class={`remnant-mark remnant-${props.kind}`}
      data-remnant={props.kind}
      transform={`translate(${props.x} ${props.y})`}
    >
      <Show
        when={isPulsar()}
        fallback={
          <>
            {/* A quasar at map scale is the black hole's own mark plus jets. The disc
                is baked rather than drawn here because it already exists, it is
                correct, and at 20-30px the noise-carved divisions are about two
                pixels across. */}
            <ellipse
              class="quasar-disc"
              cx="0"
              cy="0"
              rx={r() * 0.78}
              ry={r() * 0.22}
              fill="none"
              stroke="#c9a24a"
              stroke-width={Math.max(1.4, r() * 0.1)}
            />
            <ellipse
              cx="0"
              cy="0"
              rx={r() * 0.5}
              ry={r() * 0.13}
              fill="#0a0f18"
            />
            {/*
              * The jets, with their brightness travelling outward.
              *
              * The overlay slides the jet's GRADIENT to do this and the chart does
              * the same thing, because the alternative — discrete knots — was tried
              * and reads as grey blocks: a screen-blended off-white trapezoid on a
              * dark background is a rectangle, and scaling one vertically only makes
              * a bigger rectangle. There is no shape to misread when there is no
              * shape.
              *
              * Both poles, because one is a diagram.
              */}
            <defs>
              <linearGradient id={`mj-${props.seed}`} x1="0" y1="1" x2="0" y2="0">
                <animate attributeName="y1" values="1;0.1;-0.9" dur="1.35s" repeatCount="indefinite" />
                <animate attributeName="y2" values="2;1.1;0.1" dur="1.35s" repeatCount="indefinite" />
                <stop offset="0%" stop-color="#ffffff" stop-opacity="1" />
                <stop offset="30%" stop-color="#9fc6ff" stop-opacity="0.45" />
                <stop offset="100%" stop-color="#9fc6ff" stop-opacity="0" />
              </linearGradient>
            </defs>
            <g transform={`rotate(${props.seed % 90})`}>
              <path
                d={`M 0 ${-r() * 0.2} L ${-r() * 0.1} ${-r() * 1.5} L ${r() * 0.1} ${-r() * 1.5} Z`}
                fill={`url(#mj-${props.seed})`}
              />
              <path
                d={`M 0 ${r() * 0.2} L ${-r() * 0.1} ${r() * 1.5} L ${r() * 0.1} ${r() * 1.5} Z`}
                fill={`url(#mj-${props.seed})`}
              />
            </g>
          </>
        }
      >
        {/* A pulsar: a hard point and two opposed beams. The beams are tapered
            triangles rather than lines, because at 20px a line has no width that
            survives the reduction and a triangle does. */}
        <g transform={`rotate(${props.seed % 180})`}>
          <For each={[0, 180]}>
            {deg => (
              <path
                d={`M 0 ${deg === 0 ? -r() * 0.34 : r() * 0.34} L ${-r() * 0.24} ${deg === 0 ? -r() * 1.7 : r() * 1.7} L ${r() * 0.24} ${deg === 0 ? -r() * 1.7 : r() * 1.7} Z`}
                fill="#dceaff"
                opacity="0.92"
              />
            )}
          </For>
        </g>
        {/* The core, and the halo under it. Two elements because a single blob has
            no hard edge and the hard edge is the whole signature. */}
        {/* Sizes are pushed up hard from the first version, which drew a 4px core
            with 5px beams and was, correctly, reported as invisible. The problem is
            not that it was small: it was small AND pale, against a territory fill
            that is a bright hatch. A mark has to out-contrast its background, not
            merely fit inside its own box. */}
        <circle cx="0" cy="0" r={r() * 0.62} fill="#2f5a91" opacity="0.45" />
        <circle cx="0" cy="0" r={r() * 0.34} fill="#9dc4f2" opacity="0.95" />
        <circle cx="0" cy="0" r={r() * 0.2} fill="#ffffff" />
        {/* A dark rim under the core. The fill behind it is a hatch, and a white dot
            on a hatch disappears into it; a rim is what makes it a dot. */}
        <circle
          cx="0"
          cy="0"
          r={r() * 0.38}
          fill="none"
          stroke="#080d16"
          stroke-width={Math.max(0.8, r() * 0.07)}
        />
      </Show>
    </g>
  );
}