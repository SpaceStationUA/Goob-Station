/**
 * One generated world, still or turning.
 *
 * Lives outside the spike on purpose. The comparison panel is disposable
 * scaffolding that will be deleted; this is the part worth keeping, because it
 * is what the system overlay draws.
 */

import { createEffect, createSignal, onCleanup, onMount, Show } from "solid-js";
import { planetSheetAsync, planetUri, type PlanetType } from "./lib/planet";
import { reducedMotion } from "./BlackHole";
import WorldRingGL from "./WorldRingGL";

export interface WorldSpriteProps {
  seed: number;
  type: PlanetType;
  /** Diameter in CSS pixels. */
  px: number;
  /**
   * Frames across one full turn. 0 or 1 draws a still.
   *
   * Rotation is invisible at map scale — a 16px sprite that turns costs twelve
   * times the pixels and shows nothing — so the chart keeps drawing stills and
   * only the overlay, which has room for 200px, asks for frames.
   */
  frames?: number;
  /** Seconds for one full turn. Slow on purpose: this is a backdrop. */
  period?: number;
  cloudThreshold?: number;
  /** Draw a ring system. Geometry, not pixels — see WorldRing. */
  ring?: boolean;
  /** Alt text. */
  title?: string;
}

/**
 * A world that appears immediately and starts turning when it can.
 *
 * This is a measured decision, not a flourish. Generating one 200px planet costs
 * about 100ms, which is fine. Generating the 24 frames that turn it costs about
 * half a second — and it is a synchronous pixel loop with `toDataURL` at the end,
 * so that half second is a frozen page, right when the player just clicked
 * something. There is no way to make the loop itself incremental without
 * restructuring the generator, so the work is deferred instead of spread:
 *
 *   1. Draw the still at full size straight away. ~100ms, and the panel is
 *      complete the moment it opens — a 200px world is five times the linear
 *      size of a map marker, which is the entire point of the overlay.
 *   2. Build the strip on a macrotask, then swap it in.
 *
 * Step 2 is not a spinner. The strip is generated once per (system, size, dpr)
 * and cached inside `planet.ts`, so the second visit to a system is instant, and
 * a second overlay open while the first is still generating is a cache hit.
 *
 * Deferring is what makes this honest rather than merely tolerable. Rendering
 * the still first is not a placeholder to be replaced by the "real" sprite: it
 * is the same generator at the same size, and if the strip never arrives the
 * panel still shows a correct planet.
 */

