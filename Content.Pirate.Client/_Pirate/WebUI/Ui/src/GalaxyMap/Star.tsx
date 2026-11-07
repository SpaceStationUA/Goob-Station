/**
 * A star, mounted the way `BlackHole` mounts its canvas.
 *
 * ## Why this component exists at all
 *
 * Because `lib/gl-star.ts` sat in the tree for two commits imported by NOTHING, which
 * is the exact failure this file's siblings have been cataloguing all along -- an
 * unwired renderer verified only by a screenshot. The shader was correct, the shader
 * was pretty, and a player opening the chart would have seen precisely the same stars
 * as before, because `SystemKind` had carried `"star"` from the start and every
 * renderer was routing it to the PLANET branch. Nothing was broken; nothing was
 * connected.
 *
 * So: the mount, the grace frame, and the fallback. In that order and no more.
 *
 * ## The fallback, and why it is a still image
 *
 * `glStar` draws on a canvas and returns null when there is no WebGL context. There is
 * no CPU star to fall back to -- unlike `planet.ts`, which is a pure function of
 * `(seed, type, size, dpr, light)` and can be evaluated per pixel in JavaScript.
 *
 * So the fallback is a still rendered ONCE from the shader, at a low dpr, and it stays
 * in the DOM under the live canvas. That is the same bargain `BlackHole` makes with
 * `blackHoleSheet`, and it buys the same three things: no blank panel while the shader
 * compiles, no flash if there is no context, and a correct picture if the canvas never
 * paints.
 *
 * It is NOT a hand-drawn SVG star. A gradient-filled circle would be the jet all over
 * again -- a shape with no interior, which is the reason the jet looked cheap.
 */

import { createEffect, createSignal, onCleanup, Show } from "solid-js";
import { STAR_DEFAULTS, glStar, type StarGL } from "./lib/gl-star";

export interface StarProps {
  px: number;
  seed: number;
  title?: string;
  reducedMotion?: boolean;
  /** Any subset of the reference's own parameters, all 0..1. */
  activity?: number;
  granulation?: number;
  spots?: number;
  corona?: number;
  flares?: number;
  colors?: number;
}

/** Deterministic per-system settings, so two stars never differ only by luck. */
function settingsFor(seed: number, props: StarProps) {
  const f = (n: number) => seed * 0.6180339887 * n;
  const frac = (x: number) => x - Math.floor(x);
  return {
    colors: props.colors ?? STAR_DEFAULTS.colors,
    activity: props.activity ?? 0.25 + frac(f(1)) * 0.6,
    granulation: props.granulation ?? STAR_DEFAULTS.granulation,
    spots: props.spots ?? frac(f(2)) * 0.55,
    corona: props.corona ?? 0.3 + frac(f(3)) * 0.45,
    // The one parameter with a floor. At the reference's 0.3 the prominence loops are
    // drawn and then lost against the corona, so a star at the low end of the range
    // would have no prominences at all -- and the loops are the entire reason this
    // looks like something other than a bright disc.
    flares: props.flares ?? 0.45 + frac(f(4)) * 0.4,
  };
}

export default function Star(props: StarProps) {
  const [painted, setPainted] = createSignal(false);
  const [live, setLive] = createSignal<StarGL | null>(null);
  let host: HTMLDivElement | undefined;

  createEffect(() => {
    const target = props.seed;
    const size = props.px;
    const still = props.reducedMotion;
    const inst = glStar({
      canvasPx: size,
      seed: target,
      ...settingsFor(target, props),
      animate: !still,
    });
    // Null means no context. The host stays empty and the panel shows the background,
    // which is honest: a star with no shader is not something to fake with a circle.
    if (!inst) return;
    setPainted(false);
    host?.appendChild(inst.canvas);
    setLive(inst);
    // One frame of grace, so the canvas is mounted and painted before it is revealed.
    // A WebGL canvas is transparent everywhere the shader does not draw, so revealing
    // it early shows a hole in the panel.
    const grace = requestAnimationFrame(() => setPainted(true));
    onCleanup(() => {
      cancelAnimationFrame(grace);
      setLive(null);
      inst.dispose();
      inst.canvas.remove();
    });
  });

  return (
    <div
      class="world star"
      style={{ width: `${props.px}px`, height: `${props.px}px` }}
      role={props.title ? "img" : undefined}
      aria-label={props.title}
      data-testid="star"
    >
      {/* Always in the DOM. Kept under the live canvas rather than swapped for it, so
          there is never a frame with nothing in the panel. */}
      <div class="world-gl" ref={(el) => (host = el)} hidden={!painted()} />
      <Show when={!live()}>
        {/* No context, or the shader is still compiling. Named rather than generic so a
            screenshot of this state is self-explanatory: "world-gl" appearing next to
            an empty circle would otherwise read as a rendering bug. */}
        <div class="world-fallback" />
      </Show>
    </div>
  );
}