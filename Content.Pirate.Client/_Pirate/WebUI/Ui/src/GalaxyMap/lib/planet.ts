/**
 * Procedural pixel planets.
 *
 * The algorithm is ported from Deep-Fold's PixelPlanets Godot shaders
 * (github.com/Deep-Fold/PixelPlanets, MIT). It is value noise + fbm sampled
 * through a sphere projection, thresholded into bands, lit by a dot product
 * against a light direction, and dithered with an ordered matrix. About a
 * hundred lines of shader, so a clean TypeScript version is less work than
 * vendoring someone else's port and inheriting their structure.
 *
 * Two things are deliberately NOT the same as the original:
 *
 * 1. The lattice hash is an integer hash, not the original
 *    `fract(sin(dot(coord, k)) * c)`. That is a fine trick inside one engine
 *    and a liability in a shipped game: ECMAScript leaves `Math.sin` precision
 *    implementation-defined, so the planets could differ between clients
 *    without anybody changing anything. An integer hash cannot drift.
 *
 * 2. The sprite is generated AT its display size. The original is an editor
 *    that renders large and lets you scale in engine; scaling pixel art down
 *    turns it to mush. Here the noise frequency is tied to the radius, because
 *    a continent has to be about three pixels across to be readable at all, and
 *    three pixels means a different frequency at 12px than at 96px.
 *
 * A planet is a pure function of its parameters, so the model only needs to
 * carry a seed and a type. No art pipeline, and two clients holding the same
 * model draw the same planets.
 */

export type PlanetType =
  | "terran"
  | "ocean"
  | "desert"
  | "ice"
  | "gas"
  | "lava"
  | "barren"
  | "asteroid";

/** How the surface is decided: a land cutoff, latitude bands, or neither. */
type BandKind = "terrain" | "lat" | "solid";

interface TypeSpec {
  kind: BandKind;
  /** Surface threshold, 0..1. Higher means less land. */
  cutoff: number;
  /** Latitude band count for gas giants. */
  bands: number;
  /** How far the band edges are pushed around by noise. */
  warp: number;
  /** Glowing cracks, for lava. */
  emissive: boolean;
  /** Rim colour, or null for an airless world. */
  atmo: string | null;
  /** Palette, dark to light. Index 0 is the deepest, index 3 the brightest. */
  pal: [string, string, string, string];
}

export const PLANET_TYPES: Record<PlanetType, TypeSpec> = {
  terran: {
    kind: "terrain",
    cutoff: 0.5,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: "#7cc0ee",
    // deep sea, land, highland, snow — the order the threshold walk expects.
    pal: ["#1d4570", "#42764a", "#78a256", "#dde1c9"],
  },
  ocean: {
    kind: "terrain",
    cutoff: 0.6,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: "#7cc0ee",
    pal: ["#1a3f6b", "#357a55", "#5f9c62", "#d2d8b6"],
  },
  desert: {
    kind: "terrain",
    // No sea at all: the cutoff sits below the noise floor, so every pixel is
    // land and the whole palette is in play.
    cutoff: 0.04,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: "#e8b878",
    pal: ["#7a4a26", "#ab7536", "#d6a95e", "#f0e0b4"],
  },
  ice: {
    kind: "terrain",
    cutoff: 0.1,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: "#bfe4ff",
    pal: ["#3f6d9c", "#6b96c6", "#aed0e9", "#f2f9ff"],
  },
  gas: {
    kind: "lat",
    cutoff: 0.5,
    bands: 7,
    warp: 0.42,
    emissive: false,
    atmo: "#f0d8a8",
    pal: ["#8a5836", "#c08c46", "#e2ba74", "#f6e8bc"],
  },
  lava: {
    kind: "terrain",
    cutoff: 0.44,
    bands: 0,
    warp: 0,
    emissive: true,
    atmo: "#ff7a2a",
    pal: ["#2a1512", "#5e2413", "#b04016", "#ff9c42"],
  },
  barren: {
    kind: "terrain",
    cutoff: 0.08,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: null,
    pal: ["#3a3a46", "#63636f", "#94949f", "#cacad4"],
  },
  asteroid: {
    kind: "solid",
    cutoff: 0.5,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: null,
    pal: ["#38332c", "#4e4740", "#6b6154", "#8b8070"],
  },
};

export const PLANET_TYPE_LIST = Object.keys(PLANET_TYPES) as PlanetType[];

/** Bump when the noise changes, so cached sprites regenerate. */
export const PLANET_ALGO_VERSION = 1;

