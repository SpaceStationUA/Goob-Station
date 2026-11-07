/**
 * The ring's constants, from `GasPlanetLayers.tscn`.
 *
 * Same arrangement as `blackhole-consts.ts`, and for the same reason: there are two
 * ring renderers — a WebGL one for the overlay and an SVG one for the chart's
 * markers — and they were built from a guess rather than from the source, so they
 * drifted and produced a flat ribbon rather than a ring.
 *
 * ## The declared defaults are not the scene's values
 *
 *     uniform                declared    scene sets
 *     ring_perspective          4.0          6.0
 *     rotation                  0.0          0.7
 *     ring_width                0.1        0.127
 *     size                     50.0         15.0
 *     OCTAVES                   --            4
 *     scale_rel_to_planet       6.0          6.0
 *
 * `ring_perspective` is the one with teeth. It is the ring's foreshortening: 4.0
 * opens the ring to a quarter and it reads as a hoop lying on the planet, 6.0 is
 * edge-on enough to read as a ring seen from above the plane. The SVG ring was
 * authored against 0.22, which is neither, and 0.22 is also what a person reaches
 * for when asked "how open should this look" without the source in front of them.
 *
 * ## The planet's hole is drawn by the ring, not by the sprite
 *
 * `Ring.gdshader` cuts the planet out of its own upper half:
 *
 *     if (uv.y < 0.5) ring *= step(1.0 / scale_rel_to_planet, distance(uv, vec2(0.5)));
 *
 * So the occlusion is geometry, sized in the ring's own uv, and it does not depend
 * on the planet sprite being opaque across its whole disc. The HTML version got that
 * by accident instead: it put the far half behind a `<img>` and relied on the image
 * being transparent outside the planet. That is fragile in a way that is invisible
 * until the sprite's dithered edge stops lining up, and it cannot work at all in a
 * single composited canvas.
 *
 * The ring canvas is 300x300 and the planet 100x100 in the scene, both centred, so
 * the planet's radius is 1/6 of the ring's — which is exactly the `1/6` the shader
 * compares against. That identity is the whole reason the hole lands on the
 * planet's edge, and it is why `HOLE_SCALE` below is derived from the canvas and
 * the planet's radius rather than being a literal.
 */

/** The scene's `ring_perspective`: the ring's foreshortening. */
export const PERSPECTIVE = 6.0;

/** The scene's `rotation`, radians. */
export const ROTATION = 0.7;

/** The scene's `ring_width`, in the annulus' own units. */
export const RING_WIDTH = 0.127;

/** The scene's `time_speed`. The ring's texture turns at this rate. */
export const TIME_SPEED = 0.2;

/** The scene's `size`: the noise's tiling period, and `round()`ed inside `rand`. */
export const NOISE_SIZE = 15.0;

/** The scene's `OCTAVES`. */
export const OCTAVES = 4;

/** The scene's UV quantisation, in divisions of the canvas. */
export const PIXELS = 300;

/** The scene's `light_origin`, which sits OUTSIDE the canvas to the left. */
export const LIGHT_ORIGIN: readonly [number, number] = [-0.1, 0.3];

/**
 * `step(0.28, ring)` — the ring's alpha is a STEP.
 *
 * And the fbm decides where the boundary falls, so this cuts ragged holes in the
 * annulus rather than trimming it evenly. That is the ring's structure: the gaps
 * are where the noise fell below the cut, which is why the reference's rings have
 * divisions in them and a flat annulus does not.
 */
export const ALPHA_CUT = 0.28;

/**
 * The scene's three lit tones.
 *
 * Light to dark, and indexed by `posterized` before it crosses 1.0 — after that the
 * shader switches to `DARK` entirely rather than continuing through these.
 */
export const COLORS: readonly string[] = [
  "#eec39a", // 0.933333, 0.764706, 0.603922
  "#b37a50", // 0.701961, 0.478431, 0.313726
  "#8f563b", // 0.560784, 0.337255, 0.231373
];

/**
 * The scene's three shadowed tones — LIFTED, and that is a deviation.
 *
 * The reference's are #553036, #322337 and #222033. They are dark plums and
 * charcoals, chosen to sit against the generator's own backdrop, which is a mid
 * grey. This page's panel is #080d16 at 90%, so that shadowed end lands at
 * luminance 8 to 20 against a background at 12 and the outer half of the ring
 * simply vanishes. What is left is one lit arm and nothing else, which reads as a
 * broken ring rather than as a shadowed one.
 *
 * So the three are raised, keeping their hue relationships and their spacing, and
 * only far enough that the darkest still reads as the darkest part of the ring:
 *
 *     reference      lifted
 *     #553036        #7a4a4e
 *     #322337        #4e3b57
 *     #222033        #3b3950
 *
 * This is the same class of decision as the ring's size: the reference was authored
 * against a different backdrop and a different frame, and both times the answer was
 * to keep its structure and move its presentation rather than the reverse.
 */
export const DARK: readonly string[] = [
  "#7a4a4e", // was #553036
  "#4e3b57", // was #322337
  "#3b3950", // was #222033
];

/**
 * How much wider than the planet's diameter the ring canvas is.
 *
 * **This is the one deliberate deviation from the reference, and it is a size.**
 *
 * The reference's own value is 3: its ring canvas is 300px against a 100px planet.
 * That number and `RING_OUTER` together fix the ring's radius in planet radii, and
 * the reference's is 3.16 — a ring wider than the planet by more than three times
 * its radius, which is grander than Saturn's own A ring at about 2.3.
 *
 * It also does not fit. 3.16 radii around a 200px planet is 632px across, and the
 * overlay panel is 490px even once widened for the ringed case. The first attempt
 * used the reference's 3.0 and the ring was clipped at both edges, which reads as a
 * rendering fault rather than as a crop.
 *
 * So: 2.1, which gives 2.21 planetary radii. That is both affordable and *closer to
 * the solar system's own ring than the reference is*, so the compromise costs less
 * than it looks. Everything else in this file is the reference's.
 */
export const CANVAS_TO_PLANET = 2.1;

/**
 * The ring's outer edge, in the canvas's own uv.
 *
 * Not a free choice: the shader derives it. `center_d` runs from
 * `0.5 - ring_width` up to `0.5 + ring_width` (the second smoothstep has its edges
 * reversed, so it is a ramp falling to zero at `0.4 + ring_width`), and `center_d` is
 * measured in a space where the canvas half-width is `0.5`. So the outer edge lands
 * at `(0.4 + RING_WIDTH) / 0.5` of the half-width, which is `1.054` — very slightly
 * past the canvas, and clipped, exactly as in the reference.
 */
export const RING_OUTER = (0.4 + RING_WIDTH) / 0.5;

/** Where the ring's inner edge falls, in uv. The gap the planet sits in. */
export const RING_INNER = 0.5 - RING_WIDTH;

/**
 * `scale_rel_to_planet` for a given canvas size and planet radius.
 *
 * The shader compares `distance(uv, centre)` against `1 / scale_rel_to_planet`, and
 * that distance is in uv, so the hole it cuts has a radius of `canvas / scale` in
 * pixels. To land that on the planet's edge:
 *
 *     canvas / scale = planetRadius   =>   scale = canvas / planetRadius
 *
 * Which is why the hole stays on the planet when the canvas is resized, and why
 * dividing by three anywhere else would be wrong for the same reason it was wrong
 * for the black hole's horizon: the shader is already in the sprite's own uv.
 */
export function holeScaleFor(canvasPx: number, planetRadiusPx: number): number {
  return canvasPx / planetRadiusPx;
}