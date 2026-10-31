/**
 * A black hole: a dark horizon with a photon ring, and a warped accretion disc.
 *
 * Ported from the reference generator's `BlackHole.gdshader` and
 * `BlackHoleRing.gdshader`, and this file exists because of them. The obvious
 * reading of "black hole" is a flat annulus around a dark circle, which is what
 * the first attempt here was, and it looks like a diagram of a black hole rather
 * than like one.
 *
 * ## The warp is the whole thing
 *
 * The reference does not bend an ellipse. It builds one in a space it has
 * displaced, and draws wherever the *displaced* coordinate lands inside the
 * annulus — so what you see is the **preimage** of an ellipse under a non-linear
 * map. The displacement is:
 *
 *     if (uv.y < 0.5)       uv.y += bump(distance_to_centre)   // upper half: up
 *     else if (uv.y > 0.53) uv.y -= bump(distance_to_centre)   // lower half: down
 *
 * where `bump` is 1 at the centre and falls to 0 at the rim. The two halves are
 * pulled *apart* in opposite directions, by an amount that is largest where the
 * disc is closest to the singularity. Material near the middle is flung outward
 * and off the top or bottom of the sprite, so the annulus opens up into a twisted
 * shape with a hole in the middle — which is why it reads as something being
 * dragged around a gravity well rather than as a hoop.
 *
 * This is also the reason it is baked rather than drawn as SVG geometry, which the
 * ring beside it is. A ring's boundary is an ellipse and can be a path; this
 * boundary is the solution set of a nonlinear equation and cannot. Trying to
 * approximate it with a handful of warped control points would produce something
 * that is nearly right and reads as nearly right, which is worse than either
 * extreme.
 *
 * The disc is also *textured* — `disk *= pow(fbm(...), 0.5)` — and the texture
 * rotating against a fixed shape is most of what makes it look like material in
 * orbit. That is unachievable with flat fills at any path complexity.
 *
 * Attribution: the reference is Deep-Fold's PixelPlanets, MIT licensed.
 */

import {
  ALPHA_CUT,
  DISC,
  DISK_WIDTH,
  HOLE,
  DISC_PIXELS,
  HOLE_CANVAS_RATIO,
  HOLE_LIGHT_WIDTH,
  HOLE_RADIUS,
  N_COLORS,
  NOISE_SIZE,
  OCTAVES,
  PERSPECTIVE,
} from "./blackhole-consts";
import { TAU, fbm, fract, hexToRgb, smoothstep, type RGB } from "./paint";

/** Bump when the noise changes, so cached sprites regenerate. */
export const BLACKHOLE_ALGO_VERSION = 8;

export interface BlackHoleOpts {
  seed: number;
  /** Width of the whole sprite in CSS pixels: horizon and disc together. */
  px: number;
  dpr?: number;
}

interface Frame {
  d: number;
  /** Horizon radius in device pixels. */
  hr: number;
  /**
   * Width of each of the horizon's two light bands, in device pixels.
   *
   * The scene's `light_width` is in the horizon sprite's own uv, and the shader
   * measures in that same space, so it needs no conversion here. In the combined
   * sprite it has to be divided by three exactly once — the same trap `hr` fell
   * into, which is why it is spelled out rather than left implicit.
   */
  hlw: number;
  /** How much of the sprite the disc's un-warped annulus spans. */
  hole: RGB[];
  disc: RGB[];
  /** The reference's `disk_width` uniform. Their default is 0.1. */
  diskWidth: number;
  /** How much of the sprite the disc spans, reduced to leave room for the tilt. */
  discScale: number;
  /** Fixed tilt, so each system is tipped differently. */
  tilt: number;
  size: number;
  seed: number;
}