export interface PlanetOpts {
  seed: number;
  type: PlanetType;
  /** Diameter in CSS pixels. The sprite is generated at exactly this size. */
  px: number;
  /** Light direction in radians. */
  light?: number;
  /**
   * Pull the palette toward a nation colour so the planet reads as part of the
   * map rather than a sticker on it. 0 = natural, 1 = fully the tint.
   */
  tint?: string;
  tintAmount?: number;
  /**
   * Ordered dithering. This is what makes the result read as pixel art rather
   * than a smooth ball, and it is the single variable to turn off when
   * comparing "pixel planet" against "just a shaded sphere".
   */
  dither?: boolean;
  /** Device pixel ratio, so the sprite stays sharp on a HiDPI display. */
  dpr?: number;
}

/* ------------------------------------------------------------------ noise */

/** Integer hash. See the note at the top of the file about why not sin(). */
function hash2(ix: number, iy: number, period: number, seed: number): number {
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
function vnoise(x: number, y: number, period: number, seed: number): number {
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
function fbm(x: number, y: number, period: number, octaves: number, seed: number): number {
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

/** 4x4 ordered dither, 0..1. */
const BAYER4 = [
  0, 8, 2, 10,
  12, 4, 14, 6,
  3, 11, 1, 9,
  15, 7, 13, 5,
].map(n => n / 16 - 0.5);

/* ------------------------------------------------------------------ colour */

type RGB = [number, number, number];

function hexToRgb(hex: string): RGB {
  const h = hex.replace("#", "");
  const v = parseInt(h.length === 3 ? h.replace(/./g, c => c + c) : h, 16);
  return [(v >> 16) & 255, (v >> 8) & 255, v & 255];
}

function rgbToHex(c: RGB): string {
  return (
    "#" +
    c
      .map(n => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, "0"))
      .join("")
  );
}

function mixRgb(a: RGB, b: RGB, t: number): RGB {
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

/** Shortest-arc hue rotation, so mixing through 0/360 does not sweep the wheel. */
function mixHsl(a: RGB, b: RGB, t: number): RGB {
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

/* ------------------------------------------------------------------ render */

const cache = new Map<string, string>();

/** The cache key has to include the size. */
function keyOf(o: PlanetOpts): string {
  return [
    PLANET_ALGO_VERSION,
    o.seed,
    o.type,
    Math.round(o.px),
    (o.light ?? -2.2).toFixed(3),
    o.tint ?? "-",
    (o.tintAmount ?? 0).toFixed(3),
    o.dither === false ? 0 : 1,
    o.dpr ?? 1,
  ].join("|");
}

/**
 * Ordered dither across a list of thresholds, in `h` space.
 *
 * Each threshold is a narrow soft step rather than a hard cut, and the Bayer
 * value decides which side of it a pixel lands on. Confining the mix to a thin
 * window around each threshold is the whole trick: dithering the raw band
 * coordinate instead mixes half of every band, which spreads a continent into a
 * 4px checkerboard and makes the dithered planet look blurrier than the
 * undithered one.
 *
 * `next` maps a threshold to the palette entry it introduces, so sea and land
 * can be tuned independently.
 */
function ditherBands(
  pal: RGB[],
  h: number,
  thresholds: number[],
  next: number[],
  edge: number,
  ditherV: number,
): RGB {
  let col = pal[0];
  for (let i = 0; i < thresholds.length; i++) {
    const p = smoothstep((h - thresholds[i]) / edge);
    col = p > ditherV ? pal[next[i]] : col;
  }
  return col;
}

function smoothstep(t: number): number {
  const c = Math.max(0, Math.min(1, t));
  return c * c * (3 - 2 * c);
}

/**
 * Render a planet and return it as a PNG data URI.
 *
 * The URI is what lets a procedurally drawn sprite live inside the chart's SVG,
 * which is otherwise all geometry. A cache keyed on every parameter means the
 * work happens once per (system, size) pair and never again.
 */
export function planetUri(o: PlanetOpts): string {
  const k = keyOf(o);
  const hit = cache.get(k);
  if (hit) return hit;

  const spec = PLANET_TYPES[o.type];
  const dpr = o.dpr ?? 1;
  // Round the device size to a whole pixel. A fractional canvas size makes the
  // browser resample, which is the one thing that must never happen to pixel art.
  const d = Math.max(3, Math.round(o.px * dpr));
  const half = d / 2;
  // The sprite is a square with a disc in it, so there is no room for a halo
  // outside the rim. Rather than pay for a second element per planet, the glow
  // is baked into the transparent margin — which is what makes a planet sit IN
  // space instead of on top of the chart. The canvas is oversized by this factor
  // and scaled back down on the way out.
  const GLOW = 1.3;
  const cd = Math.round(d * GLOW);
  const chalf = cd / 2;

  const seed = (o.seed | 0) ^ 0x9e37;
  const light = o.light ?? -2.2;
  // A light with no Z component would leave the whole limb unlit, so the
  // terminator would be a hard edge through the middle of the disc.
  const lz = 0.42;
  const ll = Math.hypot(Math.cos(light), Math.sin(light), lz);
  const lx = Math.cos(light) / ll;
  const ly = Math.sin(light) / ll;
  const lzz = lz / ll;

  // Noise frequency is in raw sphere-UV, so the BASE period of 2 puts the
  // largest continent at about half the disc — two or three of them, which is
  // what makes it read as a world rather than as moss. The original shader does
  // the same thing; its `size` uniform only controls tiling, not frequency.
  //
  // Octave count is then the part that scales with size, because the finest
  // octave has to land near one pixel. An octave finer than that is not detail,
  // it is per-pixel noise, and it turns every coastline into speckle: the
  // original first pass of this looked like moss for exactly that reason.
  const period = 2;
  const octaves = Math.max(2, Math.min(6, Math.floor(Math.log2((d * 2) / 3)) + 1));
  const rot = (hash2(seed, 7, 64, 13) * Math.PI * 2) as number;
  const cosR = Math.cos(rot);
  const sinR = Math.sin(rot);
  const dither = o.dither !== false;
  // A fraction of the total fbm range, so it actually crosses band boundaries.
  // The first pass used 1.15/period, which at 96px was 0.036 — far too small to
  // flip a pixel, so "dithered" and "not dithered" came out pixel-identical and
  // the comparison answered nothing.
  const ditherAmt = 0.1;

  const tint = o.tint ? hexToRgb(o.tint) : null;
  const tintAmt = o.tintAmount ?? 0;
  const pal = spec.pal.map(hex => {
    const c = hexToRgb(hex);
    return tint ? mixHsl(c, tint, tintAmt) : c;
  });
  const atmo = spec.atmo ? hexToRgb(spec.atmo) : null;
  // A small sprite cannot afford a dark side. Below about 24px the night half is
  // most of the disc, so a planet drawn at map scale stops reading as a lit body
  // and starts reading as a hole punched in the territory behind it. Lifting the
  // floor and lightening the night colour costs the dramatic terminator and buys
  // back the silhouette.
  const small = d < 28;
  // 0.42 rather than 0.34: on a pale territory fill a dark planet is a hole
  // punched in the map, and Solarian's grey is the worst case on the chart.
  const nightFloor = small ? 0.42 : 0.1;
  const night: RGB = small ? [40, 52, 80] : [8, 11, 22];

  const cv = document.createElement("canvas");
  cv.width = cd;
  cv.height = cd;
  const ctx = cv.getContext("2d");
  if (!ctx) return "";
  const img = ctx.createImageData(cd, cd);
  const px = img.data;

  for (let y = 0; y < cd; y++) {
    for (let x = 0; x < cd; x++) {
      const nx = (x + 0.5 - chalf) / chalf;
      const ny = (y + 0.5 - chalf) / chalf;
      const r2 = nx * nx + ny * ny;
      const i = (y * cd + x) * 4;
      if (r2 > GLOW * GLOW) continue;
      if (r2 > 1) {
        // Atmosphere halo, falling off to nothing at the sprite edge.
        const a = smoothstep((GLOW * GLOW - r2) / (GLOW * GLOW - 1)) * (atmo ? 0.5 : 0.22);
        px[i] = atmo ? atmo[0] : 150;
        px[i + 1] = atmo ? atmo[1] : 165;
        px[i + 2] = atmo ? atmo[2] : 200;
        px[i + 3] = Math.round(a * 255);
        continue;
      }
      const nz = Math.sqrt(1 - r2);

      // Spherify: the same projection the original uses to wrap the noise
      // around the globe. Cheaper than an equirectangular lookup and it avoids
      // the polar pinch entirely.
      const inv = 1 / (nz + 1);
      let sx = (nx * inv) * 0.5 + 0.5;
      let sy = (ny * inv) * 0.5 + 0.5;
      // Rotate about the disc centre so each seed gets its own orientation.
      const cx = sx - 0.5;
      const cy = sy - 0.5;
      sx = cx * cosR - cy * sinR + 0.5;
      sy = cx * sinR + cy * cosR + 0.5;

      const h = fbm(sx * period, sy * period, period, octaves, seed);
      // Bayer in 0..1, used to pick a side at each threshold. With dithering
      // off the side is a plain 0.5 cut — passing 1 here instead made the
      // comparison unreachable, so every undithered planet came out as a flat
      // single tone and looked like a bug rather than a control.
      const ditherV = dither ? BAYER4[(y & 3) * 4 + (x & 3)] + 0.5 : 0.5;

      let col: RGB;
      if (spec.kind === "lat") {
        // Latitude bands, pushed around by noise so they swirl. Gas giants keep
        // hard edges: banding is the whole read, and softening it makes them
        // look like every other planet.
        const lat = Math.asin(Math.max(-1, Math.min(1, ny))) / Math.PI + 0.5;
        const warp = fbm(sx * period * 0.5, sy * period * 0.5, period, 3, seed + 77) - 0.5;
        const b = lat * spec.bands + warp * spec.warp;
        const th: number[] = [];
        const nx: number[] = [];
        for (let i = 1; i < spec.bands; i++) {
          th.push(i - warp * spec.warp);
          nx.push(1 + ((i - 1) % 3));
        }
        col = ditherBands(pal, b, th, nx, 0.05, ditherV);
      } else if (spec.kind === "solid") {
        col = ditherBands(pal, h, [0.42, 0.56, 0.7], [1, 2, 3], 0.05, ditherV);
      } else {
        // Abyss, shelf, lowland, highland, with a hard snow line above the last
        // threshold so mountains only appear on genuinely high ground instead of
        // everywhere above the shoreline.
        const c = spec.cutoff;
        const span = 1 - c;
        col = ditherBands(
          pal,
          h,
          [c, c + span * 0.55, c + span * 0.9],
          [1, 2, 3],
          0.045,
          ditherV,
        );
        if (spec.emissive && h > c + span * 0.5) {
          // Lava: the cracks glow, so the night side is not the only bright part.
          col = mixRgb(col, pal[3], 0.8);
        }
      }

      // Light. Below the terminator it falls to a dark blue rather than to
      // black, which is what keeps the night side from punching a hole in the
      // map it sits on.
      const dot = nx * lx + ny * ly + nz * lzz;
      const shade = dot < 0.14 ? nightFloor + (1 - nightFloor) * smoothstep((dot + 0.5) / 0.64) : 1;
      let out = mixRgb(night, col, shade);

      // Rim light, one pixel wide, hugging the lit limb.
      if (atmo && r2 > 0.86) {
        const rim = smoothstep((r2 - 0.86) / 0.14) * Math.max(0, dot);
        out = mixRgb(out, atmo, rim * 0.55);
      }

      px[i] = out[0];
      px[i + 1] = out[1];
      px[i + 2] = out[2];
      px[i + 3] = 255;
    }
  }
  ctx.putImageData(img, 0, 0);
  // Returned scaled back down to the requested disc size, halo and all.
  const out2 = document.createElement("canvas");
  out2.width = d;
  out2.height = d;
  const octx = out2.getContext("2d");
  if (!octx) return "";
  octx.imageSmoothingEnabled = false;
  octx.drawImage(cv, 0, 0, cd, cd, 0, 0, d, d);
  const uri = out2.toDataURL("image/png");
  cache.set(k, uri);
  return uri;
}

/**
 * Map a system kind onto a planet type.
 *
 * Anything that is a built station rather than a star stays off the planet path
 * entirely; the four marker silhouettes on the chart exist to distinguish those,
 * and a small grey rock would throw the distinction away.
 */
/** Types a star system can be. `asteroid` is deliberately not one of them. */
const ORBITAL_TYPES: PlanetType[] = ["terran", "ocean", "desert", "ice", "gas", "lava", "barren"];

export function planetTypeFor(kind: string, id: string): PlanetType {
  if (kind === "station" || kind === "outpost") return "asteroid";
  // Spread across the orbital types by id. The first cut of this indexed the
  // whole list and fell off the end, so every system in the map was an asteroid
  // and the planets looked uniformly like grey rocks — which is a much less
  // interesting failure than the one it was masking.
  return ORBITAL_TYPES[Math.abs(seedFromId(id)) % ORBITAL_TYPES.length];
}

/** A different seed per system, so two systems never look identical. */
export function seedFromId(id: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < id.length; i++) {
    h ^= id.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  return h | 0;
}

export function clearPlanetCache(): void {
  cache.clear();
}
