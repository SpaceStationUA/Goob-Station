/**
 * Primitives for painting a procedural body: noise, colour, dither.
 *
 * Extracted from `planet.ts` when the black hole needed the same handful of
 * helpers, and importing them from a module called "planet" would have been a lie
 * about what that module is. Both are per-pixel generators that share exactly
 * these primitives, and anything else procedural would look here too.
 *
 * Nothing here knows what a planet is.
 */

/** Integer hash. See the note at the top of the file about why not sin(). */
export function hash2(ix: number, iy: number, period: number, seed: number): number {
  const x = ((ix % period) + period) % period;
  const y = ((iy % period) + period) % period;
  let h = Math.imul(x, 0x27d4eb2d) ^ Math.imul(y, 0x165667b1) ^ Math.imul(seed | 0, 0x9e3779b9);
  h = Math.imul(h ^ (h >>> 15), 0x85ebca6b);
  h ^= h >>> 13;
  h = Math.imul(h, 0xc2b2ae35);
  h ^= h >>> 16;
  return (h >>> 0) / 4294967296;
}

/** Value noise that tiles on `period`, so the far side of the sphere matches. */
export function vnoise(x: number, y: number, period: number, seed: number): number {
  const ix = Math.floor(x);
  const iy = Math.floor(y);
  const fx = x - ix;
  const fy = y - iy;
  const ux = fx * fx * (3 - 2 * fx);
  const uy = fy * fy * (3 - 2 * fy);
  const a = hash2(ix, iy, period, seed);
  const b = hash2(ix + 1, iy, period, seed);
  const c = hash2(ix, iy + 1, period, seed);
  const d = hash2(ix + 1, iy + 1, period, seed);
  return a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy;
}

/** Summed octaves. The period doubles with each one so tiling survives. */
export function fbm(x: number, y: number, period: number, octaves: number, seed: number): number {
  let v = 0;
  let amp = 0.5;
  let p = period;
  let fx = x;
  let fy = y;
  for (let i = 0; i < octaves; i++) {
    v += vnoise(fx, fy, p, seed + i * 1013) * amp;
    fx *= 2;
    fy *= 2;
    p *= 2;
    amp *= 0.5;
  }
  return v;
}

/**
 * Tileable cell noise (Dave Hoskins, shadertoy 4djGRh), which is what the
 * original's star is built from. F1 distance to the nearest jittered feature
 * point in a wrapping grid.
 *
 * A star wants cells and a planet wants fbm. Cells give the mottled, granular
 * look with hard-ish boundaries between granules; smooth noise gives a gas cloud
 * instead, which reads as a fuzzy ball rather than as a star.
 */
export function worley(x: number, y: number, numCells: number, seed: number): number {
  const px = x * numCells;
  const py = y * numCells;
  const cx = Math.floor(px);
  const cy = Math.floor(py);
  let d = Infinity;
  for (let ox = -1; ox <= 1; ox++) {
    for (let oy = -1; oy <= 1; oy++) {
      const gx = cx + ox;
      const gy = cy + oy;
      const tx = gx + hash2(gx, gy, numCells, seed);
      const ty = gy + hash2(gx, gy, numCells, seed + 7919);
      const ddx = tx - px;
      const ddy = ty - py;
      const dd = ddx * ddx + ddy * ddy;
      if (dd < d) d = dd;
    }
  }
  return Math.sqrt(d);
}

/**
 * Cellular noise, ported from the original's cloud shader (which credits
 * Leukbaars on shadertoy). Used only as a displacement: a grid of cells, each
 * with one jittered blob, accumulating into a turbulent field. Value noise alone
 * gives fog; this is what makes the difference look like weather.
 */
