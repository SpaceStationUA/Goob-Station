/**
 * The reference's constants, in one place, because there are two renderers.
 *
 * `gl.ts` draws the black hole live on a canvas; `blackhole.ts` bakes it to a data
 * URI for the chart's marker. They were written at different times, from different
 * sources, and they disagreed: the overlay looked like the reference and the map
 * marker still looked like a rounded cigar. A check that compares the two would only
 * catch that after it had already shipped, so the constants live here instead and
 * both files import them. Drift is then not a thing that can happen.
 *
 * The values are `BlackHole.tscn`'s, NOT the shaders' declarations. That
 * distinction is the whole ballgame and the source file is next to this one:
 *
 *     uniform            declared    scene sets
 *     ring_perspective     4.0         14.0
 *     disk_width           0.1        0.065
 *     size                50.0         6.598
 *     OCTAVES               --            3
 *     rotation             0.0         0.766
 *     radius               0.5        0.247
 *     light_width         0.05        0.028
 *
 * `ring_perspective` accounts for most of the difference: at 4.0 the disc is
 * foreshortened four to one and reads as a fat ellipse, at 14.0 it is the thin sweep
 * it is meant to be.
 */

/** The disc's five steps, bright to DARK. */
export const DISC: [string, string, string, string, string] = [
  "#ffffeb",
  "#fff540",
  "#ffb84a",
  "#ed7b39",
  "#bd4035",
];

/**
 * The horizon's three steps: a blue-grey void, a WHITE ring, an ORANGE outer ring.
 *
 * The order is the scene's, and it is not "dark to light" — `BlackHole.gdshader`
 * starts at `colors[0]` and steps outward twice, so this array runs void, white,
 * orange, and the white sits at `radius - light_width` with orange outside it.
 */
export const HOLE: [string, string, string] = ["#272737", "#ffffeb", "#ed7b39"];

/**
 * How many times the horizon's sprite is smaller than the disc's canvas.
 *
 * In `BlackHole.tscn`, `BlackHole` spans 100x100 and `Disk` spans 300x300, both
 * centred on the same point. The disc shader scales its own uv by this to get into
 * the horizon sprite's space, which is why `HOLE_RADIUS` below is the scene's
 * number UNCHANGED rather than divided by three. Dividing it here as well divides
 * twice and makes the horizon three times too small, which is what it was.
 */
export const HOLE_CANVAS_RATIO = 3;

/** The scene's `ring_perspective`. Foreshortening of the disc, and of the light. */
export const PERSPECTIVE = 14.0;

/** The scene's `disk_width`. */
export const DISK_WIDTH = 0.065;

/** The scene's `size`: the noise's tiling period, and `round()`ed inside `rand`. */
export const NOISE_SIZE = 6.598;

/** The scene's `OCTAVES`. */
export const OCTAVES = 3;

/** The scene's `rotation`. */
export const ROTATION = 0.766;

/** The scene's `time_speed`. */
export const TIME_SPEED = 0.2;

/** The disc's UV quantisation, in divisions of the canvas. */
export const DISC_PIXELS = 300;

/** The horizon's UV quantisation, in divisions of ITS sprite. */
export const HOLE_PIXELS = 100;

/** The scene's `radius`, in the horizon sprite's own uv. */
export const HOLE_RADIUS = 0.247;

/** The scene's `light_width`, in the horizon sprite's own uv. */
export const HOLE_LIGHT_WIDTH = 0.028;

/**
 * `step(0.15, disk)` — the disc's alpha is a STEP, opaque or absent.
 *
 * Treating it as a ramp is most of what turns the band into a smear, because the
 * palette is indexed by a different value and a ramp lets the two disagree.
 */
export const ALPHA_CUT = 0.15;

/** The disc's palette index range, so `posterized` is clamped to it. */
export const N_COLORS = DISC.length;
