/**
 * A black hole, drawn as one generated sprite.
 *
 * The geometry here is `lib/blackhole.ts`, and this file is only the two hosts that
 * place it. An earlier version drew it as SVG — a dark circle, a stroked ring and
 * two `ringHalf` annuli — and it looked like a diagram of a black hole: a hoop with
 * a circle inside it. The reference's disc is the *preimage* of an ellipse under a
 * non-linear warp, which no combination of paths can express, and it is textured
 * with rotating fbm, which no flat fill can either. So it is baked, like a planet,
 * and this component is a wrapper around the same cache the planets use.
 *
 * The void is not drawn as a black shape. It is genuinely the absence of anything,
 * as it is in the reference: what makes a black hole legible is the photon ring at
 * the horizon's edge and the disc wrapped around it. On a chart with a near-black
 * page that is a real risk — a dark disc is a hole in the chart — and the photon
 * ring is what pays for it, so its contrast is asserted in `check-dom.mjs` by
 * measuring the rendered pixels rather than by reading an attribute.
 */

import { blackHoleSheet, blackHoleUri } from "./lib/blackhole";

export interface BlackHoleProps {
  /** Width of the sprite in CSS pixels. The disc is drawn to fill it. */
  px: number;
  seed: number;
  /** Seconds for one full turn of the disc's texture. */
  period?: number;
  /** Frames across one turn. 0 or 1 draws a still. */
  frames?: number;
  title?: string;
}

export default function BlackHole(props: BlackHoleProps) {
  const frames = () => Math.max(1, Math.floor(props.frames ?? 0));
  const still = () => blackHoleUri({ seed: props.seed, px: props.px, dpr: dpr() });
  const dpr = () => (typeof window === "undefined" ? 1 : window.devicePixelRatio || 1);
  // A rotating disc is the one place on the chart where motion is the point: the
  // shape is fixed and only the texture turns, which is what sells it as material
  // in orbit rather than as a decal.
  const sheet = () =>
    frames() > 1
      ? blackHoleSheet({ seed: props.seed, px: props.px, dpr: 1 }, frames()).uri
      : undefined;

  return (
    <div
      class="blackhole"
      style={{ width: `${props.px}px`, height: `${props.px}px` }}
      role={props.title ? "img" : undefined}
      aria-label={props.title}
    >
      <img class="world-still" src={still()} alt="" />
      {sheet() && (
        <div
          class="world-turn"
          style={{
            "background-image": `url(${sheet()})`,
            "background-size": `${props.px * frames()}px ${props.px}px`,
            "animation-duration": `${props.period ?? 30}s`,
            "animation-timing-function": `steps(${frames()})`,
            "--world-end": `-${props.px * frames()}px`,
          }}
        />
      )}
    </div>
  );
}
