/**
 * A ring system, drawn as geometry rather than baked into the sprite.
 *
 * The reference bakes its rings into the planet texture, on a canvas three times
 * the planet's resolution and six times its radius. That is the wrong trade here,
 * and the first two reasons are about the map rather than the overlay:
 *
 *  1. **At map size a baked ring is mush.** Markers are 16 to 40px. Six times the
 *     planet's radius is either a grey smudge at that size or it has to be drawn
 *     small enough to still be a ring, and 200px of generated pixels per system
 *     to produce a smudge is not affordable on a chart with twenty-odd of them.
 *  2. **It makes the sprite non-square.** Every sprite in `planet.ts` is `d` on a
 *     side and each marker sizes itself from that, so a ringed world would have to
 *     report a different box and every consumer would need to know about it.
 *  3. **A ring is an ellipse.** It is geometry, it wants to be vector, and both
 *     consumers here are already SVG — so it stays sharp at 16px and at 200px
 *     with one path and no pixels at all.
 *
 * The far half goes BEHIND the sprite and the near half IN FRONT, which is what
 * sells it as a ring passing round a planet rather than a hoop drawn over one. In
 * the chart that is two paths either side of the `<image>`; in the overlay it is
 * two absolutely-positioned SVGs either side of the `<img>`. The occlusion is
 * free in both: the sprite is opaque across its disc and transparent outside it,
 * so the far half is hidden exactly where the planet is and visible exactly where
 * it is not.
 *
 * Deliberately not animated. The reference's ring turns — but its ring carries fbm
 * texture, so the turn is visible. A flat annulus rotating about its own centre is
 * a no-op, and animating it would buy a compositor layer and no change. This is
 * also the one place where a genuinely different rotation rate from the planet is
 * both wanted and affordable; see the note in the README.
 */

import { For, Show } from "solid-js";
import { hexToRgb } from "./lib/paint";

export interface RingGeom {
  /** Outer semi-major axis, in pixels. */
  rx: number;
  /** Outer semi-minor axis. Small relative to `rx` is nearly edge-on. */
  ry: number;
  /** Band thickness in pixels. */
  band: number;
  /**
   * Where the division is, in radians. Omit for a complete ring.
   *
   * Without it the ring reads as a drawn ellipse; with it, as an object.
   */
  gapAngle?: number;
  /** Half-width of the division, in radians. */
  gapHalf?: number;
}

/**
 * One half of a ring, as an SVG path.
 *
 * The two halves are the same shape with opposite winding — there is no separate
 * "back" and "front" geometry, only which way round the arc is drawn. Returns an
 * empty string when the division has eaten the whole half, so a caller can skip
 * emitting a path at all rather than emitting a degenerate one.
 *
 * Angles run anticlockwise from +x in an SVG's y-down frame, so the *near* half
 * (the one that passes in front of the planet) is the lower arc, t in [0, PI].
 */
/**
 * One slice of one half of the ring.
 *
 * `from`/`to` are 0..1 across the band's width: 0 is the outer edge, 1 the inner.
 * Three slices in three tones is what stops a ring reading as a strip of paper laid
 * over a planet — a single fill has no interior, so the eye has nothing to model it
 * with and it sits on the disc like a sticker. Real ring systems are layered, and
 * three bands is the cheapest thing that says so.
 */
export function ringHalf(
  g: RingGeom,
  above: boolean,
  from = 0,
  to = 1,
): string {
  const rx = g.rx - g.band * from;
  const ry = g.ry - g.band * 0.34 * from;
  const rx2 = Math.max(0.5, g.rx - g.band * to);
  const ry2 = Math.max(0.5, g.ry - g.band * 0.34 * to);
  const TAU = Math.PI * 2;

  // Work in the half's own frame, where it is simply [0, PI]. Angles run
  // anticlockwise from +x in an SVG's y-down frame, so the *near* half — the one
  // that passes in front of the planet — is the lower arc.
  const off = above ? Math.PI : 0;
  const arc = (from: number, to: number) => {
    if (to - from < 1e-3) return "";
    const pt = (r: number, t: number) =>
      `${(Math.cos(t + off) * r).toFixed(2)} ${(Math.sin(t + off) * r).toFixed(2)}`;
    return (
      `M ${pt(rx, from)} A ${rx.toFixed(2)} ${ry.toFixed(2)} 0 0 1 ${pt(rx, to)} ` +
      `L ${pt(rx2, to)} A ${rx2.toFixed(2)} ${ry2.toFixed(2)} 0 0 0 ${pt(rx2, from)} Z`
    );
  };

  const half = g.gapHalf ?? 0;
  if (g.gapAngle === undefined || half <= 0) return arc(0, Math.PI);

  // The division as an interval in the half's frame. It may wrap past TAU, which
  // is fine — the comparisons below only care about the overlap with [0, PI].
  const norm = (t: number) => ((t % TAU) + TAU) % TAU;
  const a = norm(g.gapAngle - half - off);
  const b = norm(g.gapAngle + half - off);
  const width = b > a ? b - a : b + TAU - a;
  const cutLo = Math.max(0, Math.min(Math.PI, a));
  const cutHi = Math.max(0, Math.min(Math.PI, a + width));

  /**
   * A gap that falls entirely OUTSIDE this half leaves the half whole.
   *
   * This is the opposite of what the first version did, and it removed the entire
   * far half of every ringed world whose division happened to land in the near
   * half — which is most of them, since a gap is in one half or the other. The
   * symptom was not a missing ring but a ring with no far side, so it still looked
   * broadly ring-shaped at a glance and only a count of the path elements gave it
   * away.
   */
  if (cutHi <= cutLo + 1e-3) return arc(0, Math.PI);

  // Whatever survives, as one piece. With a division of at most 0.16 radians
  // against a half of pi there is always one dominant remainder, so splitting into
  // two sub-paths would buy nothing but a second element to keep in sync.
  return cutLo >= Math.PI - cutHi ? arc(0, cutLo) : arc(cutHi, Math.PI);
}

