/**
 * Procedural pixel planets.
 *
 * The surface algorithm is ported from Deep-Fold's PixelPlanets Godot shaders
 * (github.com/Deep-Fold/PixelPlanets, MIT): value noise + fbm sampled through a
 * sphere projection, thresholded into bands, lit by a dot product against a
 * light direction, and dithered with an ordered matrix. The cloud layer is
 * ported from their `Clouds.gdshader`, which warps an fbm with a cellular noise
 * so the result reads as cloud rather than as fog.
 *
 * About a hundred lines of shader each, so a clean TypeScript version is less
 * work than vendoring someone else's port and inheriting their structure.
 *
 * Four things are deliberately NOT the same as the original:
 *
 * 1. The lattice hash is an integer hash, not the original
 *    `fract(sin(dot(coord, k)) * c)`. That is a fine trick inside one engine
 *    and a liability in a shipped game: ECMAScript leaves `Math.sin` precision
 *    implementation-defined, so the planets could differ between clients
 *    without anybody changing anything. An integer hash cannot drift.
 *
 * 2. The sprite is generated AT its display size. The original is an editor
 *    that renders large and lets you scale in engine; scaling pixel art down
 *    turns it to mush. Here the octave count is tied to the radius, because a
 *    continent has to be about three pixels across to be readable at all, and
 *    three pixels means a different frequency at 12px than at 96px.
 *
 * 3. Land rotates about the planet's own polar axis, not about the view axis.
 *    The original rotates the disc UV *before* the sphere projection, which
 *    spins the globe like a coin on a table: the poles travel in circles. This
 *    shifts longitude instead, scaled by cos(latitude), so the poles stay put and
 *    the equator sweeps fastest. Without that, rotation reads as the continents
 *    sliding sideways rather than as a world turning.
 *
 * 4. A planet is a pure function of its parameters, so the model only carries a
 *    seed and a type. No art pipeline, and two clients holding the same model
 *    draw the same planets.
 *
 * Animation is frame-based, the way the original tool's spritesheet export is:
 * render N frames into one horizontal strip and step through it. Re-rendering per
 * animation frame would mean a `toDataURL` per system per frame, which is not a
 * thing you can do sixty times a second.
 */

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

/**
 * Cellular noise, ported from the original's cloud shader (which credits
 * Leukbaars on shadertoy). Used only as a displacement: a grid of cells, each
 * with one jittered blob, accumulating into a turbulent field. Value noise alone
 * gives fog; this is what makes the difference look like weather.
 */
