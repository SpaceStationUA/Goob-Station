/**
 * GLSL shared between the background shaders.
 *
 * `Nebulae.shader` and `StarStuff.shader` are two layers of one background and they
 * are near-identical programs: the same `rand`, the same `noise`, the same `fbm`,
 * the same `circleNoise`, the same `cloud_alpha`, the same `dither`, the same
 * `rotate`, with a different arrangement of them at the end. Vendored beside this
 * file in `reference/space/`.
 *
 * All of those are also the primitives every other shader in this project uses --
 * the accretion disc, the ring, and `planet.ts` on the CPU -- and they are the same
 * hash in all of them:
 *
 *     fract(sin(dot(coord, vec2(12.9898, 78.233))) * 15.5453)
 *
 * That sameness is the reason to have picked this generator over hand-drawn art, and
 * it is worth keeping in one string rather than four copies of it.
 *
 * Two deviations from a literal transcription, both forced, both documented at the
 * point they occur:
 *
 *  - **`OCTAVES` is a constant, not a uniform.** GLSL ES 1.00 requires a constant
 *    expression as a loop bound, so the background's two octave counts are
 *    interpolated into the source rather than set. `StarStuff` wants 8, so this is
 *    not a small number being unrolled by accident.
 *  - **`texture(colorscheme, …)` becomes `ramp()`.** The reference samples a
 *    one-pixel-tall `GradientTexture1D`. In WebGL that is a 1D lookup, which is a
 *    uniform array and a lerp -- and it is exact rather than approximate, since
 *    Godot's gradient here has eight evenly spaced stops.
 */

/** The shared prelude. `${octaves}` is interpolated by the caller. */
export function noisePrelude(octaves: number): string {
  return `
uniform float u_size;
uniform float u_seed;
uniform float u_pixels;
uniform vec3  u_palette[8];
uniform vec4  u_background;

/**
 * The aspect correction, and it is here because BOTH layers use it.
 *
 * Declaring it per-shader put it only in Nebulae, which compiled and drew normally
 * while StarStuff failed on an undeclared identifier and drew nothing at all.
 *
 * Nothing said so. The layer helper logs the compile error and returns; the second
 * layer then covers the ground the first would have drawn; and the result is
 * indistinguishable from a layer that is merely subtle. It was found by a NEGATIVE
 * CONTROL -- suppressing StarStuff and getting byte-identical numbers -- which is
 * the only reason it was found at all. Hence also Nebula.layers, so a check can
 * assert that both programs built. (No backticks in these comments: they terminate
 * the template literal the shader lives in. Fourth time.)
 */
uniform vec2  u_uvCorrect;

/**
 * The reference's hash. Note the seed is ADDED to the multiplier here, where the
 * planet shaders MULTIPLY by it:
 *
 *     planet shaders:  fract(sin(dot(c, k)) * 15.5453 * seed)
 *     background:      fract(sin(dot(c, k)) * (15.5453 + seed))
 *
 * Both are faithful to their own source, and they are not the same function. Copying
 * one into the other would change every background without changing a line of the
 * original, which is the sort of thing that is invisible until two images that should
 * match do not.
 */
float rnd(vec2 coord, float tilesize) {
  return fract(sin(dot(coord, vec2(12.9898, 78.233))) * (15.5453 + u_seed));
}

float vnoise(vec2 coord, float tilesize) {
  vec2 i = floor(coord);
  vec2 f = fract(coord);
  float a = rnd(i, tilesize);
  float b = rnd(i + vec2(1.0, 0.0), tilesize);
  float c = rnd(i + vec2(0.0, 1.0), tilesize);
  float d = rnd(i + vec2(1.0, 1.0), tilesize);
  vec2 cubic = f * f * (3.0 - 2.0 * f);
  return mix(a, b, cubic.x) + (c - a) * cubic.y * (1.0 - cubic.x) + (d - b) * cubic.x * cubic.y;
}

float fbm(vec2 coord, float tilesize) {
  float value = 0.0;
  float scale = 0.5;
  for (int i = 0; i < ${octaves}; i++) {
    value += vnoise(coord, tilesize) * scale;
    coord *= 2.0;
    scale *= 0.5;
  }
  return value;
}

/** Ordered dither. Transcribed with its arguments the reference passes them. */
bool dither(vec2 uv1, vec2 uv2) {
  return mod(uv1.y + uv2.x, 2.0 / u_pixels) <= 1.0 / u_pixels;
}

/** By Leukbaars, from https://www.shadertoy.com/view/4tK3zR */
float circleNoise(vec2 uv, float tilesize) {
  float uv_y = floor(uv.y);
  uv.x += uv_y * 0.31;
  vec2 f = fract(uv);
  float h = rnd(vec2(floor(uv.x), floor(uv_y)), tilesize);
  float m = length(f - 0.25 - (h * 0.5));
  float r = h * 0.25;
  return smoothstep(0.0, r, m * 0.75);
}

/** Two iterations of circleNoise summed into the fbm's domain. */
float cloud_alpha(vec2 uv, float tilesize) {
  float c_noise = 0.0;
  for (int i = 0; i < 2; i++) {
    c_noise += circleNoise(uv * 0.5 + (float(i + 1)) + vec2(-0.3, 0.0), ceil(tilesize * 0.5));
  }
  return fbm(uv + c_noise, tilesize);
}

/**
 * The colourscheme, as a lerp across its eight evenly spaced stops.
 *
 * The reference samples a 1D gradient texture at \`vec2(col_value, 0)\`, and its
 * Colorscheme.tres puts the stops at 0, 1/7, ... 1, so this is exact.
 */
vec3 ramp(float t) {
  float x = clamp(t, 0.0, 1.0) * 7.0;

  // UNROLLED, and it has to be.
  //
  // The obvious version is a loop with u_palette[i], and it will not compile:
  // GLSL ES 1.00 permits only CONSTANT index expressions on a uniform array, so
  // "the index is an int" is a compile error rather than a runtime one. Every index
  // below is a literal, which is the only reason this works at all.
  vec3 a = u_palette[0];
  vec3 b = u_palette[1];
  if (x >= 6.0)      { a = u_palette[6]; b = u_palette[7]; }
  else if (x >= 5.0) { a = u_palette[5]; b = u_palette[6]; }
  else if (x >= 4.0) { a = u_palette[4]; b = u_palette[5]; }
  else if (x >= 3.0) { a = u_palette[3]; b = u_palette[4]; }
  else if (x >= 2.0) { a = u_palette[2]; b = u_palette[3]; }
  else if (x >= 1.0) { a = u_palette[1]; b = u_palette[2]; }
  return mix(a, b, fract(x));
}
`;
}