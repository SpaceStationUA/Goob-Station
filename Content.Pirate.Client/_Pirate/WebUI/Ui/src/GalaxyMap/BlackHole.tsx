/**
 * A black hole, drawn as one generated sprite.
 *
 * The geometry here is `lib/blackhole.ts`, and this file is only the host that places
 * it. An earlier version drew it as SVG — a dark circle, a stroked ring and two
 * `ringHalf` annuli — and it looked like a diagram of a black hole: a hoop with a
 * circle inside it. The reference's disc is the *preimage* of an ellipse under a
 * non-linear warp, which no combination of paths can express, and it is textured
 * with rotating fbm, which no flat fill can either. So it is baked, like a planet.
 *
 * ## It reuses `WorldSprite`'s class contract, and that is not tidiness
 *
 * The first version used `class="blackhole"` and gave its strip `class="world-turn"`.
 * The stylesheet only shows a strip under `.world.turning`, so the animation ran
 * happily on an element sitting at `opacity: 0` and the black hole never turned at
 * all. It reported as animating: `background-position` advanced, `playState` was
 * `running`, and nothing was visible to advance. A rotating element nobody can see is
 * the most embarrassing failure available to a component whose only reason to exist
 * is that it rotates, and the fix is to not keep a second, parallel set of class
 * names for the same thing.
 *
 * ## The period is the difference between rotating and being static
 *
 * At 48 seconds a turn across 24 frames, each frame holds for two seconds. That
 * animates — the position advances, the clock runs — and it reads as a slideshow, or
 * as nothing at all. A viewer asked whether something turns is watching for change
 * over about a second, so the turn has to fit in a few times that. The planet hosts
 * use 15s and the black hole 11s, which is the difference between "it moved" and "I
 * could not tell".
 *
 * ## It is live now, and the baked path is the fallback
 *
 * Everything above describes a strip, and it was right about the shape and wrong
 * about what to do about the two other things. A filmstrip played with `steps(n)`
 * cannot be smoother than `n / period`, and at 48 frames over 6s that is 8fps,
 * which reads as a slideshow however good the frames are; and 48 frames of a 200px
 * body is a synchronous pixel loop, so the panel sat on a still for over a second.
 * Both are properties of the technique, not of the tuning — more frames cost
 * linearly and cap out well below 60.
 *
 * So the overlay renders this live in WebGL (`lib/gl.ts`) and the strip is what it
 * falls back to when there is no context. The baked still is still drawn underneath
 * at all times: it is one frame, it costs about 30ms, and it means the panel is
 * never blank while the shader compiles, never flashes if WebGL turns out to be
 * missing, and is right on its own if the canvas never paints.
 */

import { createEffect, createSignal, onCleanup, Show } from "solid-js";
import { blackHoleSheet, blackHoleUri } from "./lib/blackhole";
import { blackHoleGL, type BlackHoleGL } from "./lib/gl";

/** `prefers-reduced-motion`. Rotation is the point here, so this is the only off switch. */
export function reducedMotion(): boolean {
  return (
    typeof window !== "undefined" &&
    window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true
  );
}

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
  const dpr = () => (typeof window === "undefined" ? 1 : window.devicePixelRatio || 1);
  // Read first so the effect re-subscribes when any of them change.
  const seed = () => props.seed;
  const px = () => props.px;
  const n = () => (reducedMotion() ? 1 : frames());

  const still = () => blackHoleUri({ seed: seed(), px: px(), dpr: dpr() });

  const [sheet, setSheet] = createSignal<string>();
  const [live, setLive] = createSignal<BlackHoleGL | null>(null);
  let host!: HTMLDivElement;

  // The live renderer. Appended imperatively because the canvas is created, sized
  // and driven by WebGL rather than by the reconciler.
  createEffect(() => {
    const target = seed();
    const size = px();
    const still3d = reducedMotion();
    const inst = blackHoleGL({
      seed: target,
      px: size,
      period: props.period ?? 6,
      animate: !still3d,
    });
    if (!inst) return;
    host?.appendChild(inst.canvas);
    setLive(inst);
    onCleanup(() => {
      setLive(null);
      inst.dispose();
      inst.canvas.remove();
    });
  });

  createEffect(() => {
    const want = n();
    const target = seed();
    const size = px();
    if (want <= 1) {
      setSheet(undefined);
      return;
    }
    setSheet(undefined);
    // At dpr 1: rotation hides resampling, and 2x is four times the pixels for a
    // strip that is on screen for a fraction of a second.
    const timer = window.setTimeout(() => {
      setSheet(blackHoleSheet({ seed: target, px: size, dpr: 1 }, want).uri);
    }, 0);
    onCleanup(() => window.clearTimeout(timer));
  });

  return (
    <div
      class="world blackhole"
      classList={{ turning: sheet() !== undefined }}
      style={{ width: `${px()}px`, height: `${px()}px` }}
      role={props.title ? "img" : undefined}
      aria-label={props.title}
    >
      {/* The still is always in the DOM, and stays there under the live canvas.
          It is one frame and it costs about 30ms, and it buys three things: no
          blank panel while the shader compiles, no flash if there is no context,
          and a correct picture on its own if the canvas never paints. */}
      <img class="world-still" src={still()} alt="" />
      <Show when={sheet() && !live()} keyed>
        {uri => (
          <div
            class="world-turn"
            style={{
              "background-image": `url(${uri})`,
              "background-size": `${px() * n()}px ${px()}px`,
              "animation-duration": `${props.period ?? 6}s`,
              "animation-timing-function": `steps(${n()})`,
              "--world-end": `-${px() * n()}px`,
            }}
          />
        )}
      </Show>
      <div class="world-gl" ref={host} hidden={!live()} />
    </div>
  );
}
