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

import { BAYER4, TAU, fbm, fract, hexToRgb, smoothstep, type RGB } from "./paint";

/** Bump when the noise changes, so cached sprites regenerate. */
export const BLACKHOLE_ALGO_VERSION = 1;

export interface BlackHoleOpts {
  seed: number;
  /** Width of the whole sprite in CSS pixels: horizon and disc together. */
  px: number;
  dpr?: number;
}

/**
 * The horizon's three steps, inward to outward: the void, a dim warm ring, and
 * the thin bright photon ring at the edge.
 *
 * The void is not `#000`. The chart's page is near black, so a true black disc is
 * a hole in the chart rather than an object in it — the eye reads a gap as a bug.
 * This was measured, not assumed: the photon ring sits at luminance 229 and the
 * horizon at 7, and it is that contrast, not the shape, that makes a black hole
 * legible at 26px.
 */
const HOLE: [string, string, string] = ["#0b0912", "#e6d6bc", "#fffaf0"];

/** The disc, dark to hot. Five steps, matching the reference's `n_colors`. */
const DISC: [string, string, string, string, string] = [
  "#5e1f0e",
  "#9c3d18",
  "#cf7529",
  "#efb257",
  "#fdeab4",
];

interface Frame {
  d: number;
  /** Horizon radius in device pixels. */
  hr: number;
  /** How much of the sprite the disc's un-warped annulus spans. */
  discR: number;
  hole: RGB[];
  disc: RGB[];
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
  const hr = d * 0.15;
  return {
    d,
    hr,
    discR: d * 0.5,
    hole: HOLE.map(hexToRgb),
    disc: DISC.map(hexToRgb),
    // A shallow, seeded tip. The reference exposes this as a uniform and sets it to
    // `rotation + 0.7`; seeding it means a chart with two of these does not show
    // two identically-posed discs.
    tilt: (fract(o.seed * 0.6180339887) - 0.5) * 1.9,
    /**
     * Cells across the disc. The reference uses `size = 50` on a 300px canvas;
     * 5 here gave about five cells over the whole structure, so the band came out
     * as a smooth cut-out shape with a gradient in it rather than as turbulent
     * gas. This is the difference between "a bar" and "a disc".
     */
    size: 18,
    seed: (o.seed | 0) ^ 0x51ed,
  };
}

/**
 * The displacement ramp: 1 at the centre, 0 at `outer`, smooth between.
 *
 * The reference writes this as `smoothstep(d, 0.5, 0.2)` — edges the wrong way
 * round. GLSL leaves that undefined, and in practice it computes
 * `clamp((x - a) / (b - a))`, which with `a > b` is a *decreasing* ramp. Spelled
 * out here so the sign is not a matter of faith: this is a bump that falls off
 * outward, and getting it the other way up throws the disc off the sprite.
 */
function bump(dd: number, outer: number): number {
  return 1 - smoothstep((dd - outer * 0.4) / (outer * 0.6));
}