function prep(o: BlackHoleOpts, d: number): Frame {
  // The reference draws the disc on a canvas three times the horizon's and lets it
  // run to the edges, with the horizon at a little under a third of the width. The
  // two are baked into one sprite here, because a chart marker is one box and a
  // second element to position is a second thing to get wrong at 26px.
  // 0.247 is the scene's radius in the HORIZON SPRITE's own uv, and that sprite is
  // a third of the disc's canvas, so it is 0.247/3 of the canvas. It is not divided
  // by three anywhere else: the shader's uv is already scaled up from the canvas, so
  // dividing here as well divided twice and made the horizon three times too small.
  const hr = d * (HOLE_RADIUS / HOLE_CANVAS_RATIO);
  return {
    d,
    hr,
    hlw: d * (HOLE_LIGHT_WIDTH / HOLE_CANVAS_RATIO),
    hole: HOLE.map(hexToRgb),
    disc: DISC.map(hexToRgb),
    diskWidth: DISK_WIDTH,
    discScale: 1.0,
    /**
     * A seeded tip, and never a flat one.
     *
     * The warp is applied in the rotated frame, so the rotation decides which parts
     * of the sprite get displaced — it is not a cosmetic angle. At a tilt near zero
     * the displacement is symmetric about the sprite's horizontal midline, and the
     * result is a straight bar lying across the middle with a matching crescent above
     * and below it: a diagram of a black hole rather than one. Seeding it in
     * `0.4..1.1` radians either way keeps every disc visibly tipped.
     */
    tilt: (fract(o.seed * 0.6180339887) < 0.5 ? -1 : 1) *
      (0.22 + fract(o.seed * 0.2718281) * 0.45),
    /**
     * The scene's `size`, which is the noise's tiling period and not its frequency:
     * 6.598, not the 50 the shader declares. The scene's value is also the reason
     * `rand` tiles at `2*size` by `size` rather than over a unit square, and it is
     * what keeps the gas turbulent instead of a smooth cut-out.
     */
    size: NOISE_SIZE,
    seed: (o.seed | 0) ^ 0x51ed,
  };
}

/**
 * The displacement ramp, transcribed exactly.
 *
 * `smoothstep(d, outer, inner)` in the reference is a call with its edges the wrong
 * way round, which GLSL leaves undefined and which in practice computes
 * `clamp((x - e0) / (e1 - e0))` with `x` being the very argument passed as `e0`.
 * That gives `clamp((inner - d) / (outer - d))`, and the shape is the part that
 * matters: it **peaks at `inner / outer` at the centre and is already zero by
 * `inner`**. It is not a plateau.
 *
 * I had it as a plateau at 1.0 out to `inner`, falling to 0 at `outer`, and
 * multiplied the result by one. So the disc was being displaced by a full
 * sprite-height where theirs moves it by 0.4 — which is what tore the annulus into
 * a disconnected bar and a detached arc instead of one ribbon sweeping past the
 * void.
 *
 * Written out rather than left as a reversed smoothstep, because "reversed" is
 * exactly the part that is ambiguous and it is now load-bearing four times over.
 */
function bump(dd: number, outer: number, inner: number): number {
  if (dd >= inner) return 0;
  const den = outer - dd;
  if (den <= 1e-6) return 0;
  const t = Math.max(0, Math.min(1, (inner - dd) / den));
  // The reference's smoothstep CURVE, not the linear ratio.
  return t * t * (3 - 2 * t);
}