/** Ring geometry for a planet of `size` pixels across. */
export function ringGeomFor(size: number, seed: number, tilt?: number): RingGeom {
  /**
   * Two different ratios, because the two consumers have opposite constraints.
   *
   * The overlay's panel is 268px wide and the planet in it is 200, which leaves
   * 34px of headroom on each side — so the ring's outer radius has to stay under
   * 1.34x the planet's own, or it is clipped at both edges. At map scale there is
   * no panel and no clipping, and the ring is competing for attention with a
   * 16px sprite and a label, so it can afford to be proportionally larger.
   *
   * Both are well inside the real proportion, where Saturn's A ring reaches about
   * 2.3 planetary radii. That is not reachable here and not worth chasing: it
   * would be twice the panel's width.
   */
  const rx = size * (size >= 60 ? 0.64 : 0.78);
  return {
    rx,
    // More open at map size. A 16px marker with the overlay's tilt has a minor
    // axis of under two pixels, and the band has to fit inside that or the whole
    // ring collapses to a line the sprite's own dither eats.
    ry: rx * (tilt ?? (size >= 60 ? 0.24 : 0.34)),
    // Narrower than it was, for the same reason the colours moved: a wide band in a
    // tone close to the planet's own does not read as a separate object at all.
    band: rx * (size >= 60 ? 0.15 : 0.24),
    // One of four quadrants, so a chart with several ringed systems does not
    // show the same gap on all of them.
    gapAngle: [-2.5, -0.9, 0.9, 2.5][(seed >>> 0) % 4] + ((((seed >>> 3) % 100) / 100) - 0.5) * 0.7,
    gapHalf: size >= 60 ? 0.1 : 0.16,
  };
}

export interface WorldRingProps {
  /** Diameter of the planet in CSS pixels. The ring is sized from it. */
  px: number;
  /** 0..1, how open the ring is. */
  tilt?: number;
  /** Ring colour on the lit, near half. */
  color?: string;
  /** Ring colour on the far half, which is behind the planet. */
  dark?: string;
  seed?: number;
  /** Draw only the near half; the far half is a second instance behind the img. */
  front?: boolean;
}

export default function WorldRing(props: WorldRingProps) {
  const g = () => ringGeomFor(props.px, props.seed ?? 1, props.tilt);
  // The SVG box has to be big enough for the ring, and the planet is centred in
  // it, so the offset is negative by exactly half the difference.
  const box = () => g().rx * 2 + 2;
  const style = () => ({
    width: `${box()}px`,
    height: `${box()}px`,
    left: `${(props.px - box()) / 2}px`,
    top: `${(props.px - box()) / 2}px`,
  });
  /** Outer slice darkest, middle darkest, inner brightest: a lit face, a gap, a lit face. */
  const ringTone = (side: string, slice: number, p: WorldRingProps) => {
    const lit = p.color ?? "#f2ead9";
    const shade = p.dark ?? "#4a4034";
    const mixAt = (t: number) => {
      const a = hexToRgb(lit);
      const b = hexToRgb(shade);
      return `rgb(${Math.round(a[0] + (b[0] - a[0]) * t)},${Math.round(
        a[1] + (b[1] - a[1]) * t,
      )},${Math.round(a[2] + (b[2] - a[2]) * t)})`;
    };
    return side === "front"
      ? [mixAt(0.72), mixAt(1), mixAt(0.18)][slice]
      : [mixAt(0.85), mixAt(1), mixAt(0.5)][slice];
  };

  return (
    <Show when={props.front === true ? "front" : "back"}>
      {side => (
        <svg
          class={`world-ring world-ring-${side()}`}
          width={box()}
          height={box()}
          viewBox={`${-box() / 2} ${-box() / 2} ${box()} ${box()}`}
          style={style()}
          aria-hidden="true"
        >
          {/* Three concentric slices, not one fill. A single flat band has no
              interior for the eye to model, so it sits on the disc like a sticker
              rather than reading as an object in front of it — which is exactly how
              the one-band version looked on a cream gas giant. The middle slice is
              darkest, which is both how a ring reads (a shadowed gap between two lit
              faces) and how it separates from a planet of any colour. */}
          <For each={[0, 1, 2]}>
            {i => (
              <path
                d={ringHalf(g(), side() === "front", i / 3, (i + 1) / 3)}
                fill={ringTone(side(), i, props)}
              />
            )}
          </For>
        </svg>
      )}
    </Show>
  );
}
