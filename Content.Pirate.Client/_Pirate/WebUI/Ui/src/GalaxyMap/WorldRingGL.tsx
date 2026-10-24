/**
 * The ring in the system overlay, live on a canvas.
 *
 * Two reasons this exists rather than the SVG rings in `WorldRing.tsx`:
 *
 *  1. **The ring is carved by noise, and that needs pixels.** The reference's ring
 *     is an annulus multiplied by four octaves of fbm and cut with
 *     `step(0.28, …)`, so the divisions in it are where the noise fell below the
 *     threshold. Three flat ribbons cannot have divisions, which is what made the
 *     previous ring read as a wire hoop laid across the planet.
 *  2. **The planet's hole is the ring's own job.** The shader cuts it, in its own
 *     uv, so the occlusion is exact. The HTML version got the same effect by
 *     putting the far half behind a transparent `<img>`, which works right up until
 *     the two layers are not both positioned — which is exactly how it was broken.
 *
 * The SVG ring stays for the chart's markers, where 16px leaves no interior for
 * noise and the chart is pure SVG. `outerRadii` is shared so the two agree on the
 * ring's size instead of each guessing.
 */

import { createSignal, onCleanup, onMount, Show } from "solid-js";
import WorldRing from "./WorldRing";
import { CANVAS_TO_PLANET } from "./lib/ring-consts";
import { glRing, glRingSupported, outerRadii, rotationFor } from "./lib/gl-ring";

export interface WorldRingGLProps {
  /** Planet diameter in CSS pixels. */
  planetPx: number;
  seed: number;
  /** False for `prefers-reduced-motion`. */
  animate?: boolean;
}

export default function WorldRingGL(props: WorldRingGLProps) {
  let host!: HTMLDivElement;
  const canvasPx = () => props.planetPx * CANVAS_TO_PLANET;
  const g = () => outerRadii(props.planetPx, canvasPx());

  /**
   * A signal rather than a local, because the fallback has to be in the DOM and the
   * canvas has not been mounted yet at the point we find out whether it will work.
   */
  const [failed, setFailed] = createSignal(false);

  onMount(() => {
    const r = glRingSupported()
      ? glRing({
          seed: props.seed,
          planetPx: props.planetPx,
          canvasPx: canvasPx(),
          animate: props.animate,
        })
      : null;
    // No WebGL, or a shader that would not compile. The SVG ring is a worse ring but
    // it is a ring, and a ring system with no ring reads as a bug in the map rather
    // than as a missing feature. The shader reports its own compile failure to the
    // console, so this is not a silent downgrade.
    if (!r) {
      setFailed(true);
      return;
    }
    r.canvas.classList.add("world-ring-gl");
    host.appendChild(r.canvas);
    onCleanup(() => r.dispose());
  });

  return (
    <Show
      when={!failed()}
      fallback={
        <div class="world-ring-fallback" aria-hidden="true">
          <WorldRing px={props.planetPx} seed={props.seed} />
          <WorldRing px={props.planetPx} seed={props.seed} front />
        </div>
      }
    >
      <div
          ref={host}
        class="world-ring-gl-host"
      aria-hidden="true"
      style={{
          width: `${canvasPx()}px`,
          height: `${canvasPx()}px`,
          // The shader centres the ring on its canvas and cuts the planet's hole at
          // the planet's own radius, so the planet must be centred on that canvas
          // too. That alignment is the entire contract, and it is why this can be
          // one canvas rather than a far half and a near half.
          left: `${(props.planetPx - canvasPx()) / 2}px`,
          top: `${(props.planetPx - canvasPx()) / 2}px`,
      }}
      // Geometry the checks read rather than pixels, because a ring's proportions
      // are checkable and its noise is not.
      data-ring-outer={Math.round(g().rx)}
      data-ring-hole={Math.round(g().holeR)}
          data-ring-canvas={Math.round(canvasPx())}
        // The ring's own rotation, in radians.
        //
        // Not for drawing — for checking. The shader decides which side of the ring
        // passes in front by testing `uv.y < 0.5` on the ROTATED uv, so the boundary
        // between "in front of" and "behind" is a line through the planet's centre at
        // this angle, not the horizontal. A check that assumed the horizontal found
        // 741 lit pixels above the planet's centre and called it a bug, when those
        // pixels were on the near side of a 40-degree line.
        data-ring-rotation={rotationFor(props.seed)}
      />
    </Show>
  );
}