function renderFrame(
  f: Frame,
  out: ImageData,
  stride: number,
  ox: number,
  oy: number,
  spin: number,
) {
  const { d, hr, hlw, hole, disc, tilt, size, seed } = f;
  const px = out.data;
  /**
   * A direct transliteration of the reference's `fragment()`, in its order and with
   * its arithmetic, rather than a paraphrase of it.
   *
   * Three separate defects in this file all came from paraphrasing. The first was
   * reading `smoothstep(d, outer, inner)` — a call with its edges reversed, which
   * GLSL leaves undefined — as a plateau at 1.0 rather than as a ramp peaking at
   * `inner / outer`; that displaced the disc by a whole sprite height where the
   * original moves it 0.4, and it is what tore the annulus into a disconnected bar
   * and a detached arc. The second was computing the displacement ramp from the
   * distance to the sprite centre in the *unrotated* frame while the geometry used
   * the rotated one. The third was the alpha threshold. Each was a transcription
   * error rather than a design error, and the only reliable way to stop making them
   * is to stop rewriting the thing in your own shape.
   *
   * So: same statements, same order, same constants. The two deviations are marked
   * where they occur.
   */
  const rot = (x: number, y: number, a: number): [number, number] => {
    const c = Math.cos(a);
    const sn = Math.sin(a);
    const dx = x - 0.5;
    const dy = y - 0.5;
    return [dx * c - dy * sn + 0.5, dx * sn + dy * c + 0.5];
  };
  // DEVIATION 1: their texture rotation is `time * time_speed * 3.0`, and with
  // time_speed 0.2 that is 0.6 of a turn across the loop — which does not close, so
  // frame 0 and frame N would differ. One whole turn does, and at this scale the
  // rate is not the point. The wobble below keeps their factor and is periodic
  // either way.
  const tRot = spin * TAU;
  const wobble = Math.sin(spin * TAU * 0.4) * 0.01;

  for (let y = 0; y < d; y++) {
    for (let x = 0; x < d; x++) {
      const i = ((y + oy) * stride + (x + ox)) * 4;
      const u = (x + 0.5) / d;
      const v = (y + 0.5) / d;

      let final: RGB | null = null;

      // ---- the horizon, UNDERNEATH ----------------------------------------
      // BlackHole.gdshader first, because in `BlackHole.tscn` it is node 0 and the
      // disc is node 1 and Godot draws later siblings on top. The disc is therefore
      // composited OVER this, which is the depth cue: the band crosses in front of
      // the singularity and the photon ring is broken where it does.
      //
      // The banding is two hard thresholds on the radius, not a falloff — the
      // reference has no gradient in here at all.
      const dr = Math.hypot(u - 0.5, v - 0.5) * d;
      if (dr <= hr) {
        final = hole[0];
        if (dr > hr - hlw) final = hole[1];
        if (dr > hr - hlw * 0.5) final = hole[2];
      }

      // ---- the disc, over the horizon --------------------------------------
      {
        // The reference's dither, transcribed: `dither(UV, uv)` takes the RAW uv
        // first and the QUANTISED one second, and tests against `2/pixels`. The
        // ordering is load-bearing — passing the quantised value for both is a
        // different pattern — and so is `pixels`, which the scene sets to 300.
        const qv = Math.floor(v * DISC_PIXELS) / DISC_PIXELS;
        const dith = fract(u + qv) <= 1 / DISC_PIXELS;

        let [pxu, pyu] = rot(u, v, tilt);
        const uv2x = pxu;
        const uv2y = pyu;
        pxu = (pxu - 0.5) * 1.3 + 0.5;
        [pxu, pyu] = rot(pxu, pyu, wobble);

        let lx = 0.5;
        let ly = 0.5;
        let dWidth = f.diskWidth;
        // The distance is taken from the CURRENT uv, after the rotation and the x
        // scale but before the y displacement — the order matters and getting it
        // wrong mixes two coordinate frames.
        if (pyu < 0.5) {
          const dd = Math.hypot(pxu - 0.5, pyu - 0.5);
          pyu += bump(dd, 0.5, 0.2);
          dWidth += bump(dd, 0.5, 0.3);
          ly -= bump(dd, 0.5, 0.2);
        } else if (pyu > 0.53) {
          const dd = Math.hypot(pxu - 0.5, pyu - 0.5);
          pyu -= bump(dd, 0.4, 0.17);
          dWidth += bump(dd, 0.5, 0.2);
          ly += bump(dd, 0.5, 0.2);
        }

        // The SCENE's ring_perspective, 14.0. The shader declares 4.0 and the
        // scene overrides it, and 4.0 is the reason this read as a fat ellipse for
        // so long: four to one instead of fourteen to one. It scales the light
        // vector as well as the disc, which is kept.
        const PERSP = PERSPECTIVE;
        const lightD = Math.hypot(uv2x - lx, (uv2y - ly) * PERSP) * 0.3;

        // `uv_center = uv - vec2(0, 0.5)`, then `*= vec2(1, ring_perspective)`,
        // and the reference point is (0.5, 0) in that space — which is the sprite
        // centre, since (0.5, 0.5) maps to (0.5, 0).
        // Scaled to leave room for the tilt. The reference's disc canvas is three
        // times the horizon's, so its ring can be tipped 0.7 radians without
        // anything leaving the frame. Here they share one box, and at full width a
        // 40-degree tilt swings the ring's ends clean off the sprite — which is what
        // made the disc read as small and oddly clipped.
        const cx0 = (pxu - 0.5) * f.discScale;
        let cy = (pyu - 0.5) * PERSP * f.discScale;
        const cdist = Math.hypot(cx0, cy);

        let disk = smoothstep(
          (cdist - (0.1 - dWidth * 2.0)) / (0.5 - dWidth - (0.1 - dWidth * 2.0)),
        );
        disk *= smoothstep((0.4 - (cdist - dWidth)) / dWidth);

        // Texture, rotating against the fixed shape.
        let cx = cx0;
        [cx, cy] = rot(cx, cy + 0.5, tRot);
        const n = fbm(cx * size, cy * size, 64, OCTAVES, seed + 17);
        disk *= Math.pow(Math.max(0, n), 0.5);
        if (dith) disk *= 1.2;

        // A step, not a ramp: the reference's alpha is `step(0.15, disk)`, opaque
        // or absent, and the palette is chosen independently of it. Treating this as
        // a ramp is most of what turns the band into a smear.
        if (disk >= ALPHA_CUT) {
          const idx = Math.max(
            0,
            Math.min(N_COLORS - 1, Math.floor((disk + lightD) * (N_COLORS - 1))),
          );
          final = disc[idx];
        }
      }

      if (!final) continue;
      px[i] = final[0];
      px[i + 1] = final[1];
      px[i + 2] = final[2];
      px[i + 3] = 255;
    }
  }
}

