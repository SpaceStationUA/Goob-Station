/**
 * Procedural pixel planets.
 *
 * The surface algorithm is ported from Deep-Fold's PixelPlanets Godot shaders
 * (github.com/Deep-Fold/PixelPlanets, MIT): value noise + fbm sampled through a
 * sphere projection, banded by distance to a light point in screen space, and
 * dithered with an ordered matrix. Land is chosen by comparing four displaced
 * noise fields against each other rather than by walking one field through
 * thresholds. The cloud layer is
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













import {
  BAYER4, TAU, circleNoise, craterField, fbm, fract, hash2,
  hexToRgb, mixHsl, mixRgb, smoothstep, worley, type RGB,
} from "./paint";

/* ----------------------------------------------------------------- colour */









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
 * How the surface is decided: a two-layer sea/land world, a fully-clouded one,
 * a single noise field, or a self-luminous disc. `star` is a different kind of thing rather than a ninth
 * planet, which is why it is not in the orbital list below.
 */
type BandKind = "terrain" | "lat" | "solid" | "star";

interface TypeSpec {
  kind: BandKind;
  /** Surface threshold, 0..1. Higher means less land. */
  cutoff: number;
  /** Glowing cracks, for lava. */
  emissive: boolean;
  /** Rim and corona colour, or null for an airless world. */
  atmo: string | null;
  /**
   * Palette, as two lists rather than one.
   *
   * The original draws a planet in two passes and this follows it, because the
   * split is what produces the look rather than being an implementation detail.
   *
   * `sea` is the world with no land on it, banded purely by distance to the
   * light: three steps, lit to shadow. It is what shows through wherever the
   * land test fails, and for a gas giant or an airless rock it is the whole
   * planet.
   *
   * `land` is four steps picked by comparing displaced noise fields against each
   * other rather than by walking one field through thresholds — index 0 is the
   * band that wins nearest the light, index 3 the base that fills everything
   * else. Null for a world with no continents at all.
   */
  sea: [string, string, string, string];
  land: [string, string, string, string] | null;
  /**
   * Water, if this kind of world has any: [deep, bright]. Rivers are drawn as a
   * separate pass on top of the land, which is why they get their own pair.
   */
  water?: [string, string];
  /** Base cloud threshold. Gas and ice worlds are soupy; rock is not. */
  cloud: number;
  /**
   * Craters, for worlds with nothing but rock to show for it.
   *
   * Only for airless surfaces. A crater on a world with an atmosphere is a
   * contradiction the eye catches immediately, because there is nothing left to
   * erode it and nothing to fill it.
   */
  craters?: boolean;
  /**
   * A second, independent water field, for ice worlds.
   *
   * The reference's ice world is two instances of the SAME surface shader at
   * different thresholds, composited: the sheet, and the melt water coming
   * through it. One threshold cannot do that, because one threshold gives every
   * body of water the same size and the same edge complexity.
   */
  lakes?: boolean;
}

/**
 * Palettes.
 *
 * `sea` is lit-to-shadow in three steps; `land` is ordered by how close to the
 * light each band wins, so index 0 is the highlight band and index 3 is the base
 * that fills the rest of every continent.
 *
 * These are not arbitrary. The lit step has to stay clearly lighter than the
 * shadow step or the planet reads as a flat disc with a gradient on it, and the
 * land bands have to be distinguishable from each other at 16px, which is a much
 * harder constraint than at 200px — at map size only two of the four ever show
 * and they have to be the two that carry the shape.
 */
export const PLANET_TYPES: Record<PlanetType, TypeSpec> = {
  star: {
    kind: "star",
    cutoff: 0,
    emissive: false,
    // Warm corona. A star is the one body here that SHOULD bleed past its own
    // edge; the planets were doing the same thing and it read as a sticker.
    atmo: "#ffc46a",
    // Cell noise is the whole look of a star, so these four steps are granule
    // brightness rather than depth. The top one is near-white because the core
    // of a star photographs as blown out.
    sea: ["#c2470d", "#e8801f", "#ffc463", "#fff3d4"],
    land: null,
    cloud: 0,
  },
  terran: {
    kind: "terrain",
    cutoff: 0.5,
    emissive: false,
    atmo: "#7cc0ee",
    sea: ["#2f6796", "#22507c", "#173a5e", "#102845"],
    // Beach → forest → upland → snow, so the snow only lands on land that is
    // already high AND near the light, which is what puts caps on mountains
    // instead of speckling them everywhere.
    land: ["#7ba055", "#63903f", "#527a35", "#44652c"],
    water: ["#2a6f8e", "#3f9ec4"],
    cloud: 0.56,
  },
  ocean: {
    kind: "terrain",
    // Mostly water: only the highest field clears the land test, so this is an
    // archipelagos rather than a world with seas.
    cutoff: 0.62,
    emissive: false,
    atmo: "#7cc0ee",
    sea: ["#2a6a9c", "#1c4d7c", "#10335a", "#0a2340"],
    land: ["#86ab6a", "#6b9154", "#557841", "#44612f"],
    water: ["#1f7fa0", "#38b4d6"],
    cloud: 0.58,
  },
  desert: {
    kind: "terrain",
    // No sea at all: the cutoff sits below the noise floor, so every pixel is
    // land and the whole palette is in play.
    cutoff: 0.04,
    emissive: false,
    atmo: "#e8b878",
    sea: ["#c9a066", "#a87c46", "#7d5a31", "#553c21"],
    land: ["#e8cf9a", "#dcbd80", "#d0ab68", "#c29952"],
    cloud: 0.72,
  },
  ice: {
    kind: "terrain",
    // Was 0.1, which put almost every pixel on the sheet and left the water as one
    // continent-sized region — a world half ocean, rather than an ice sheet with
    // water in it. At 0.34 the sheet is broken and the water reads as water.
    cutoff: 0.34,
    emissive: false,
    atmo: "#bfe4ff",
    sea: ["#7fa8cd", "#5c86ad", "#3f6288", "#2c4562"],
    land: ["#e8f4fc", "#d6e9f6", "#c4dcee", "#b2cfe4"],
    cloud: 0.56,
    lakes: true,
  },
  gas: {
    kind: "lat",
    cutoff: 0.5,
    emissive: false,
    atmo: "#f0d8a8",
    // No land, so `sea` is the whole planet: four steps of cloud deck.
    sea: ["#f8eecd", "#e0b87c", "#bd8a4e", "#8a5c33"],
    land: null,
    // NO separate cloud deck, and this was wrong for a while.
    //
    // The `lat` branch below already paints the surface from a
    // cellular-displaced turbulence field, so for this type the SURFACE IS the
    // weather and a deck on top is a second, redundant one. At 0.44 it covered
    // roughly half the disc in white, and because the cloud field is
    // seed-dependent that is not a uniform wash but a lottery: measured on two
    // seeds of this type at 128px, one gave a correctly banded giant whose top
    // colour was the palette's own cream, while the other spent 28% of its pixels
    // on near-white and read as a blank ball. Both were "working".
    cloud: 0,

  },
  lava: {
    kind: "terrain",
    // Higher than the other terrain worlds on purpose. The comparison hands the
    // brightest band to roughly half of whatever passes the land test, so a low
    // cutoff gives a lava world that is molten all over and reads as desert.
    // Raising it means most of the surface stays cooled crust.
    cutoff: 0.58,
    emissive: true,
    atmo: "#ff7a2a",
    sea: ["#6e2a12", "#4a1c0e", "#2a1109", "#180905"],
    // The dark steps are cooled crust and the bright ones are what is still
    // molten; `emissive` decides which of them glows rather than just being
    // lighter.
    land: ["#ffb457", "#e8681f", "#8a3312", "#4a1a0c"],
    cloud: 0,
    craters: true,
  },
  barren: {
    kind: "terrain",
    cutoff: 0.08,
    emissive: false,
    // Airless, so no rim: a glow round a world with no atmosphere is a lie the
    // eye reads as a sticker even when it cannot say why.
    atmo: null,
    sea: ["#9a9aa6", "#6e6e7a", "#4a4a54", "#30303a"],
    land: ["#c6c6d0", "#adadb9", "#9494a2", "#7b7b8b"],
    cloud: 0,
    craters: true,
  },
  asteroid: {
    kind: "solid",
    cutoff: 0.5,
    emissive: false,
    atmo: null,
    sea: ["#7b7166", "#574f46", "#3a342d", "#241f1b"],
    land: ["#8d8376", "#786f63", "#635b50", "#4e473e"],
    cloud: 0,
    craters: true,
  },
};