function circleNoise(u: number, v: number, period: number, seed: number): number {
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

/* ----------------------------------------------------------------- colour */

type RGB = [number, number, number];

function hexToRgb(hex: string): RGB {
  const h = hex.replace("#", "");
  const v = parseInt(h.length === 3 ? h.replace(/./g, c => c + c) : h, 16);
  return [(v >> 16) & 255, (v >> 8) & 255, v & 255];
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

/* ------------------------------------------------------------------ types */

export type PlanetType =
  | "star"
  | "terran"
  | "ocean"
  | "desert"
  | "ice"
  | "gas"
  | "lava"
  | "barren"
  | "asteroid";

/**
 * How the surface is decided: a land cutoff, latitude bands, a lit sphere, or a
 * self-luminous disc. `star` is a different kind of thing rather than a ninth
 * planet, which is why it is not in the orbital list below.
 */
type BandKind = "terrain" | "lat" | "solid" | "star";

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
  /** Rim and corona colour, or null for an airless world. */
  atmo: string | null;
  /** Palette, dark to light. Index 0 is the deepest, index 3 the brightest. */
  pal: [string, string, string, string];
  /** Base cloud threshold. Gas and ice worlds are soupy; rock is not. */
  cloud: number;
}

export const PLANET_TYPES: Record<PlanetType, TypeSpec> = {
  star: {
    kind: "star",
    cutoff: 0,
    bands: 0,
    warp: 0,
    emissive: false,
    // Warm corona. A star is the one body here that SHOULD bleed past its own
    // edge; the planets were doing the same thing and it read as a sticker.
    atmo: "#ffc46a",
    pal: ["#7a1e05", "#d4550f", "#ffa62b", "#fff3cd"],
    cloud: 0,
  },
  terran: {
    kind: "terrain",
    cutoff: 0.5,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: "#7cc0ee",
    // deep sea, land, highland, snow — the order the threshold walk expects.
    pal: ["#1d4570", "#42764a", "#78a256", "#dde1c9"],
    cloud: 0.54,
  },
  ocean: {
    kind: "terrain",
    cutoff: 0.6,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: "#7cc0ee",
    pal: ["#1a3f6b", "#357a55", "#5f9c62", "#d2d8b6"],
    cloud: 0.5,
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
    cloud: 0.66,
  },
  ice: {
    kind: "terrain",
    cutoff: 0.1,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: "#bfe4ff",
    pal: ["#3f6d9c", "#6b96c6", "#aed0e9", "#f2f9ff"],
    cloud: 0.46,
  },
  gas: {
    kind: "lat",
    cutoff: 0.5,
    bands: 7,
    warp: 0.42,
    emissive: false,
    atmo: "#f0d8a8",
    pal: ["#8a5836", "#c08c46", "#e2ba74", "#f6e8bc"],
    // A gas giant really is a cloud deck all the way down, so the layer wants to
    // be thick — but at the threshold that makes it literally opaque the bands
    // vanish and it becomes a featureless cream ball, which throws away the one
    // silhouette that made gas giants worth having. Half cover, so the banding
    // shows through the weather.
    cloud: 0.44,
  },
  lava: {
    kind: "terrain",
    cutoff: 0.44,
    bands: 0,
    warp: 0,
    emissive: true,
    atmo: "#ff7a2a",
    pal: ["#2a1512", "#5e2413", "#b04016", "#ff9c42"],
    cloud: 0,
  },
  barren: {
    kind: "terrain",
    cutoff: 0.08,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: null,
    pal: ["#3a3a46", "#63636f", "#94949f", "#cacad4"],
    cloud: 0,
  },
  asteroid: {
    kind: "solid",
    cutoff: 0.5,
    bands: 0,
    warp: 0,
    emissive: false,
    atmo: null,
    pal: ["#38332c", "#4e4740", "#6b6154", "#8b8070"],
    cloud: 0,
  },
};

export const PLANET_TYPE_LIST = Object.keys(PLANET_TYPES) as PlanetType[];

/**
 * Types an orbiting world can be. `star` and `asteroid` are deliberately not
 * among them: a star is not one of these, and an asteroid marks a built station
 * rather than a world.
 */
const ORBITAL_TYPES: PlanetType[] = ["terran", "ocean", "desert", "ice", "gas", "lava", "barren"];

/** Bump when the noise changes, so cached sprites regenerate. */
export const PLANET_ALGO_VERSION = 3;

export interface PlanetOpts {
  seed: number;
  type: PlanetType;
  /** Diameter in CSS pixels. The sprite is generated at exactly this size. */
  px: number;
  /** Light direction in radians. */
  light?: number;
  /**
   * Land rotation, 0..1 for a full turn. Zero for a static sprite, and the
   * frames of a sheet step evenly through it.
   */
  spin?: number;
  /**
   * Cloud threshold, 0..1, overriding the type's default.
   *
   * LOWER MEANS MORE CLOUD. This is a cut-off on the noise field, not a fraction
   * of the disc, and the direction is genuinely easy to get backwards: a value of
   * 0.25 floods the planet white and 0.9 leaves it bare. The original shader has
   * the same inverted meaning, which is why it is named `cloudThreshold` here
   * rather than `cloudCover`.
   */
  cloudThreshold?: number;
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

/* ----------------------------------------------------------------- render */

/** 4x4 ordered dither, -0.5..0.5. */
const BAYER4 = [
  0, 8, 2, 10,
  12, 4, 14, 6,
  3, 11, 1, 9,
  15, 7, 13, 5,
].map(n => n / 16 - 0.5);

/**
 * Ordered dither across a list of thresholds, in `h` space.
 *
 * Each threshold is a narrow soft step rather than a hard cut, and the Bayer
 * value decides which side of it a pixel lands on. Confining the mix to a thin
 * window around each threshold is the whole trick: dithering the raw band
 * coordinate instead mixes half of every band, which spreads a continent into a
 * 4px checkerboard and makes the dithered planet look blurrier than the
 * undithered one.
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

function fract(x: number): number {
  return x - Math.floor(x);
}

/** Precomputed per-call constants, so the pixel loop stays cheap. */
interface Frame {
  d: number;
  rPx: number;
  glow: number;
  isStar: boolean;
  kind: BandKind;
  bands: number;
  warp: number;
  cutoff: number;
  emissive: boolean;
  pal: RGB[];
  atmo: RGB | null;
  night: RGB;
  nightFloor: number;
  cloud: number;
  dither: boolean;
  period: number;
  octaves: number;
  seed: number;
  rot: number;
  lx: number;
  ly: number;
  lz: number;
}

function prep(o: PlanetOpts, d: number, threshold: number | undefined): Frame {
  const spec = PLANET_TYPES[o.type];
  const isStar = spec.kind === "star";
  // A sprite is `px` on a side. A planet fills it exactly; a star fills a third
  // of it and lets a corona occupy the rest, because a star bleeding past its own
  // edge is correct and a planet doing it is not. The disc radius is derived from
  // that ratio, so the sprite never has to be resampled on the way out — and
  // resampling is the one thing that must never happen to pixel art.
  const glow = isStar ? 1.5 : 1;
  const tint = o.tint ? hexToRgb(o.tint) : null;
  const tintAmt = o.tintAmount ?? 0;

  // Noise frequency is in raw sphere-UV, so the BASE period of 2 puts the
  // largest continent at about half the disc — two or three of them, which is
  // what makes it read as a world rather than as moss. The original shader does
  // the same thing; its `size` uniform only controls tiling, not frequency.
  //
  // Octave count is then the part that scales with size, because the finest
  // octave has to land near one pixel. An octave finer than that is not detail,
  // it is per-pixel noise, and it turns every coastline into speckle.
  const small = d < 28;
  const light = o.light ?? -2.2;
  // A light with no Z component would leave the whole limb unlit, so the
  // terminator would be a hard edge through the middle of the disc.
  const lz = 0.42;
  const ll = Math.hypot(Math.cos(light), Math.sin(light), lz);

  return {
    d,
    rPx: d / 2 / glow,
    glow,
    isStar,
    kind: spec.kind,
    bands: spec.bands,
    warp: spec.warp,
    cutoff: spec.cutoff,
    emissive: spec.emissive,
    pal: spec.pal.map(h => {
      const c = hexToRgb(h);
      return tint ? mixHsl(c, tint, tintAmt) : c;
    }),
    atmo: spec.atmo ? hexToRgb(spec.atmo) : null,
    // A small sprite cannot afford a dark side. Below about 24px the night half
    // is most of the disc, so a planet drawn at map scale stops reading as a lit
    // body and starts reading as a hole punched in the territory behind it.
    // 0.42 rather than 0.34: on a pale territory fill a dark planet is a hole
    // punched in the map, and Solarian's grey is the worst case on the chart.
    nightFloor: small ? 0.42 : 0.1,
    night: small ? [40, 52, 80] : [8, 11, 22],
    cloud: isStar ? 0 : (threshold ?? spec.cloud),
    dither: o.dither !== false,
    // Base period 3 rather than 2. At 2 the largest continent covers half the
    // disc, so most of a planet is empty ocean and rotation reads as "the one
    // green patch slid off" rather than as a world turning. Three gives enough
    // land for the eye to track, and it is also the first thing that makes the
    // slow rotation legible at map size.
    period: 3,
    octaves: Math.max(2, Math.min(6, Math.floor(Math.log2((d * 2) / 3)) + 1)),
    seed: (o.seed | 0) ^ 0x9e37,
    rot: hash2(o.seed | 0, 7, 64, 13) * Math.PI * 2,
    lx: Math.cos(light) / ll,
    ly: Math.sin(light) / ll,
    lz: lz / ll,
  };
}

/**
 * Render one frame of a sprite into `out`, with its top-left at (ox, oy).
 *
 * Split out from the sheet assembly because the animation path needs the exact
 * same pixel code as the static path. If the two ever diverge, a rotating planet
 * stops matching the still of itself, which is the kind of thing nobody notices
 * until it is in front of a player.
 */
function renderFrame(f: Frame, out: ImageData, stride: number, ox: number, oy: number, spin: number) {
  const { d, rPx, glow, pal, atmo, night, nightFloor, period, octaves, seed, kind } = f;
  const px = out.data;
  const FLARES = 4;
  const TAU = Math.PI * 2;
  // How far the cloud deck runs ahead of the ground at the middle of a turn.
  // Kept small: the deck is sampled in the same sphere space, so a large shear
  // slides cloud over places that were night-side a moment ago.
  const CLOUD_SHEAR = 0.09;

  for (let y = 0; y < d; y++) {
    for (let x = 0; x < d; x++) {
      const i = ((y + oy) * stride + (x + ox)) * 4;
      const dx = (x + 0.5 - d / 2) / rPx;
      const dy = (y + 0.5 - d / 2) / rPx;
      const r2 = dx * dx + dy * dy;
      if (r2 > glow * glow) continue;
      if (r2 > 1) {
        // Corona. Star only — for a planet this branch is unreachable, because a
        // planet's glow is 1.
        const g = 1 - (r2 - 1) / (glow * glow - 1);
        px[i] = atmo ? atmo[0] : 255;
        px[i + 1] = atmo ? atmo[1] : 210;
        px[i + 2] = atmo ? atmo[2] : 140;
        px[i + 3] = Math.round(g * g * 0.8 * 255);
        continue;
      }
      const nx = dx;
      const ny = dy;
      const nz = Math.sqrt(1 - r2);

      // Spherify: the same projection the original uses to wrap the noise around
      // the globe. Cheaper than an equirectangular lookup, and no polar pinch.
      const inv = 1 / (nz + 1);
      const sx0 = (nx * inv) * 0.5 + 0.5;
      const sy = (ny * inv) * 0.5 + 0.5;

      // Rotation: a shift along the sphere's u axis.
      //
      // The obvious refinement is to scale that shift by cos(latitude) so the
      // poles hold still and the equator sweeps fastest. It is wrong, and
      // expensively wrong: after a full turn a mid-latitude pixel has advanced
      // by 0.6 of a texture period, not a whole one, so the animation never loops
      // and frame N does not match frame 0. A constant shift is the only one that
      // closes for every latitude at once, and the apparent speed difference falls
      // out of the sphere projection anyway — u is compressed toward the limb, so
      // the same shift covers more screen there. `checkLoopCloses` in
      // tools/check-galaxy.ts asserts the property, because it is invisible until
      // the seam is on screen for a minute.
      const sx = fract(sx0 + spin + f.rot);

      // Bayer in 0..1, used to pick a side at each threshold. With dithering off
      // the side is a plain 0.5 cut.
      const ditherV = f.dither ? BAYER4[(y & 3) * 4 + (x & 3)] + 0.5 : 0.5;

      // Light. Below the terminator it falls to a dark blue rather than to black,
      // which is what keeps the night side from punching a hole in the map.
      const dot = nx * f.lx + ny * f.ly + nz * f.lz;
      const shade =
        dot < 0.14 ? nightFloor + (1 - nightFloor) * smoothstep((dot + 0.5) / 0.64) : 1;

      let col: RGB;
      let outRgb: RGB;
      if (f.isStar) {
        // Self-luminous, so there is no terminator at all: brightness falls off
        // from the centre outward. Running a star through the planet lighting
        // model draws a planet, which is exactly the mistake of shading Sol like
        // a world with a bright side.
        const t = 1 - r2;
        if (t > 0.7) col = mixRgb(pal[3], [255, 255, 255], (t - 0.7) / 0.3);
        else if (t > 0.4) col = mixRgb(pal[2], pal[3], (t - 0.4) / 0.3);
        else if (t > 0.14) col = mixRgb(pal[1], pal[2], (t - 0.14) / 0.26);
        else col = mixRgb(pal[0], pal[1], t / 0.14);
        const gran = fbm(sx * period * 2, sy * period * 2, period, octaves, seed + 991) - 0.5;
        col = mixRgb(col, pal[3], Math.max(0, gran) * 0.45);
        // Flares: narrow spikes at seeded angles, and the thing that makes a disc
        // read as a star rather than as a glowing coin.
        const ang = Math.atan2(ny, nx);
        let fl = 0;
        for (let k = 0; k < FLARES; k++) {
          const a0 = hash2(seed, k, 64, 31) * TAU;
          const da = Math.abs(((ang - a0 + Math.PI * 3) % TAU) - Math.PI);
          fl = Math.max(fl, Math.pow(Math.max(0, Math.cos(da)), 40) * (0.35 + hash2(seed, k, 64, 77) * 0.5));
        }
        outRgb = mixRgb(col, [255, 255, 255], fl * 0.75);
      } else {
        const h = fbm(sx * period, sy * period, period, octaves, seed);
        if (kind === "lat") {
          // Latitude bands, pushed around by noise so they swirl. Gas giants keep
          // hard edges: banding is the whole read.
          const lat = Math.asin(Math.max(-1, Math.min(1, ny))) / Math.PI + 0.5;
          const warp = fbm(sx * period * 0.5, sy * period * 0.5, period, 3, seed + 77) - 0.5;
          const b = lat * f.bands + warp * f.warp;
          const th: number[] = [];
          const nxs: number[] = [];
          for (let k = 1; k < f.bands; k++) {
            th.push(k - warp * f.warp);
            nxs.push(1 + ((k - 1) % 3));
          }
          col = ditherBands(pal, b, th, nxs, 0.05, ditherV);
        } else if (kind === "solid") {
          col = ditherBands(pal, h, [0.42, 0.56, 0.7], [1, 2, 3], 0.05, ditherV);
        } else {
          // Abyss, shelf, lowland, highland, with a hard snow line above the last
          // threshold so mountains only appear on genuinely high ground.
          const c = f.cutoff;
          const span = 1 - c;
          col = ditherBands(pal, h, [c, c + span * 0.55, c + span * 0.9], [1, 2, 3], 0.045, ditherV);
          if (f.emissive && h > c + span * 0.5) {
            col = mixRgb(col, pal[3], 0.8);
          }
        }
        outRgb = mixRgb(night, col, shade);

        // Rim light, hugging the lit limb. On the disc, not outside it.
        if (atmo && r2 > 0.86) {
          const rim = smoothstep((r2 - 0.86) / 0.14) * Math.max(0, dot);
          outRgb = mixRgb(outRgb, atmo, rim * 0.55);
        }
      }

      /* ---------------------------- clouds ---------------------------- */
      if (f.cloud > 0) {
        // The deck is in the atmosphere, so it turns with the planet but not at
        // the same rate — the offset is the wind.
        //
        // The shear is a SINE of the phase rather than a different rate, and that
        // is the whole trick. Scaling the rate instead (spin * 0.82) makes the
        // clouds a non-integer fraction of a turn, so the animation never quite
        // returns to its starting frame: the land closes perfectly and the clouds
        // do not, which shows up as a small jump once per loop. A sine of the
        // phase is periodic with the loop by construction — clouds run ahead
        // through the middle of the turn and fall back by the end — so the seam
        // is exactly zero. `tools/check-dom.mjs` asserts it, and it caught this.
        const drift = spin + CLOUD_SHEAR * Math.sin(spin * TAU);
        const cu = fract(sx0 + drift);
        // A gentle latitude tilt, so the bands are not dead horizontal.
        const cvv = sy * 1.4 + smoothstep(Math.abs(sx0 - 0.4) / 1.3) * 0.25;
        // Cellular noise at a real cell count. The original multiplies by
        // `size * 0.3` where size is ~50 on a 100px sprite, i.e. ~15 cells
        // across; scaling that by the surface period instead put less than ONE
        // cell on the whole globe, which turns the deck into a single spiral
        // rather than into weather.
        const CLD = 5;
        let warpN = 0;
        for (let i = 0; i < 6; i++) {
          warpN += circleNoise(cu * CLD + i * 1.7 + 11, cvv * CLD * 0.6 + i * 2.3 + 11, CLD, seed + 5);
        }
        warpN /= 6;
        // The fbm coordinate is DISPLACED by the cellular field. That is the
        // whole trick: value noise alone warps into fog, and displacing it by
        // something blobby is what gives cloud its edges.
        const c = fbm(
          cu * period + warpN * 0.9,
          cvv * period + warpN * 0.9,
          period,
          Math.max(2, octaves - 1),
          seed + 313,
        );
        // `ditherV` is already 0..1 here. An earlier version compared against
        // `ditherV - 0.5`, which inverts the test: below the cover threshold
        // painted and above it did not, so a light cover came out solid white
        // and a heavy one came out nearly clear.
        const a = smoothstep((c - f.cloud) / 0.05) > ditherV ? 1 : 0;
        if (a > 0) {
          const bright = Math.min(1, Math.max(0, (c - f.cloud) / 0.18));
          const cc = mixRgb([206, 216, 228], [255, 255, 255], bright);
          // Clouds take the same terminator as the ground, or they stay lit on
          // the night side and the planet looks like it has a bright rim all round.
          outRgb = mixRgb(outRgb, mixRgb(night, cc, shade), 0.9);
        }
      }

      px[i] = outRgb[0];
      px[i + 1] = outRgb[1];
      px[i + 2] = outRgb[2];
      px[i + 3] = 255;
    }
  }
}

/* ------------------------------------------------------------------ cache */

const cache = new Map<string, string>();

function keyOf(o: PlanetOpts, spin: number, frames: number): string {
  return [
    PLANET_ALGO_VERSION,
    frames,
    o.seed,
    o.type,
    Math.round(o.px),
    (o.light ?? -2.2).toFixed(3),
    spin.toFixed(4),
    o.cloudThreshold ?? "-",
    o.tint ?? "-",
    (o.tintAmount ?? 0).toFixed(3),
    o.dither === false ? 0 : 1,
    o.dpr ?? 1,
  ].join("|");
}

function renderToDataUri(o: PlanetOpts, frames: number): string {
  const dpr = o.dpr ?? 1;
  // Round the device size to a whole pixel. A fractional canvas size makes the
  // browser resample, which is the one thing that must never happen to pixel art.
  const d = Math.max(3, Math.round(o.px * dpr));
  const cv = document.createElement("canvas");
  cv.width = d * frames;
  cv.height = d;
  const ctx = cv.getContext("2d");
  if (!ctx) return "";
  const img = ctx.createImageData(d * frames, d);
  const f = prep(o, d, o.cloudThreshold);
  for (let n = 0; n < frames; n++) {
    // Land turns once across the strip. The clouds turn with it, a little slower.
    renderFrame(f, img, d * frames, n * d, 0, frames === 1 ? (o.spin ?? 0) : n / frames);
  }
  ctx.putImageData(img, 0, 0);
  return cv.toDataURL("image/png");
}

/** A single still, as a PNG data URI. This is what the chart draws. */
export function planetUri(o: PlanetOpts): string {
  const k = keyOf(o, o.spin ?? 0, 1);
  const hit = cache.get(k);
  if (hit) return hit;
  const uri = renderToDataUri(o, 1);
  cache.set(k, uri);
  return uri;
}

export interface PlanetSheet {
  uri: string;
  frames: number;
  /** Side of one frame, in device pixels. */
  framePx: number;
}

/**
 * A horizontal strip of `frames` frames covering one full rotation.
 *
 * The original tool exports spritesheets for exactly this, and it is the only
 * affordable way to animate: the alternative is a `toDataURL` per system per
 * animation frame, which at eighteen systems is not something you can do at
 * sixty frames a second. Step through the strip with `steps()` and it costs
 * nothing at runtime.
 */
export function planetSheet(o: PlanetOpts, frames: number): PlanetSheet {
  const n = Math.max(1, Math.floor(frames));
  const k = keyOf(o, 1 / n, n);
  let uri = cache.get(k);
  if (uri === undefined) {
    uri = renderToDataUri(o, n);
    cache.set(k, uri);
  }
  return { uri, frames: n, framePx: Math.max(3, Math.round(o.px * (o.dpr ?? 1))) };
}

/**
 * Map a system kind onto a planet type.
 *
 * A star is a star. The first cut hashed every system across the orbital list
 * and so gave Sol a green terran world, which is not a subtle mistake: it is the
 * mistake of drawing the capital of a Solarian system as a planet.
 */
export function planetTypeFor(kind: string, id: string): PlanetType {
  if (kind === "star") return "star";
  if (kind === "station" || kind === "outpost") return "asteroid";
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