function renderFrame(f: Frame, out: ImageData, stride: number, ox: number, oy: number, spin: number) {
  const { d, hr, discR, hole, disc, tilt, size, seed } = f;
  const px = out.data;
  const cosT = Math.cos(tilt);
  const sinT = Math.sin(tilt);
  // One full turn of the disc's texture per loop, so frame 0 and frame N match.
  // The reference spins it at 314x the planet's rate, which cannot close on a
  // frame budget that size; the visual cue is the same at any rate.
  const texSpin = spin * TAU;

  for (let y = 0; y < d; y++) {
    for (let x = 0; x < d; x++) {
      const i = ((y + oy) * stride + (x + ox)) * 4;
      // Normalised sprite coords, 0..1.
      const u = (x + 0.5) / d;
      const v = (y + 0.5) / d;
      // Distance from the sprite's centre, in the same units the bump uses.
      const dC = Math.hypot(u - 0.5, v - 0.5);

      let outRgb: RGB | null = null;

      // ---- the disc, as the preimage of an annulus under the warp ----------
      {
        // Rotate about the centre, then widen. `uv2` is kept pre-warp because the
        // reference computes the lighting from it.
        const rx = (u - 0.5) * cosT - (v - 0.5) * sinT + 0.5;
        const ry = (u - 0.5) * sinT + (v - 0.5) * cosT + 0.5;
        const ux = (rx - 0.5) * 1.3 + 0.5;
        const uy0 = ry;

        let wy = uy0;
        let dWidth = 0.115;
        let lightY = 0.5;
        const b = bump(dC, 0.5);
        if (uy0 < 0.46) {
          // Upper half: pushed up, and the band is widened where it is closest in.
          wy = uy0 + b;
          dWidth += bump(dC, 0.42);
          lightY -= b;
        } else if (uy0 > 0.56) {
          // Lower half: pushed down. The 0.5..0.53 dead band is the reference's, and
          // it is what keeps the two displacements from tearing the disc apart
          // along the sprite's horizontal midline.
          wy = uy0 - bump(dC, 0.34);
          dWidth += bump(dC, 0.4);
          lightY += bump(dC, 0.4);
        }

        // The annulus, in the displaced space, squashed 4:1 for the viewing angle.
        const cx = (ux - 0.5) * 1.0;
        const cy = (wy - 0.5) * 4.0;
        const centerD = Math.hypot(cx, cy);

        // smoothstep(e0, e1, x), written the way our smoothstep takes it. The
        // reference's two calls are smoothstep(0.1 - 2w, 0.5 - w, centre_d) and
        // smoothstep(centre_d - w, centre_d, 0.4); the second one's edges depend on
        // centre_d, which is unusual enough to be worth spelling out.
        const e0 = 0.1 - dWidth * 2.0;
        const e1 = 0.5 - dWidth;
        let disk = smoothstep((centerD - e0) / (e1 - e0));
        disk *= smoothstep((0.4 - (centerD - dWidth)) / dWidth);

        if (disk > 0) {
          // Texture, rotating against the fixed shape. `pow(fbm, 0.5)` lifts the
          // mid-tones, which is what turns a noisy field into something that reads
          // as glowing gas rather than as dirt.
          const tcx = cx * Math.cos(texSpin) - cy * Math.sin(texSpin);
          const tcy = cx * Math.sin(texSpin) + cy * Math.cos(texSpin);
          const n = fbm(tcx * size, tcy * size, 64, 3, seed + 17);
          disk *= Math.pow(Math.max(0, n), 0.5);

          // Their dither. Two steps of a 2x2 ordered pattern; BAYER4 stands in,
          // which is the same idea at twice the resolution.
          const dith = BAYER4[(y & 3) * 4 + (x & 3)] / 16;
          if (dith < 0.5) disk *= 1.2;

          if (disk > 0.15) {
            // Lighting from the pre-warp coordinate, so the bright side does not
            // swim around with the warp.
            const lightD =
              Math.hypot((ux - 0.5) * 1.0, (uy0 - lightY) * 4.0) * 0.3;
            const idx = Math.max(
              0,
              Math.min(disc.length - 1, Math.floor((disk + lightD) * (disc.length - 1))),
            );
            outRgb = disc[idx];
          }
        }
      }

      // ---- the horizon, under the disc -------------------------------------
      // The disc goes on top so a ray can cross in front of it, which is the cue
      // that reads as depth. Everywhere else the warp has pushed the disc clear of
      // the centre anyway.
      // Horizon first, disc over it: a ray crossing in front of the void is the
      // depth cue, and it cannot be expressed the other way round.
      let final: RGB | null = null;
      const dr = Math.hypot(u - 0.5, v - 0.5) * d;
      if (dr <= hr) {
        const t = dr / hr;
        final = t < 0.9 ? hole[0] : t < 0.95 ? hole[1] : hole[2];
      }
      if (outRgb) final = outRgb;
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