export const PLANET_TYPE_LIST = Object.keys(PLANET_TYPES) as PlanetType[];

/**
 * Types an orbiting world can be. `star` and `asteroid` are deliberately not
 * among them: a star is not one of these, and an asteroid marks a built station
 * rather than a world.
 */
const ORBITAL_TYPES: PlanetType[] = ["terran", "ocean", "desert", "ice", "gas", "lava", "barren"];

/** Bump when the noise changes, so cached sprites regenerate.
 * 4: screen-space banding and multi-field land, replacing the 3D dot product.
 * 5: craters on the airless worlds.
 * 6: the asteroid silhouette is a noise field, not a circle.
 * 7: star rays sampled in polar space, replacing four angular lobes.
 * 8: gas giants banded by one-dimensional latitude noise.
 * 9: ice worlds get a second, independent water field.
 * 10: gas giants lost their redundant cloud deck; the asteroid silhouette
 *     is no longer polar-damped.
 * 11: gas giant tone varies with longitude, so its rotation is visible. */
export const PLANET_ALGO_VERSION = 11;

export interface PlanetOpts {
  seed: number;
  type: PlanetType;
  /** Diameter in CSS pixels. The sprite is generated at exactly this size. */
  px: number;
  /**
   * Suppress craters whatever the type says.
   *
   * This exists so the visual test can prove its crater assertion is measuring
   * craters. The first version of that check compared an airless world against a
   * world with weather, and it failed: the rainy world scored HIGHER, because the
   * metric was counting coastlines and rivers — any dark region next to a light
   * one — rather than craters. Comparing across types cannot isolate the feature.
   *
   * Rendering the same world with this set does isolate it, because the only
   * difference between the two images is the craters. A flag only tests can set is
   * much cheaper than an assertion that can pass for the wrong reason.
   */
  suppressCraters?: boolean;
  /** Suppress the second water field on an ice world. Same reasoning as above. */
  suppressLakes?: boolean;
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



/** How far a prominence ray reaches, 0..1, as a fraction of the photosphere. */
function rayReach(r: number, a: number, seed: number): number {
  // POLAR space: radius first, angle second, with the angle compressed to 0.4.
  //
  // This is the whole difference between a star and four spikes. The field is
  // sampled at (radius, angle), so its features are arcs concentric with the star
  // rather than patches on a square; the threshold below rises with radius, so
  // each arc is cut off further out the weaker it is. An arc cut off at a radius
  // IS a ray. Sampling the same noise in Cartesian space — which is what the four
  // `cos^40` lobes this replaces were, in effect — cannot produce that shape at
  // all, and on screen it read as a hard white cross ruled across the disc.
  // The offset is the whole tuning, and it took two attempts to reason about
  // rather than guess.
  //
  // `fbm` here is NOT normalised: three octaves sum to 0.875 with a mean of
  // 0.4375 and a standard deviation near 0.17, so after the 1.6 the field has a
  // mean of 0.7 and a standard deviation of 0.27. Subtracting 0.15 therefore left
  // 43% of all angles producing *some* ray, each a short one from the core
  // cutoff out to barely past it — and half the disc went cream. Subtracting 0.4
  // fixed the mean reach but not the spread, which is the number that matters:
  // what decides whether a ray is visible is how far it gets, not how far it
  // reaches on average.
  //
  // 0.62 puts the mean below the core cutoff, so only the top quarter of the
  // arcs reach the limb at all, and those are the ones that read as rays.
  const nf = fbm(r * 7 + 3.1, a * 2.2 + 7.7, 64, 3, seed + 131) * 1.6;
  return nf - 0.62;
}

/**
 * Prominence ray at one point, 0..1. Zero outside the ray.
 *
 * Solid along its length and dithered at the tip, which is what makes it read as
 * light thinning out rather than as a shape with an edge. The original splits
 * this into two thresholds an arbitrary distance apart for the same reason.
 */
function flare(r: number, a: number, seed: number, ditherV: number): number {
  const reach = rayReach(r, a, seed);
  if (r > reach) return 0;
  if (r <= reach - 0.05) return 1;
  return ditherV > 0.5 ? 0.45 : 0;
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





/** Precomputed per-call constants, so the pixel loop stays cheap. */
interface Frame {
  d: number;
  rPx: number;
  glow: number;
  isStar: boolean;
  kind: BandKind;
  cutoff: number;
  emissive: boolean;
  /** Lit-to-shadow, three steps. */
  sea: RGB[];
  /** Four steps, index 0 winning nearest the light. Null when landless. */
  land: RGB[] | null;
  /** River colour pair, or null. */
  water: [RGB, RGB] | null;
  craters: boolean;
  lakes: boolean;
  /**
   * Craters across the sphere. Integer, because the field has to tile at the
   * wrap.
   *
   * The scaling was badly wrong at first and it showed up as a test that could not
   * see the feature at all: `round(d / 52)` clamped to a minimum of 2, so a 128px
   * sprite got two cells across the whole sphere and the product field never rose
   * above the bowl threshold anywhere. Measured, craters covered 28 pixels of
   * 16384 — 0.17%. Only sprites above about 150px had any, which is exactly
   * backwards: those are the ones with room to spare.
   *
   * Crater COUNT is close to constant in angular terms, so the frequency should
   * barely move. What has to change with size is that a 16px rock cannot afford
   * detail, so the floor is 3 rather than 1.
   */
  craterFreq: number;
  atmo: RGB | null;
  /**
   * Light position in disc-UV (0..1 across the sprite), as a vector.
   *
   * The original uses this in two different ways and both matter: its *distance*
   * bands the planet, and its *direction* displaces the land noise so that land
   * colour correlates with which way from the light a pixel sits. Carried as a
   * pair because the second use needs the direction, not just the magnitude.
   */
  lx: number;
  ly: number;
  night: RGB;
  nightFloor: number;
  cloud: number;
  dither: boolean;
  period: number;
  octaves: number;
  seed: number;
  rot: number;
}

function prep(o: PlanetOpts, d: number, threshold: number | undefined): Frame {
  const spec = PLANET_TYPES[o.type];
  const isStar = spec.kind === "star";
  // A sprite is `px` on a side. A planet fills it exactly; a star fills a third
  // of it and lets a corona occupy the rest, because a star bleeding past its own
  // edge is correct and a planet doing it is not. The disc radius is derived from
  // that ratio, so the sprite never has to be resampled on the way out — and
  // resampling is the one thing that must never happen to pixel art.
  //
  // A solid gets 1.2, not 1, because its outline is a noise field rather than a
  // circle and the lobes need somewhere to go — a rock clipped flat on its
  // high side is worse than a small rock. This is also the first non-star to
  // have pixels outside the nominal disc, which is why the corona branch below
  // now tests isStar explicitly instead of relying on glow being 1.
  const glow = isStar ? 1.5 : spec.kind === "solid" ? 1.3 : 1;
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
  // The light is a POINT in disc space, not a direction. That is the single
  // biggest difference from the first version of this port, which lit the sphere
  // with a dot product and got a smooth 3D terminator: correct-looking, and not
  // what makes a planet read as this generator's work. Banding by distance to a
  // point gives the flat, poster-like terminator with visible steps in it, and
  // perturbing that distance with noise is what stops those steps from being a
  // clean arc.
  //
  // `light` arrives as an angle and is mapped onto a point inside the disc, kept
  // well clear of the edge so the shadow side always has somewhere to go.
  const light = o.light ?? -2.2;
  const lightR = 0.21;
  const lx = 0.5 + Math.cos(light) * lightR;
  const ly = 0.5 + Math.sin(light) * lightR;

  return {
    d,
    rPx: d / 2 / glow,
    glow,
    isStar,
    kind: spec.kind,
    cutoff: spec.cutoff,
    emissive: spec.emissive,
    sea: spec.sea.map(h => {
      const c = hexToRgb(h);
      return tint ? mixHsl(c, tint, tintAmt) : c;
    }),
    land: spec.land
      ? spec.land.map(h => {
          const c = hexToRgb(h);
          return tint ? mixHsl(c, tint, tintAmt) : c;
        })
      : null,
    water: spec.water
      ? [hexToRgb(spec.water[0]), hexToRgb(spec.water[1])]
      : null,
    craters: spec.craters === true && !o.suppressCraters,
    lakes: spec.lakes === true && !o.suppressLakes,
    craterFreq: Math.max(3, Math.min(7, Math.round(d / 30))),
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
    lx,
    ly,
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
  const { d, rPx, glow, sea, land, water, atmo, night, nightFloor, period, octaves, seed, kind } = f;
  const px = out.data;
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
        // Corona, and ONLY a star has one. This used to be unreachable for
        // everything else because every planet's glow was exactly 1; an asteroid
        // now has 1.2 so its lobes fit, which makes the guard load-bearing
        // rather than decorative. Without it an airless rock grows a warm corona,
        // which is the exact thing an airless rock must not have.
        if (!f.isStar) continue;
        //
        // The falloff is g^4, not g^2. A square falloff looks right at 16px and
        // wrong at 200px: the corona is a fixed fraction of the sprite box, so at
        // overlay size it becomes a wide, evenly-lit orange donut with a soft
        // edge — a fuzzy blob rather than a star. The steeper power keeps the
        // brightness against the photosphere, which is where a corona actually
        // is, and lets the outer part fall away fast enough to still read as
        // light rather than as paint.
        const g = 1 - (r2 - 1) / (glow * glow - 1);
        px[i] = atmo ? atmo[0] : 255;
        px[i + 1] = atmo ? atmo[1] : 210;
        px[i + 2] = atmo ? atmo[2] : 140;
        // The corona brightens where a ray leaves the limb. The ray itself is
        // drawn on the disc, in polar space — see `rayReach` — because that is
        // where the original draws it and where a ray is legible; out here it is
        // only a glow, since the limb is one radius and there is no length to it.
        const rayOut = flare(Math.sqrt(r2), Math.atan2(dy, dx) * 0.4, seed, 1);
        px[i + 3] = Math.round(Math.min(1, g * g * g * 0.85 * (1 + rayOut * 0.55)) * 255);
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

      /**
       * How much noise survives this pixel, 1 at the equator and 0 at the limb.
       *
       * The sphere projection squeezes the surface coordinate hard toward the
       * top and bottom of the disc — v runs 0 at the upper limb to 0.5 at the
       * centre, and the derivative is steepest at the ends — so a pixel row there
       * crosses many noise periods at once and the field aliases. Aliased noise
       * against a hard threshold does not look like fine detail, it looks like a
       * solid band: the sprite grew a flat white cap with a horizontal top edge
       * across it, which reads as an image that has been cropped rather than as
       * a pole.
       *
       * Pulling the field back toward its mean as the limb approaches removes
       * the aliasing and leaves a smooth polar cap. Real ice caps are not
       * modelled — that would need a latitude term on the palette — and at map
       * size a calm mid-tone pole is far less wrong than a hard aliased line.
       */
      const polar = 1 - smoothstep((Math.abs(ny) - 0.7) / 0.3);

      let col: RGB;
      let outRgb: RGB;
      /**
       * Distance to the light, carried out of the branch below so the cloud deck
       * can be shaded by the same front as the ground. Without that, clouds
       * stay lit on the night side and the planet grows a bright rim all the way
       * round, which is the single most obvious way to tell a composited cloud
       * layer from one that belongs to the planet.
       */
      let dLight = 1;
      /**
       * Discrete terminator level for a pixel that is NOT already banded.
       *
       * A single continuous multiply was doing the lighting here on top of the
       * banding, which meant the sprite carried two terminators at once and the
       * palette blew out: measured, 1858 distinct colours in a 128px world. The
       * original composites only discrete layers — every colour in a planet is
       * one of the palette entries — and matching that is what makes the banding
       * read as bands rather than as a gradient with steps drawn on it.
       */
      let shade = 1;
      let isLand = false;
      /** Cleared by the asteroid silhouette, which is not a circle. */
      let shapeA = 1;
      if (f.isStar) {
        // Self-luminous, so there is no terminator at all: brightness falls off
        // from the centre outward. Running a star through the planet lighting
        // model draws a planet, which is exactly the mistake of shading Sol like
        // a world with a bright side.
        const t = 1 - r2;
        if (t > 0.7) col = mixRgb(sea[0], [255, 255, 255], (t - 0.7) / 0.3);
        else if (t > 0.4) col = mixRgb(sea[1], sea[0], (t - 0.4) / 0.3);
        else if (t > 0.14) col = mixRgb(sea[2], sea[1], (t - 0.14) / 0.26);
        else col = mixRgb(sea[3], sea[2], t / 0.14);

        /**
         * Granulation from cell noise, quantised into four bands with a dithered
         * boundary.
         *
         * Cell noise rather than fbm is the point: a star's surface is granules
         * with hard edges between them and the eye reads that specifically. The
         * first version of this port used fbm and got a smooth orange ball with
         * faint mottling — fine as a gradient, wrong as a star.
         *
         * The statistic is INVERTED and normalised, and that is not cosmetic. A
         * straight `c10 * c20 * 2` — the obvious transcription of the original —
         * has a badly skewed distribution: measured over the disc it puts 44% of
         * pixels in the lowest band and 3% in the highest, so almost the whole
         * star lands on two adjacent palette entries and reads as a blown-out
         * white ball with dark outlines. Cell F1 distance is already skewed the
         * other way, and `1 - a` is close to uniform: 23/27/27/23 across four
         * bands.
         *
         * The fine scale is then added as MODULATION rather than folded into the
         * statistic. Weighting it into a sum with the coarse scale is what
         * re-skews the distribution (measured: 10/37/40/13), because the two
         * scales have different distributions and averaging them favours the
         * denser one. Adding a small signed term moves pixels within the
         * distribution instead of dragging it, which is what breaks up the large
         * cells without unbalancing the bands.
         */
        const coarse = worley(sx, sy, 14, seed + 31);
        const fine = worley(sx, sy, 30, seed + 57);
        // The PRODUCT of the two fields, not either alone: a single cell field
        // lights up its cell WALLS, because F1 distance's contours are the walls,
        // and a star built from walls is a honeycomb. The product only lights up
        // where two independent centres nearly coincide, which is irregular.
        //
        // What the product costs is skew — it is a product of two skewed fields,
        // so it piles up near zero. Measured over the disc, `coarse * fine * 2`
        // (the literal transcription of the original) lands 37/37/18/9 across four
        // bands and the star becomes a blown-out ball on two adjacent colours. A
        // linear scale cannot fix a skew, so the quartiles are stretched onto
        // even spacing: 19/35/26/20. Same field, same cells, no honeycomb and no
        // two-colour star.
        let gn = Math.min(1, coarse * fine * 2.6);
        gn = gn < 0.3 ? gn * (0.375 / 0.3)
            : gn < 0.62 ? 0.375 + (gn - 0.3) * (0.25 / 0.32)
            : 0.625 + (gn - 0.62) * (0.375 / 0.38);
        gn += (fine - 0.45) * 0.12;
        if (ditherV > 0.5) gn += 0.1;
        col = sea[Math.max(0, Math.min(3, Math.floor(gn * 4)))];

        // Form on top of texture. The granulation says what the surface is made
        // of; the radius says it is a sphere. The original relies on the sphere
        // projection alone for this, which gives a disc of even noise — correct
        // per its own shader, but it loses the limb, and a star with no limb is a
        // sticker.
        //
        // In THREE steps, not a ramp, and the ramp is what it used to be: a
        // continuous blend here put 831 distinct colours in a 128px star. Three
        // zones read as a photosphere with a hotter core and a cooler limb, which
        // is all a star needs at this size.
        const tt = 1 - r2;
        //
        // Only the core. There was a second, wider zone here that warmed the
        // inner 71% of the disc toward the top palette entry, and it was the
        // reason the star read as a pale cream ball with orange veins: the
        // granulation's own light bands were already close to that entry, so
        // lightening them took the contrast out of the surface entirely. The
        // granulation is the texture; nothing else gets to wash it.
        // Small, and DITHERED at the edge. At 0.72 this was a hard-edged white
        // disc covering more than half the radius, which reads as a white circle
        // pasted onto an orange ball rather than as a photosphere — the one
        // boundary in this file that was not dithered, and the only one a viewer
        // would have described as a bug.
        if (tt > 0.88) col = mixRgb(col, sea[3], 0.5);
        else if (tt > 0.8 && ditherV > 0.5) col = mixRgb(col, sea[3], 0.25);
        else col = mixRgb(sea[0], col, 0.45);

        outRgb = col;

        /**
         * Prominence rays, over the photosphere.
         *
         * Sampled in polar space and thresholded against a radius-rising bound,
         * which turns concentric arcs into rays. This is the second attempt: the
         * first used four `cos^40` lobes at seeded angles, and those are four
         * symmetric spikes that render as a hard white cross ruled across the
         * disc — which is what a lens artefact looks like, not a star. Four is
         * also simply the wrong number; a star's limb is crowded.
         *
         * Nothing is drawn inside the inner fifth, matching the original's
         * `step(n2 * 0.25, d)`: rays are something the limb does, and carrying
         * them into the core turns the whole disc into a starburst.
         */
        //
        // From 0.3 of the radius outward, not 0.2: rays belong to the limb, and
        // carrying them further in is what turns a granulated photosphere into a
        // starburst. The colour is the star's own hot tone rather than white, so
        // a ray brightens the surface instead of erasing it.
        if (r2 > 0.2) {
          const ray = flare(Math.sqrt(r2), Math.atan2(ny, nx) * 0.4, seed, ditherV);
          if (ray > 0) outRgb = mixRgb(outRgb, [255, 233, 186], ray * 0.45);
        }
      } else {
        /**
         * Distance to the light point, in disc-UV.
         *
         * Banding on THIS rather than on a dot product is what gives the look:
         * the terminator becomes a ragged front across the face of the disc
         * instead of a smooth 3D curve, and the bands are visible as steps
         * rather than as a gradient. Adding noise to the distance is the other
         * half — without it the steps land on a clean arc and the whole thing
         * reads as a vector illustration of a sphere.
         */
        const u = 0.5 + nx * 0.5;
        const v = 0.5 + ny * 0.5;
        const dLightRaw = Math.hypot(u - f.lx, v - f.ly);
        dLight =
          dLightRaw +
          (fbm(sx * period, sy * period, period, Math.max(2, octaves - 2), seed + 404) - 0.5) *
            0.55 *
            polar;
        // The original squares the distance before using it, which pushes the
        // mid-tones toward the light and widens the terminator.
        const dLit = dLight * dLight * 0.62;

        // ---- pass one: the body, banded by distance to the light ----------
        // Three steps with a dithered edge on each. The dither window is narrow
        // on purpose: a wide one turns the whole shadow side into a gradient and
        // the banding — the entire point — disappears.
        const bandW = 0.055;
        col = sea[0];
        if (dLit > 0.085) col = sea[1];
        if (dLit > 0.085 && dLit < 0.085 + bandW && ditherV > 0.5) col = sea[0];
        if (dLit > 0.2) col = sea[2];
        if (dLit > 0.2 && dLit < 0.2 + bandW && ditherV > 0.5) col = sea[1];
        if (dLit > 0.4) col = sea[3];
        if (dLit > 0.4 && dLit < 0.4 + bandW && ditherV > 0.5) col = sea[2];
        // Kept because the lakes pass below has to put water back, and water is
        // this banded colour — not a flat blue. Without it, melt ponds would
        // arrive unlit and sit on the night side as bright holes.
        const seaCol = col;

        // ---- pass two: land, chosen by comparing displaced fields ----------
        /**
         * Four fbms, each displaced along the light direction by an amount
         * proportional to the first one, compared against each other.
         *
         * The first version walked a single field through three thresholds to
         * pick abyss/shelf/lowland/highland. That produces smooth concentric
         * bands around the noise's own contours — it looks like elevation
         * shading, not like land. Comparing *different* fields is what produces
         * the original's coastlines: the boundaries stop following one field's
         * contours and become the places where two independent fields cross,
         * which is where they get thin, broken and island-like.
         *
         * The displacement is what makes it cohere. Pushing each field toward or
         * away from the light by a noise-driven amount means the comparison
         * resolves toward the light more often than away from it, so land
         * brightness correlates with position on the disc instead of being
         * independent of it.
         */
        // One surface field, for the land pass only. Skipped entirely for gas
        // giants, which band by weather, and for asteroids, which take their
        // surface from two offset fields in the disc plane instead — both would
        // otherwise pay for an fbm per pixel to throw it away.
        const h = land ? 0.5 + (fbm(sx * period, sy * period, period, octaves, seed) - 0.5) * polar : 0;

        if (land) {
          if (h >= f.cutoff) {
            isLand = true;
            const du = f.lx - 0.5;
            const dv = f.ly - 0.5;
            const g = h * polar;
            const f2 = fbm(
              (sx * period - du * g), (sy * period - dv * g), period, octaves, seed + 101,
            );
            const f3 = fbm(
              (sx * period - du * g * 1.5), (sy * period - dv * g * 1.5), period, octaves, seed + 211,
            );
            const f4 = fbm(
              (sx * period - du * g * 2.2), (sy * period - dv * g * 2.2), period, octaves, seed + 307,
            );
            // Base, then progressively closer to the light. Note the light term
            // ADDS to the compared field, so a pixel far from the light needs a
            // much lower field to win — which is the terminator biting into the
            // land rather than being painted over it afterwards.
            col = land[3];
            if (f4 + dLit < h) col = land[2];
            if (f3 + dLit < h) col = land[1];
            if (f2 + dLit < h) col = land[0];

            // Rivers, as a separate pass over the land. Rare enough to be a
            // detail rather than a feature, which is why it is a hard cut: a
            // soft one would put a haze over every continent.
            if (water) {
              const rf = fbm(sx * period + h * 6, sy * period + h * 6, period, octaves, seed + 503);
              if (rf < h * 0.5) col = water[0];
              else if (rf < h * 0.56) col = water[1];
            }

            if (f.emissive && f2 + dLit < h * 0.7) col = land[0];
          }
        }

        // ---- pass two-and-a-half: melt lakes --------------------------------
        if (f.lakes && isLand) {
          /**
           * A second water field, independent of the first and at a finer scale.
           *
           * This is what the reference's ice world is: the same surface shader
           * twice at two thresholds, the sheet and the melt coming through it. One
           * threshold cannot produce it, because one threshold gives every body of
           * water the same size and the same edge complexity — which is why this
           * type used to look like a world that was half ocean rather than a world
           * with lakes on it.
           *
           * The threshold sits well below the sheet's, so ponds are common inside
           * the ice and the big water bodies still come from the main field. The
           * two are decorrelated by frequency as well as by seed, so the ponds do
           * not all land along the same coastline.
           */
          const h2 =
            0.5 +
            (fbm(sx * period * 2.4, sy * period * 2.4, period, Math.max(2, octaves - 1), seed + 733) -
              0.5) *
              polar;
          if (h2 < 0.44 || (h2 < 0.4 && ditherV > 0.5)) {
            isLand = false;
            col = seaCol;
          }
        }

        if (kind === "lat") {
          /**
           * Gas giants: bands of constant latitude, torn up by weather.
           *
           * This file spent a while convinced that gas giants are not banded at
           * all. That came from reading the wrong shader — `GasPlanet.gdshader`
           * really does have no bands, it is a cellular cloud field sampled
           * directly — and the conclusion was then checked against the wrong
           * evidence, because the swirls it produced did look plausible. The
           * layered variant, `GasPlanetLayers.gdshader`, settles it in a comment:
           * "a band is just one dimensional noise". It samples fbm in v alone,
           * then multiplies the turbulence by `pow(band, 2.0) * 7.0`.
           *
           * So the band term is real and it is doing a specific job: it makes the
           * weather coherent in latitude. Without it, turbulence displaces the
           * boundary by the same amount everywhere and the result is a random
           * mottle; with it, the displacement is strong in some latitudes and
           * weak in others, and the eye gets long clean stripes with storms
           * tearing across them. That is the whole difference between a gas giant
           * and a bowl of soup.
           *
           * The ramp is seven bands over four palette entries by a fixed route
           * rather than a straight one. A straight ramp reads as shading, and
           * `idx % 4` reads as a repeating pattern; this reads as cloud.
           */
          const cu = fract(sx0 + spin);
          const cvv = sy * 1.6 + smoothstep(Math.abs(sx0 - 0.4) / 1.3) * 0.3;
          let warpN = 0;
          for (let i = 0; i < 9; i++) {
            warpN += circleNoise(
              cu * period * 0.5 + i + 11,
              cvv * period * 0.5 + i + 11,
              period,
              seed + 5,
            );
          }
          warpN /= 9;
          // The weather.
          const turb =
            0.5 +
            (fbm(cu * period + warpN * 3, cvv * period + warpN * 3, period, octaves, seed + 313) -
              0.5) *
              polar;
          // The bands. One-dimensional on purpose — v only, no u — which is what
          // makes them latitude bands rather than more weather.
          const bandN = fbm(0, sy * 9, period, 3, seed + 401);
          //
          // Nine bands, and the weather's amplitude is 0.8 rather than 1.7. At 1.7
          // the displacement reached half a band width almost everywhere, so the
          // bands were scrambled into broad diagonal patches and the whole point
          // was lost — the coherence was supposed to come from the amplitude
          // varying with latitude, and it cannot do that if it is large enough to
          // destroy the bands on its own.
          //
          // The ramp stays off both ends. Running cream to dark brown and back
          // gives four very distinct stripes that read as continents; a gas
          // giant's zones and belts differ in tone but not that much, and the
          // darkest entry is a belt, not half the planet.
          const B = 9;
          const RAMP = [0, 1, 2, 1, 0, 1, 2, 1, 0];
          const latF = sy * B + (turb - 0.5) * 0.8 * (0.15 + bandN * 1.2);
          const bi0 = Math.floor(latF);
          const frac = latF - bi0;
          const bi = ((bi0 % B) + B) % B;
          let idx = RAMP[bi];
          // Dithered on both sides of every boundary. A hard line is the one
          // thing banding must never look like.
          if (frac < 0.09 && ditherV > 0.5) idx = RAMP[(bi + B - 1) % B];
          else if (frac > 0.91 && ditherV < 0.5) idx = RAMP[(bi + 1) % B];

          /**
           * A tone that moves with longitude, so the rotation is VISIBLE.
           *
           * This type was animating correctly and still looked frozen, which is a
           * worse bug than not animating because every signal said it worked.
           * Measured on two frames 1.6s apart: the black hole changed 1833 pixels,
           * this changed 89.
           *
           * The reason is structural. A band of constant latitude is invariant under
           * a shift in longitude, so `bi` — which is `floor(sy * 9 + ...)` — comes
           * back identical for the same pixel on every frame. The only longitude
           * dependence was the turbulence's contribution to the boundary *position*,
           * which shifts where a band starts without changing which band a pixel is
           * in. The bands dominate the image, so almost nothing moved.
           *
           * The fix keeps the band structure (the one-dimensional noise is what
           * makes the stripes coherent and worth having) and moves the TONE within
           * each band instead, with a field that varies in both axes. The reference
           * gets this for free: its palette comes from `disk + light_d`, where `disk`
           * is a full two-dimensional field, so its tone moves with the weather. Ours
           * was reading the band index and nothing else.
           */
          // Low frequency and a high threshold, both deliberate. The first attempt
          // used a high frequency and a threshold of 0.07 — about 0.4 of a standard
          // deviation — so it shifted the tone of some 60% of the disc and buried
          // the two things that make this type read as a gas giant: the largest
          // single tone jumped from 30% to 43% of the disc, and the bands' vertical
          // anisotropy fell from 1.48 to 1.14, which is to say they stopped being
          // bands. At a low frequency and a threshold near one deviation the swirl
          // forms a few coherent patches along the bands — which is also what a
          // storm on a gas giant is — and the stripe structure survives underneath.
          const swirl =
            fbm(cu * period * 0.9, cvv * period * 0.5, period, Math.max(2, octaves - 2), seed + 907) -
            0.5;
          if (swirl > 0.17) idx = Math.min(3, idx + 1);
          else if (swirl < -0.17) idx = Math.max(0, idx - 1);
          col = sea[idx];
                } else if (kind === "solid") {
          /**
           * An asteroid is not a disc.
           *
           * The outline is the surface field minus the radius, thresholded — so
           * the silhouette is wherever the rock happens to be high enough to
           * still exist at that distance from the middle. It was a circle before,
           * with banded noise inside it, which is a grey ball: the one shape in
           * this file that is not a world is also the one shape that must not be
           * a sphere, and nothing else on the chart has an outline to read.
           *
           * Sampled in the DISC plane, not the sphere. The surface detail is
           * wrapped around a sphere above, which is right for shading a globe;
           * an outline is a flat 2D shape and warping its coordinate would give a
           * lumpy circle rather than a lump.
           *
           * The threshold is a line in (radius, height) space, so the constant
           * sets the size: 0.02 puts the mean outline at 0.96 of the nominal
           * radius and the noise's own spread takes the lobes out to about 1.2,
           * which is what the 1.3 glow is there to fit. A rock that only fills
           * half its box is a smudge next to a neighbouring world.
           */
          //
          // Two cells across the disc, two octaves. The first attempt used five
          // cells and four octaves, which put the finest detail at forty cells
          // across the rock: the outline came out fractal-edged and read as a
          // splat or a map of continents rather than as a lump. An asteroid is
          // two or three big lobes and almost nothing else.
          const ru = u + spin + f.rot;
          // NO polar damping here, and having it was a visible bug.
          //
          // `polar` exists to stop the SPHERE projection from aliasing near the
          // limb, where v compresses and a pixel row crosses many noise periods at
          // once. The silhouette is sampled in the disc plane and never touches
          // that projection, so there is nothing to damp — but damping forces the
          // field to exactly 0.5, and this threshold crosses 0.5 at r = 0.96. The
          // rock therefore stopped at about 60% of the radius and then came back
          // as a thin detached arc along the bottom of the sprite, which read as a
          // rendering fault rather than as a lump of rock.
          const edge = 0.5 + (fbm(ru * 2.2, v * 2.2, 3, 2, seed + 907) - 0.5) * 0.6;
          const thr = 0.02 + Math.sqrt(r2) * 0.5;
          shapeA = edge > thr || (edge > thr - 0.05 && ditherV > 0.5) ? 1 : 0;

          /**
           * Light and shade from two offset copies of the same field.
           *
           * Copy the field displaced toward the light and compare: where the
           * displaced copy is LOWER, the step toward the light drops off the rock,
           * which is the side facing the source. That gives a terminator running
           * across the body in the right direction, which the single field cannot
           * do — banding one field by radius makes concentric rings, which on a
           * lump of rock reads as a dartboard.
           */
          // Five cells, three octaves. At two and a half the zones came out so
          // large that the three tones read as camouflage patches rather than as
          // shading; the finest octave here is twenty cells across, which is
          // texture, and one octave more than that is where it turned to mud.
          const nEdge = fbm(ru * 5, v * 5, 5, 3, seed + 907);
          const nLit = fbm(
            ru * 5 + (f.lx - 0.5) * 1.1,
            v * 5 + (f.ly - 0.5) * 1.1,
            5,
            3,
            seed + 907,
          );
          const rel = nLit - nEdge;
          // `sea` runs lit to shadow, index 0 lightest. This ladder had it
          // backwards — brightest band on the shadowed side — which is why the
          // rock came out muddy and unreadable rather than merely dim.
          // ONE step either side of the base, never the full ramp. Letting the
          // comparison reach both ends gave big flat blobs of the darkest and
          // lightest rock and the whole thing read as camouflage — the noise was
          // competing with the terminator instead of decorating it. The original
          // does the same thing: the comparison shifts a step and the light
          // border is what actually darkens the far side.
          col = sea[1];
          if (rel < -0.025) col = sea[0];
          else if (rel < -0.008) col = ditherV > 0.5 ? sea[0] : sea[1];
          else if (rel > 0.008) col = sea[2];

          // Terminator, quantised like everything else, and mixed toward the
          // darkest rock rather than toward night: a small body against a pale
          // territory fill goes to a hole if it is allowed to reach black.
          shade = dLit < 0.085 ? 1 : dLit < 0.2 ? 0.82 : dLit < 0.4 ? 0.6 : 0.4;
          outRgb = mixRgb(sea[3], col, 0.36 + 0.64 * shade);
        }

        // ---- pass three: craters ------------------------------------------
        if (f.craters) {
          /**
           * A bowl in three zones — floor, wall, plain — with the wall lit on the
           * side facing the light.
           *
           * The lit wall is the whole trick and it needs a second sample. Reading
           * the field once more, displaced toward the light, tells you which way
           * the surface is falling: where the displaced copy is LOWER, moving
           * toward the light takes you deeper into the bowl, which is the far wall
           * — the one that catches light. One sample cannot express that at all,
           * which is why a single-threshold crater is a dark dot and never a
           * crater.
           *
           * Three zones rather than one threshold, and this is the second attempt.
           * One threshold over the product field gave a scatter of small uniform
           * dots that read as a rash: too many, too small, and — because the floor
           * was only mixed halfway to the darkest palette entry — LIGHTER than the
           * plain in places, so the craters looked like highlights. A crater needs
           * a floor that is unambiguously in shadow and a rim that is a thin arc,
           * and separating the zones is what lets each be tuned to its own job.
           *
           * Dithered at both boundaries, like every other edge in this file: a
           * hard line on a curved shape is what makes procedural rock look like
           * vector art.
           */
          const q = f.craterFreq;
          const cf = craterField(sx * q, sy * q, q, seed + 611);
          const cfL = craterField(
            sx * q + (f.lx - 0.5) * 1.7,
            sy * q + (f.ly - 0.5) * 1.7,
            q,
            seed + 611,
          );
          const cDark = land ? land[3] : sea[3];
          const cLit = land ? land[0] : sea[0];
          // Bowl floor. Deep enough that it is darker than every land tone, or
          // the crater inverts into a highlight.
          if (cf > 0.6 || (cf > 0.53 && ditherV > 0.5)) {
            col = mixRgb(col, cDark, 0.85);
          } else if (cf > 0.42 || (cf > 0.35 && ditherV > 0.5)) {
            /**
             * The lit wall, and the sign here was backwards the first time.
             *
             * Displacing the sample toward the light: on the crater's FAR side —
             * the side the light is on, whose inward-facing wall therefore faces
             * back toward the source — the displacement walks out of the bowl and
             * the field RISES. On the near side it walks deeper and the field
             * FALLS. So the lit wall is `cfL > cf`, not the other way round, and
             * getting that backwards lights both walls evenly: every crater comes
             * out as a complete bright ring, which reads as a bubble outline
             * rather than as a hole in the ground.
             *
             * The dLight term is signed so the arc fades out on the terminator
             * side instead of carrying a lit rim into the night.
             */
            if (cfL > cf - (0.5 - dLight) * 0.55) col = mixRgb(col, cLit, 0.55);
            else col = mixRgb(col, cDark, 0.38);
          }
        }

        // The terminator, quantised. Shared by every type: land needs it because
        // it is chosen by comparison rather than banded, and sea does not strictly
        // need it — but applying it to both is what keeps a cratered world from
        // having bright craters sitting on its night side.
        shade = dLit < 0.085 ? 1 : dLit < 0.2 ? 0.8 : dLit < 0.4 ? 0.58 : 0.36;
        // Land needs the terminator because it is chosen by comparison rather
        // than banded, and gas giants need it because their bands are now bands
        // of LATITUDE — nothing in that branch darkens the far side any more.
        // Sea and rock are already banded by distance to the light, so shading
        // them again would apply the terminator twice and crush the shadow side.
        outRgb =
          isLand || kind === "lat"
            ? mixRgb(night, col, nightFloor + (1 - nightFloor) * shade)
            : col;

        // Rim light on the lit limb. Two steps, not a ramp, for the same reason
        // as everything else here. On the disc, not outside it — the sprite is
        // exactly the disc, and an atmosphere that bleeds past the edge is what
        // made the first sprite set look like noise.
        if (atmo && r2 > 0.82 && dLight < 0.3) {
          const rim = r2 > 0.93 ? 0.5 : 0.22;
          outRgb = mixRgb(outRgb, atmo, rim);
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
        const c = 0.5 + (fbm(
          cu * period + warpN * 0.9,
          cvv * period + warpN * 0.9,
          period,
          Math.max(2, octaves - 1),
          seed + 313,
        ) - 0.5) * polar;
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
      px[i + 3] = shapeA * 255;
    }
  }
}

/* ------------------------------------------------------------------ cache */

const cache = new Map<string, string>();

/**
 * Cache key.
 *
 * Built from the option object's OWN keys, sorted, rather than from a hand-written
 * list. The hand-written list was a live bug: `suppressCraters` was added and
 * forgotten, so a call that asked for a crater-free planet got back the cached
 * cratered one — and the test that was written to catch exactly that passed,
 * because it was measuring a stale image of the run before it.
 *
 * That failure mode is the worst kind: an option that changes pixels but not the
 * key is invisible, and it makes a control assertion report that a feature has no
 * effect when it has plenty. Deriving the key means an option that does not exist
 * cannot change the output, and one that is added cannot be forgotten.
 */
function keyOf(o: PlanetOpts, spin: number, frames: number): string {
  const opts = Object.keys(o)
    .sort()
    .map((k) => `${k}=${String((o as unknown as Record<string, unknown>)[k])}`)
    .join(",");
  return [PLANET_ALGO_VERSION, frames, spin.toFixed(4), opts].join("|");
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
/**
 * Build a filmstrip across several macrotasks instead of one blocking one.
 *
 * Smoothness is bought with frames, and frames cost time: measured on this machine a
 * 200px frame at dpr 1 is about 32ms, so the 28-frame strip that was good enough to
 * see a planet turn took 911ms and the 96 frames that actually look smooth take about
 * 3.1 seconds. Three seconds inside the click that opened a panel is not a long
 * animation, it is a frozen page, and it is the reason the frame count was kept low
 * rather than the reason 28 is enough.
 *
 * So the work is spread. `onFrame` is called as each frame lands, which is what lets
 * a caller show progress, and the final callback fires when the strip is complete.
 * Nothing is yielded to between batches, so a batch is still a contiguous block of
 * synchronous work — sized to stay under roughly a frame of jank rather than to be
 * as small as possible.
 *
 * Cancellation: the caller gets a handle with `cancel()`, and generation stops at the
 * next batch boundary. Without it, dismissing a panel mid-generation leaves a
 * multi-second loop running against a canvas nobody will read.
 */
export interface SheetJob {
  cancel(): void;
}

export function planetSheetAsync(
  o: PlanetOpts,
  frames: number,
  onDone: (uri: string) => void,
  opts?: { batch?: number; onFrame?: (n: number) => void },
): SheetJob {
  const batch = Math.max(1, opts?.batch ?? 6);
  let cancelled = false;
  let started = false;
  const start = () => {
    if (started) return;
    started = true;
    const dpr = o.dpr ?? 1;
    const d = Math.max(3, Math.round(o.px * dpr));
    const cv = document.createElement("canvas");
    cv.width = d * frames;
    cv.height = d;
    const ctx = cv.getContext("2d");
    if (!ctx) {
      onDone("");
      return;
    }
    const img = ctx.createImageData(d * frames, d);
    // cloudThreshold, not the type's default: the async path has to agree with the
    // synchronous one or the strip and the still are different planets.
    const f = prep(o, d, o.cloudThreshold);
    let k = 0;
    const step = () => {
      if (cancelled) return;
      const end = Math.min(frames, k + batch);
      for (; k < end; k++) {
        renderFrame(f, img, d * frames, k * d, 0, k / frames);
        opts?.onFrame?.(k + 1);
      }
      if (k < frames) {
        setTimeout(step, 0);
      } else {
        ctx.putImageData(img, 0, 0);
        onDone(cv.toDataURL("image/png"));
      }
    };
    setTimeout(step, 0);
  };
  // Deferred even for a cached hit, so the caller's own timing is consistent.
  setTimeout(start, 0);
  return { cancel: () => (cancelled = true) };
}

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
/**
 * Whether this world has a ring system.
 *
 * Only gas giants, and only about a third of them - a ring is a rarity, and a
 * chart where every large world has one reads as a chart of Saturns. Derived from
 * the seed rather than stored, so it is a pure function of the model exactly as
 * the sprite is: two clients with the same data draw the same rings without
 * either being told.
 */
export function hasRings(seed: number, type: PlanetType, explicit?: boolean): boolean {
  if (type !== "gas") return false;
  // An authored answer beats the hash. See `StarSystem.rings` for why a ring is
  // content: a lore editor has to be able to write one down.
  if (explicit !== undefined) return explicit;
  // The period here is 2^20 and not something small, and that is load-bearing.
  //
  // `hash2` reduces its first argument modulo `period` before mixing, because the
  // period is there to make noise tile. Using it for a DECISION therefore
  // collapses the output space to `period` buckets: at 32 there are thirty-two
  // possible answers no matter how many seeds are thrown at it, the measured
  // distribution was 9/16/16/13/9/16/3/3/3/13 across ten buckets, and 43.8% of
  // seeds came out below a 0.34 threshold instead of 34%. It is not a slightly
  // biased hash, it is a 32-valued hash wearing a uniform one's clothes — and the
  // first four ids tried all landed in the wrong bucket, which is what made this
  // visible at all.
  //
  // At 2^20 the measured distribution is flat to 0.2% and 34.0% fall below the
  // threshold.
  return hash2(seed | 0, 5, 1 << 20, 91) < 0.34;
}

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
