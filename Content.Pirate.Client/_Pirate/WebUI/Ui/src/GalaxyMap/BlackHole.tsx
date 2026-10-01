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
 * Strip generation is deferred on a macrotask for the same reason `WorldSprite`
 * defers: a couple of dozen frames of a 190px body is a synchronous pixel loop, and
 * running it inside the click that opened the panel freezes the page at the moment
 * the player asked it a question. The still is drawn at full size first and is
 * correct on its own — if the strip never arrives, the panel still shows a right
 * black hole.
 */

import { createEffect, createSignal, onCleanup, Show } from "solid-js";
import { blackHoleSheet, blackHoleUri } from "./lib/blackhole";

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
      {/* The still is always in the DOM. When the strip arrives it fades in over the
          top, so there is never a blank frame between the two. */}
      <img class="world-still" src={still()} alt="" />
      <Show when={sheet()} keyed>
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
    </div>
  );
}