const cache = new Map<string, string>();

/**
 * The key is built from the option object's own sorted keys for the same reason
 * `planet.ts` does it that way: a hand-written list silently drops the next option
 * added, and the symptom is a test that reads a cached image of the run before it.
 */
function keyOf(o: BlackHoleOpts, spin: number, frames: number): string {
  const opts = Object.keys(o)
    .sort()
    .map((k) => `${k}=${String((o as unknown as Record<string, unknown>)[k])}`)
    .join(",");
  return [BLACKHOLE_ALGO_VERSION, frames, spin.toFixed(4), opts].join("|");
}

function renderToDataUri(o: BlackHoleOpts, frames: number): string {
  const d = Math.max(3, Math.round(o.px * (o.dpr ?? 1)));
  const cv = document.createElement("canvas");
  cv.width = d * frames;
  cv.height = d;
  const ctx = cv.getContext("2d");
  if (!ctx) return "";
  const img = ctx.createImageData(d * frames, d);
  const f = prep(o, d);
  for (let k = 0; k < frames; k++) renderFrame(f, img, d * frames, k * d, 0, k / frames);
  // Without this the pixels are written and never blitted: `createImageData` hands
  // back a detached buffer, and `toDataURL` reads the canvas, not the buffer. The
  // sprite comes out 400x400 of pure transparency and costs 12ms, which is the tell.
  ctx.putImageData(img, 0, 0);
  return cv.toDataURL("image/png");
}

export function blackHoleUri(o: BlackHoleOpts): string {
  const k = keyOf(o, 0, 1);
  const hit = cache.get(k);
  if (hit) return hit;
  const uri = renderToDataUri(o, 1);
  cache.set(k, uri);
  return uri;
}

export interface BlackHoleSheet {
  uri: string;
  frames: number;
  px: number;
}

export function blackHoleSheet(o: BlackHoleOpts, frames: number): BlackHoleSheet {
  const k = keyOf(o, 0, frames);
  const hit = cache.get(k);
  if (hit) return { uri: hit, frames, px: o.px };
  const uri = renderToDataUri(o, frames);
  cache.set(k, uri);
  return { uri, frames, px: o.px };
}

export function clearBlackHoleCache(): void {
  cache.clear();
}