export default function WorldSprite(props: WorldSpriteProps) {
  // Guards the one window `onCleanup` cannot: the timer has been dequeued and is
  // inside `planetSheet` when the component goes away. Writing a signal after
  // unmount is a no-op in Solid rather than a crash, but the sprite would be
  // generated and dropped, which is the waste worth avoiding.
  let live = false;
  onMount(() => {
    live = true;
  });
  onCleanup(() => {
    live = false;
  });

  /**
   * Frames across one turn.
   *
   * 96 is not a round number anyone would pick by accident: at 200px a frame costs
   * about 32ms to generate, and 96 of them over 12 seconds is 8fps with a step of
   * 3.75 degrees of longitude. The 28 frames this replaced were 1.9fps with a step
   * of nearly 13 degrees, and a jump that size across a high-contrast band pattern
   * is plainly visible — it reads as a slideshow rather than as a turning planet.
   *
   * This is still stepped, and it is worth being plain about why: the reference is
   * smooth because it evaluates its noise per pixel per frame in a shader, at the
   * browser's framerate. A baked filmstrip's smoothness is frames divided by period,
   * and the frame count is bounded by what can be generated without stalling. Real
   * 60fps here means evaluating the planet live — a WebGL port for the overlay only,
   * with the baked sprite still serving the chart and the stills.
   */
  const frames = () => Math.max(1, Math.floor(props.frames ?? 0));

  /**
   * Two ratios, and the split matters.
   *
   * The still is generated at the display's real ratio, so it is sharp — it is
   * what the panel shows first, and what it keeps showing if the strip never
   * arrives. An earlier version collapsed both paths onto one ratio, which
   * quietly gave the still 1x on a 2x display: a soft, half-resolution planet
   * that was then covered by a crisp strip 500ms later, and stayed soft forever
   * if generation was skipped.
   *
   * The strip is generated at 1x. Rotation hides resampling — a frame is on
   * screen for a fiftieth of a second and the eye averages it — but it does not
   * hide cost: 2x is four times the pixels, and measured at 200px x 24 frames
   * that is the difference between 540ms and 1.9 seconds. 1x pixel art upscaled
   * with `image-rendering: pixelated` is still pixel art, and it is still a
   * 200px sprite on screen.
   */
  const realDpr = () => (typeof window === "undefined" ? 1 : window.devicePixelRatio || 1);
  const stripDpr = () => 1;

  const [sheet, setSheet] = createSignal<string>();

  createEffect(() => {
    // Rotation is the reason to open this panel, so `prefers-reduced-motion` is the
    // only thing that turns it off — and it has to be read INSIDE the effect, or a
    // change to the setting would not be noticed.
    const n = reducedMotion() ? 1 : frames();
    const target = props.seed;
    // Read first so the effect re-subscribes to a prop change.
    const px = props.px;
    const type = props.type;
    const th = props.cloudThreshold;
    if (n <= 1) {
      setSheet(undefined);
      return;
    }
    setSheet(undefined);
    // Spread across macrotasks. A 200px frame costs about 32ms on this machine, so
    // the frame count that actually looks smooth is ~96 and that is ~3.1 seconds of
    // pixel loop. Done in one go it is a frozen page; the still is on screen and
    // correct the whole time, and the strip swaps in when it is ready.
    const job = planetSheetAsync(
      { seed: target, type, px, dpr: stripDpr(), cloudThreshold: th },
      n,
      (uri) => {
        // A system can be dismissed while its strip is generating.
        if (!live) return;
        setSheet(uri);
      },
      { batch: 4 },
    );
    onCleanup(() => job.cancel());
  });

  return (
    <div
      class="world"
      classList={{ turning: sheet() !== undefined, ringed: props.ring === true }}
      style={{ width: `${props.px}px`, height: `${props.px}px` }}
    >
      {/* ONE canvas, behind the sprite.

          This used to be two SVGs — a far half behind the `<img>` and a near half in
          front of it — which relied on the sprite being transparent outside its disc
          to do the occluding. It is the reference's own arrangement now: the shader
          cuts the planet's hole out of the ring's upper half, in its own uv, so the
          occlusion is exact and there is no second layer to keep aligned.

          The far/near split survives as a fact about the ring's own geometry — the
          shader's hole test is `if (uv.y < 0.5)` — but it is the shader's business
          now, not the DOM's. */}
      <Show when={props.ring === true}>
        <WorldRingGL planetPx={props.px} seed={props.seed} />
      </Show>
      {/* The still is always in the DOM. When the strip arrives it fades in
          over the top, so there is never a blank frame between the two. */}
      <Show when={stillUri(props, realDpr())} keyed>
        {uri => (
          <img class="world-still" src={uri} alt={props.title ?? ""} />
        )}
      </Show>
      {/* `keyed` is load-bearing, and its absence was why no world ever turned.
          Without it Solid hands the child an ACCESSOR, not the value, so `uri` was a
          function and the inline style became `url(() => uri)` — which is not a
          background image at all. The element rendered, the animation ran,
          `background-position` advanced, `background-size` was correct, `playState`
          was `running`, and the strip was transparent. Every signal said rotation was
          working, because rotation WAS working: on nothing. The static sprite behind
          it is what the viewer saw.

          Worth noting that the checks written for this all passed for the entire time
          it was broken, because they asked whether generation was deferred and
          whether the loop closed — both of which are properties of the strip, and the
          strip was being built perfectly. Nothing asked whether it was visible. */}
      <Show when={sheet()} keyed>
        {uri => (
          <div
            class="world-turn"
            role={props.title ? "img" : undefined}
            aria-label={props.title}
            style={{
              "background-image": `url(${uri})`,
              "background-size": `${props.px * frames()}px ${props.px}px`,
              "animation-duration": `${props.period ?? 15}s`,
              "animation-timing-function": `steps(${frames()})`,
              "--world-end": `-${props.px * frames()}px`,
            }}
          />
        )}
      </Show>

    </div>
  );
}

/** The still, as a data URI. Cheap enough to do inline. */
function stillUri(props: WorldSpriteProps, dpr: number): string | undefined {
  return planetUri({
    seed: props.seed,
    type: props.type,
    px: props.px,
    dpr,
    cloudThreshold: props.cloudThreshold,
  });
}