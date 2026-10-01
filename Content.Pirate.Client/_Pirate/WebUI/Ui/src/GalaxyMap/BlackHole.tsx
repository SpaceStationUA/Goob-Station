/**
 * A black hole: a dark horizon, a photon ring, and a tilted accretion disc.
 *
 * Drawn as SVG geometry for the same reasons the ring is, and it reuses the ring's
 * path builder rather than having its own — an accretion disc *is* an annulus seen
 * at a shallow angle, and two implementations of "half an annulus with a gap"
 * would drift apart within a month.
 *
 * It is deliberately NOT a `PlanetType`. A planet is something that can orbit
 * something; a singularity has nothing, and putting it in `planet.ts` would mean
 * every consumer of the sprite pipeline had to learn that one entry is not a
 * world. This is a system-scale body, drawn like the ring, living beside
 * `WorldSprite` rather than inside it.
 *
 * The horizon is the one thing on this chart that must not be drawn as a dark
 * shape. Territory fills are mid-tone and the page behind them is near black, so a
 * black disc on a black page is not a black hole, it is a hole in the chart — the
 * exact failure the star corona exists to prevent, arrived at from the opposite
 * direction. What makes a black hole legible is the light *around* it: the photon
 * ring at the horizon's edge, and the disc outside that. So the horizon is drawn
 * nearly black and never pure black, and then everything that identifies it goes on
 * top.
 *
 * The disc's near and far halves differ in tone. That is not decoration: the
 * approaching side of an accretion disc is brighter and blueshifted, and it is the
 * strongest cue that what you are looking at is rotating. A symmetric annulus
 * reads as a ring, and a ring reads as a planet with rings.
 *
 * ## Why this file exports shapes as well as a component
 *
 * HTML does not render inside an SVG `<g>`. The first version of this was a
 * `<div>` with `<div>` children, which is correct in the overlay panel and
 * produced *nothing at all* on the chart — every box measured 0x0, because the
 * elements were not being laid out as graphics. Nothing errored; the landmark was
 * simply absent, at the one size where it most needed to be seen.
 *
 * So the shapes are exported separately from the positioning: `BlackHoleShapes` is
 * the only description of what a black hole looks like, and the component below and
 * the `<g>` in `Chart.tsx` are four lines of placement each. The duplication that
 * remains is positioning, which is genuinely a property of the host — an HTML panel
 * and an SVG chart cannot be positioned the same way.
 */

import { Show, type JSX } from "solid-js";
import { ringGeomFor, ringHalf } from "./WorldRing";

export interface BlackHoleProps {
  /** Diameter of the *marker* in CSS pixels, matching a star's sprite box. */
  px: number;
  /** Seeded, so the disc's division and tilt vary per system. */
  seed?: number;
}

/**
 * The disc reaches further than a planet's ring, because there is no atmosphere and
 * no surface competing with it — the whole object is the disc. Capped by the same
 * host constraint as the ring: the overlay panel leaves 34px either side of a
 * 200px planet.
 */
function discOf(px: number, seed: number) {
  return ringGeomFor(px, seed, 0.34);
}

/** Width and height of the square box that contains the whole object. */
export function blackHoleBox(px: number, seed: number): number {
  return discOf(px, seed).rx * 2 + 2;
}

/**
 * The horizon, as a fraction of the marker's radius. Just over half, so the marker
 * carries the same visual weight as a star's sprite and the disc reads as an
 * addition rather than as a replacement.
 */
function horizonOf(px: number): number {
  return (px / 2) * 0.56;
}

/**
 * The shapes, in z-order: far disc, horizon, photon ring, near disc.
 *
 * Order is the occlusion. The disc passes behind the singularity and in front of
 * it, and the only thing doing the occluding is this element order — exactly as
 * with a ring, and for the same reason.
 */
export function BlackHoleShapes(props: BlackHoleProps): JSX.Element {
  const disc = () => discOf(props.px, props.seed ?? 1);
  // Wider than a planet's ring, and opaque — an accretion disc is not a hoop you
  // see through — but nowhere near as wide as this started. At 0.42 of the outer
  // radius the annulus was so thick that its inner edge read as a second, smaller
  // lens inside the first, and the whole thing looked like a solid shape with a
  // groove in it rather than material in orbit.
  const geom = () => ({ ...disc(), band: disc().rx * (props.px >= 60 ? 0.24 : 0.34) });
  const r = () => horizonOf(props.px);
  const far = () => ringHalf(geom(), true);
  const near = () => ringHalf(geom(), false);
  return (
    <>
      <Show when={far()}>
        <path d={far()} fill="#8a5a2c" />
      </Show>
      <circle class="bh-horizon" cx="0" cy="0" r={r()} fill="#05070c" />
      {/* The photon ring is the element that makes a black hole legible on a dark
          chart at all. Without a bright edge there is nothing to see. */}
      <circle
        class="bh-photon"
        cx="0"
        cy="0"
        r={r() + 1.5}
        fill="none"
        stroke="rgba(214,232,255,0.85)"
        stroke-width="1.5"
      />
      <Show when={near()}>
        <path d={near()} fill="#e8c79a" />
      </Show>
    </>
  );
}

/**
 * The HTML host: a square box with the shapes centred in it, for the overlay panel.
 */
export default function BlackHole(props: BlackHoleProps & { title?: string }) {
  const box = () => blackHoleBox(props.px, props.seed ?? 1);
  return (
    <div class="blackhole" style={{ width: `${props.px}px`, height: `${props.px}px` }}>
      {/* A plain element rather than a <Show>. Show normalises its condition to
          truthiness, so it treats any two non-empty titles as equal and would not
          re-render when the overlay moves from one system to another. */}
      <span class="sr-only">{props.title ?? ""}</span>
      <svg
        width={box()}
        height={box()}
        viewBox={`${-box() / 2} ${-box() / 2} ${box()} ${box()}`}
        style={{
          position: "absolute",
          left: "50%",
          top: "50%",
          transform: "translate(-50%, -50%)",
          overflow: "visible",
        }}
        role={props.title ? "img" : undefined}
        aria-label={props.title}
      >
        <BlackHoleShapes {...props} />
      </svg>
    </div>
  );
}