export function circleNoise(u: number, v: number, period: number, seed: number): number {
  const y = Math.floor(v);
  // Shear alternate rows so the cells do not line up into columns.
  const xu = u + y * 0.31;
  const fx = xu - Math.floor(xu);
  const fy = v - y;
  const h = hash2(Math.floor(xu), y, period, seed);
  const m = Math.hypot(fx - 0.25 - h * 0.5, fy - 0.25 - h * 0.5);
  // GLSL's smoothstep(edge0, edge1, x) is not the same function as the 0..1
  // clamped one below, so this ramp is spelled out rather than reusing it.
  const r = h * 0.25;
  if (r <= 0) return 1;
  return smoothstep((m * 0.75) / r);
}

/**
 * Crater field: 0 on the plain, rising to 1 at the bottom of a bowl.
 *
 * Built from the same cellular noise the cloud layer uses, taken as a product of
 * two grids and inverted. `circleNoise` is low at a cell centre, so the product
 * is low only where TWO centres nearly coincide — a sparse set, which is why this
 * reads as scattered craters rather than as a dimpled ball. One grid alone would
 * put a bowl in every cell and give a regular honeycomb of them instead.
 *
 * The second grid is offset by a non-integer, deliberately: a whole-cell offset
 * re-aligns the lattices and every bowl lands on top of another, which collapses
 * the field back to a single frequency.
 *
 * `period` must be an integer and must equal the cell count across the sphere,
 * or the field seams at the wrap.
 */
export function craterField(x: number, y: number, period: number, seed: number): number {
  const a = circleNoise(x, y, period, seed);
  const b = circleNoise(x + 11.37, y + 11.37, period, seed);
  return 1 - a * b;
}

export type RGB = [number, number, number];

export function hexToRgb(hex: string): RGB {
  const h = hex.replace("#", "");
  const v = parseInt(h.length === 3 ? h.replace(/./g, c => c + c) : h, 16);
  return [(v >> 16) & 255, (v >> 8) & 255, v & 255];
}

export function mixRgb(a: RGB, b: RGB, t: number): RGB {
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

/** Shortest-arc hue rotation, so mixing through 0/360 does not sweep the wheel. */
export function mixHsl(a: RGB, b: RGB, t: number): RGB {
  const toHsl = (c: RGB): [number, number, number] => {
    const r = c[0] / 255;
    const g = c[1] / 255;
    const bl = c[2] / 255;
    const max = Math.max(r, g, bl);
    const min = Math.min(r, g, bl);
    const l = (max + min) / 2;
    if (max === min) return [0, 0, l];
    const d = max - min;
    const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    let h: number;
    if (max === r) h = ((g - bl) / d + (g < bl ? 6 : 0)) / 6;
    else if (max === g) h = ((bl - r) / d + 2) / 6;
    else h = ((r - g) / d + 4) / 6;
    return [h, s, l];
  };
  const [h1, s1, l1] = toHsl(a);
  const [h2, s2, l2] = toHsl(b);
  let dh = h2 - h1;
  if (dh > 0.5) dh -= 1;
  if (dh < -0.5) dh += 1;
  const h = ((h1 + dh * t + 1) % 1) * 360;
  const s = s1 + (s2 - s1) * t;
  const l = l1 + (l2 - l1) * t;
  const cc = (1 - Math.abs(2 * l - 1)) * s;
  const x = cc * (1 - Math.abs(((h / 60) % 2) - 1));
  const m = l - cc / 2;
  const seg = Math.floor(h / 60) % 6;
  const rgb: RGB = [
    [cc, x, 0][seg] ?? 0,
    [x, cc, 0][(seg + 4) % 6] ?? 0,
    [0, x, cc][(seg + 2) % 6] ?? 0,
  ];
  return [(rgb[0] + m) * 255, (rgb[1] + m) * 255, (rgb[2] + m) * 255];
}

export const TAU = Math.PI * 2;

/** 4x4 ordered dither, -0.5..0.5. */
export const BAYER4 = [
  0, 8, 2, 10,
  12, 4, 14, 6,
  3, 11, 1, 9,
  15, 7, 13, 5,
].map(n => n / 16 - 0.5);

export function smoothstep(t: number): number {
  const c = Math.max(0, Math.min(1, t));
  return c * c * (3 - 2 * c);
}

export function fract(x: number): number {
  return x - Math.floor(x);
}
