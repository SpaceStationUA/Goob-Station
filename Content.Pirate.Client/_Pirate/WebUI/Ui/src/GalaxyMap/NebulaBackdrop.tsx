/**
 * The nebula behind the chart.
 *
 * Two things this is not, both of which were tried first:
 *
 * **It is not drawn per frame.** Neither layer moves — both are pure functions of
 * `(uv, size, seed, pixels)` — so an animation loop would recompute two eight-octave
 * fbm chains every frame to produce a pixel-identical image. It is rendered on mount
 * and on resize.
 *
 * **It is not a background IMAGE.** It is a canvas, because the generator is
 * procedural and at 500 UV divisions the grain has to stay the same size as the
 * planets' when the window resizes. A baked PNG would be crisp at one size and soft
 * at every other.
 *
 * If WebGL is unavailable this renders nothing and the chart keeps the flat page
 * colour. That is deliberate: a missing nebula is a cosmetic loss, and throwing in
 * the chart's mount path would be a broken chart.
 */

import { createEffect, onCleanup, onMount } from "solid-js";
import { nebula } from "./lib/nebula";

export interface NebulaBackdropProps {
  /**
   * Dial the whole thing back.
   *
   * The reference's own `reduce_background`, and on this chart it is the difference
   * between a nebula and a chart. Twenty-odd territory labels, a route list and a
   * graticule sit on top of this, and the reference's palette runs from a saturated
   * red through orange to cream. At full strength the labels have to fight it.
   */
  reduce?: boolean;
  seed?: number;
  /** Index into `PALETTES`. Changing it rebuilds. */
  palette?: number;
}

export default function NebulaBackdrop(props: NebulaBackdropProps) {
  let host!: HTMLDivElement;

  const build = () => {
    const box = host.getBoundingClientRect();
    if (box.width < 8) return;
    const n = nebula({
      px: Math.round(box.width),
      reduce: props.reduce,
      seed: props.seed,
      palette: props.palette,
    });
    if (!n) return;
    n.canvas.classList.add("nebula-canvas");
    host.dataset.layers = String(n.layers);
    host.dataset.palette = n.palette;
    host.replaceChildren(n.canvas);
    onCleanup(() => n.dispose());
  };

  onMount(() => {
    build();
    // Resize only, debounced. The generation is two fbm chains over the whole canvas
    // and doing that on every resize frame is the one way to make this expensive.
    let t: ReturnType<typeof setTimeout> | undefined;
    // A palette change has to rebuild too, and that is not a resize, so the
    // observer alone will not catch it.
    createEffect(() => {
      props.palette;
      props.reduce;
      build();
    });
    const ro = new ResizeObserver(() => {
      if (t) clearTimeout(t);
      t = setTimeout(build, 180);
    });
    ro.observe(host);
    onCleanup(() => {
      if (t) clearTimeout(t);
      ro.disconnect();
    });
  });

  return <div ref={host} class="nebula-backdrop" aria-hidden="true" />;
}