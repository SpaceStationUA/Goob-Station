/**
 * A star, live on a canvas.
 *
 * ## Provenance
 *
 * Transcribed from `cosmoglyph/shaders/star.glsl`, v5, by Luke100000 — MIT, and the
 * author wrote in the itch.io comments, unprompted: *"It's hereby under the MIT
 * license. Feel free to extract the shaders, or code in general."* There are no
 * per-shader headers in the `.love`; the licence is at the repository level, which is
 * the normal arrangement and the one the comment confirms.
 *
 * That matters here for a reason specific to this project: PixelSpace is MIT for the
 * code but carries a condition on the generated images, so running it in the page was
 * sanctioned and baking a background was not. Cosmoglyph has no such condition.
 *
 * ## Why this needed the reference rather than an invention
 *
 * Because a star drawn the way everything else here is drawn looks like a bright disc.
 * Two things in the reference are what make it not look like that, and neither is
 * reachable by adding more colour:
 *
 *  - **It is palette-INDEXED, and dithered.** Every pixel picks an INDEX into a small
 *    palette — `paletteColor(PaletteTex, size, AccretionOffset + band)` — and the
 *    mask that decides coverage is an ordered `bayer4` threshold with a `discard`. The
 *    cell-shaded look is not a filter applied afterwards; it is how every pixel is
 *    decided. Ours lerps RGB, which is why ours reads as smooth next to this.
 *  - **The prominences are LOOPS, not beams.** `flareLoop` anchors a loop on the
 *    sphere from a hash (angle, depth, radius), projects it into view, and draws an
 *    elliptical arc with two independent wobbles and a hashed GAP so it is never a
 *    closed ellipse. Thirty-two of them, each thresholded by index. A prominence is a
 *    loop of plasma held above the surface by a magnetic field; drawing it as a beam
 *    is the same mistake as drawing a quasar jet as a rectangle.
 *
 * Also transcribed and worth stealing independently: granulation scrolled by activity,
 * and SPOTS carved out of the surface by a second channel of the same noise, which is
 * how a star gets a few dark patches without a second texture.
 *
 * ## What is NOT the reference's
 *
 * **The 3D.** The original orients the body with quaternions, intersects a real sphere
 * per pixel, and projects loop anchors through the camera. This is a 2D panel, so the
 * sphere is analytic — `r = length(p)`, normal from `sqrt(1 - r^2)` — and a loop anchor
 * is projected by dropping its `z`. The consequence is honest and worth stating: a loop
 * anchored near the limb has its anchor almost on the silhouette, where the real one
 * would be foreshortened into invisibility. So this draws loops a little too readily at
 * the edge, which is a deviation.
 *
 * **The hash.** Cosmoglyph's is `fract(sin(v * 12.9898) * 43758.5453)`, which is NOT the
 * hash in `glsl.ts` — that one is `fract(sin(dot(c, k)) * (15.5453 + seed))`, faithfully
 * from PixelSpace. Two references, two hashes, and `glsl.ts`'s own comment says
 * copying one into the other "would change every background without changing a line of
 * the original". So this file carries Cosmoglyph's hash, verbatim, and does not reach
 * into `glsl.ts`. A star built on the background's grain would not be the reference's
 * star.
 *
 * ## Palette indices, and why the ramp is unrolled
 *
 * GLSL ES 1.00 permits only CONSTANT index expressions on a uniform array. `ramp()` in
 * `gl-ring.ts` is unrolled for exactly this reason and hit exactly this error. Same
 * here, same reason, so there are two unrolled lookups rather than one clever one.
 */

const OCTAVES = 4;

export interface StarOpts {
  canvasPx: number;
  seed: number;
  /** Surface colours, 2-6. The reference's default is 4. */
  colors?: number;
  /** Corona colours. The reference builds a 2-stop corona palette. */
  coronaColors?: number;
  activity?: number;
  granulation?: number;
  spots?: number;
  corona?: number;
  flares?: number;
  animate?: boolean;
}

export interface StarGL {
  canvas: HTMLCanvasElement;
  dispose(): void;
}

/**
 * The default palette, in the reference's own order: a hot core running out to a cool
 * limb, then the corona. These are INDICES, which is the point — the surface is 4 bands
 * and the corona is 2, and the shader decides which band a pixel is rather than
 * computing a colour for it.
 */
const SURFACE_BANDS: [string, string, string, string] = [
  "#fffdf2", // 0 photosphere core, the hottest thing here and very nearly white
  "#ffe89a",
  "#ffab4a",
  "#e0561f", // 3 limb, deep and red
];

// Two stops, as the reference builds them (`{ star = colors, corona = 2 }`). Order
// matters: band 0 is the corona's inner edge and is therefore the brighter of the two.
// The dim band is a desaturated warm shadow rather than a second hue. It was a
// saturated violet, and a saturated darker colour at the corona's outer edge does not
// read as haze -- it reads as an OUTLINE drawn round the star, which is the opposite of
// what a corona is.
const CORONA_BANDS: [string, string] = ["#ffd9a0", "#a8664a"];

const VERT = `
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  // Y flipped, as everywhere else here: Godot's UV has (0,0) at the top left and
  // WebGL's at the bottom left.
  v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`;

const FRAG = `
precision highp float;

uniform vec2  u_resolution;
uniform float u_time;
uniform float u_seed;
uniform float u_phase;

uniform float u_bodyRadius;
uniform float u_colors;
uniform float u_coronaColors;
uniform float u_activity;
uniform float u_granulation;
uniform float u_spots;
uniform float u_corona;
uniform float u_flares;

uniform vec3 u_surface0;
uniform vec3 u_surface1;
uniform vec3 u_surface2;
uniform vec3 u_surface3;
uniform vec3 u_corona0;
uniform vec3 u_corona1;

varying vec2 v_uv;

const float PI  = 3.14159265359;
const float TAU = 6.28318530718;

// Cosmoglyph's hash, verbatim. Not glsl.ts's -- see the file header.
float hash(float value) {
  return fract(sin(value * 12.9898) * 43758.5453);
}

float hash2(vec2 p) {
  return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

// The 4x4 ordered dither, transcribed branch for branch. Verbatim rather than the
// usual 16-entry matrix lookup, because this is a transcription and the branch form is
// what the reference has.
float bayer4(vec2 position) {
  vec2 cell = mod(floor(position), 4.0);
  if (cell.y < 1.0) {
    if (cell.x < 1.0) return 0.03125;
    if (cell.x < 2.0) return 0.53125;
    if (cell.x < 3.0) return 0.15625;
    return 0.65625;
  }
  if (cell.y < 2.0) {
    if (cell.x < 1.0) return 0.8125;
    if (cell.x < 2.0) return 0.3125;
    if (cell.x < 3.0) return 0.9375;
    return 0.4375;
  }
  if (cell.y < 3.0) {
    if (cell.x < 1.0) return 0.21875;
    if (cell.x < 2.0) return 0.71875;
    if (cell.x < 3.0) return 0.09375;
    return 0.59375;
  }
  if (cell.x < 1.0) return 0.578125;
  if (cell.x < 2.0) return 0.078125;
  if (cell.x < 3.0) return 0.878125;
  return 0.378125;
}

float vnoise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  float a = hash2(i);
  float b = hash2(i + vec2(1.0, 0.0));
  float c = hash2(i + vec2(0.0, 1.0));
  float d = hash2(i + vec2(1.0, 1.0));
  return mix(a, b, f.x) + (c - a) * f.y * (1.0 - f.x) + (d - b) * f.x * f.y;
}

float fbm(vec2 p) {
  float v = 0.0;
  float s = 0.5;
  for (int i = 0; i < ${OCTAVES}; i++) {
    v += vnoise(p) * s;
    p *= 2.0;
    s *= 0.5;
  }
  return v;
}

/**
 * 3D value noise, and fbm on top of it.
 *
 * This exists because a 2D equirectangular texture CANNOT texture a sphere seen face
 * on. Every longitude meets at the centre of the visible disc, so any lat/long mapping
 * has a pole there and the cells converge into a visible pinch -- which is exactly what
 * appeared once the projection was fixed and stopped being one-dimensional.
 *
 * Sampling the noise on the SURFACE POINT is seamless by construction: there is no
 * seam to wrap and no pole to converge at, because the star's own geometry supplies
 * the parameterisation. The reference avoids this the same way, by evaluating a
 * pre-rendered surface MAP -- but a map is a lat/long image too, so it has the pole as
 * well; it simply happens to sit at the back of the sphere there rather than in the
 * middle of the visible face.
 *
 * Same hash as everything else in this file, so the star breaks down into the same
 * grain as its own corona.
 */
float hash3(vec3 p) {
  return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float vnoise3(vec3 p) {
  vec3 i = floor(p);
  vec3 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  float n000 = hash3(i);
  float n100 = hash3(i + vec3(1.0, 0.0, 0.0));
  float n010 = hash3(i + vec3(0.0, 1.0, 0.0));
  float n110 = hash3(i + vec3(1.0, 1.0, 0.0));
  float n001 = hash3(i + vec3(0.0, 0.0, 1.0));
  float n101 = hash3(i + vec3(1.0, 0.0, 1.0));
  float n011 = hash3(i + vec3(0.0, 1.0, 1.0));
  float n111 = hash3(i + vec3(1.0, 1.0, 1.0));
  float x00 = mix(n000, n100, f.x);
  float x10 = mix(n010, n110, f.x);
  float x01 = mix(n001, n101, f.x);
  float x11 = mix(n011, n111, f.x);
  return mix(mix(x00, x10, f.y), mix(x01, x11, f.y), f.z);
}

float fbm3(vec3 p) {
  float v = 0.0;
  float s = 0.5;
  for (int i = 0; i < ${OCTAVES}; i++) {
    v += vnoise3(p) * s;
    p *= 2.0;
    s *= 0.5;
  }
  return v;
}

/**
 * Three bands, unrolled.
 *
 * GLSL ES 1.00 permits only CONSTANT index expressions into a uniform array, so
 * \'palette[band]\' where band is a float is a COMPILE error rather than a runtime one.
 * \'ramp()\' in gl-ring.ts hit this and is unrolled for the same reason.
 */
vec3 surfaceBand(float band) {
  float x = clamp(band, 0.0, 0.999) * 3.0;
  if (x >= 2.0)      return mix(u_surface2, u_surface3, x - 2.0);
  else if (x >= 1.0) return mix(u_surface1, u_surface2, x - 1.0);
  return mix(u_surface0, u_surface1, x);
}

vec3 coronaBand(float band) {
  float x = clamp(band, 0.0, 0.999);
  return mix(u_corona0, u_corona1, x);
}

void main() {
  vec2 uv = v_uv;
  vec2 p = (uv - 0.5) * 2.0;
  float r = length(p);

  float bodyR = u_bodyRadius;
  vec3 surface = vec3(0.0);
  float onBody = step(r, bodyR);

  if (onBody > 0.5) {
    // Analytic sphere normal. The reference gets this from a per-pixel sphere
    // intersection; the difference is recorded in the file header.
    float z = sqrt(max(0.0, 1.0 - (r * r) / (bodyR * bodyR)));
    vec3 n = vec3(p / bodyR, z);

    // Equirectangular, and SCROLLED. The scroll rate is the activity: a star with
    // more activity turns faster, and it is the one parameter that separates a still
    // disc from something alive.
    // THE PROJECTION, and the first one was degenerate.
    //
    // It read atan(n.z, n.x) -- the viewer-axis component against x. For a sphere
    // FACING the camera that viewer component is large and nearly constant across the
    // whole disc, so the longitude was pinned near one value and the granulation was
    // effectively ONE-DIMENSIONAL. A one-dimensional texture sampled at high frequency
    // and then cut into four hard bands is exactly salt-and-pepper, which is what the
    // surface looked like no matter what I did to the frequency or the ramp width.
    //
    // For a front-facing sphere the longitude comes from the SCREEN-SPACE direction and
    // the latitude from the viewer axis, which is the standard mapping:
    //
    //     longitude = atan2(screen up, screen right)
    //     latitude  = asin(viewer axis)
    // The granulation scrolls by ROTATING THE SAMPLE POINT about the star's axis,
    // rather than by offsetting a texture coordinate. A rotated 3D point on a sphere
    // stays on the sphere, so the cells turn with the surface and there is still no
    // seam and no pole. Offsetting a uv would slide the pattern across the sphere
    // instead, which shears it at the wrap.
    float spin = u_phase * (1.0 + floor(u_activity * 2.999)) * 0.35;
    float cs = cos(spin);
    float sn = sin(spin);
    vec3 sp = vec3(n.x * cs - n.y * sn, n.x * sn + n.y * cs, n.z);

    // Three channels from one fbm at different scales, which is what gives granulation
    // its cells AND gives the spots somewhere to live without a second texture.
    // FREQUENCY, and the first attempt was a planet rather than a star.
    //
    // 26 cells around the sphere is CONTINENTS. Granulation is convective cells and
    // there are hundreds of them; at 26 this produced brown continents on a cream
    // ball with a purple rim, which is a rocky planet and nothing else. 150 across is
    // the smallest count that still reads as cells rather than as flat tone.
    // 96, not 150, and the reason is ALIASING rather than taste.
    //
    // Granulation cells want to be eight to fifteen pixels across on screen. At 150
    // cells around the equator of a 460px star each cell is about six pixels, which is
    // the same scale as the 4x4 dither -- so the ordered dither and the cells beat
    // against each other and the surface reads as speckle instead of as convection.
    // 34 cells across the DIAMETER. The number is the cell count on the visible face,
    // not around an equator, because there is no equator here -- the frequency is on
    // the unit sphere and the visible face is half of it.
    // SINGLE OCTAVE, and this is the actual cause of the speckle.
    //
    // Four things were blamed for it and three were real but incidental: the noise
    // frequency, the width of the smoothstep, the projection, and the surface dither.
    // The cause is that this was an fbm.
    //
    // fbm3 at four octaves multiplies the frequency by two each octave, so a base of
    // 34 has a top octave at 272. On a 420px disc that is roughly one cycle every one
    // and a half pixels -- which is not granulation, it is per-pixel noise, and cutting
    // it into four hard bands turns it into salt and pepper. Every "fix" applied on top
    // was treating the SYMPTOM, and lowering the base frequency just moved the
    // speckle to a different octave.
    //
    // Granulation is convective cells and cells are BAND-LIMITED: one characteristic
    // size, not a fractal with eight times the detail on top. The reference's surface
    // map is drawn as flat regions, which is the same statement. One octave of value
    // noise, and the second channel likewise.
    float gran = vnoise3(sp * 30.0 + u_seed);
    float fine = vnoise3(sp * 62.0 + u_seed + 31.0);
    float spotN = vnoise3(sp * 4.0 + u_seed + 77.0);

    float g = 0.5;
    if (u_granulation > 0.001) {
      g = 0.5 + (gran - 0.5) / u_granulation;
    }
    // Transcribed: the reference reads these channels off a pre-rendered surface map.
    // Here they are three fbm evaluations, which is the same information at the same
    // cost rather than a texture fetch, and the banding below is unchanged.
    vec3 map = vec3(gran, spotN, fine);

    float energy = mix(0.5, clamp(g, 0.0, 1.0), 0.35 + u_granulation * 0.65);
    // The transition is much WIDER than the reference's 0.24..0.76, and that is the
    // single change that stops the speckle. A steep smoothstep across four hard bands
    // means most pixels sit near a band EDGE, so a small noise excursion flips them
    // into the neighbouring colour and you get salt and pepper instead of convection.
    // Spreading the ramp out gives each band a region rather than a boundary.
    float identity = smoothstep(0.10, 0.94, energy + map.b * 0.05);

    // SPOTS, and the transcription made every pixel a spot.
    //
    // map.g is an fbm centred on 0.5, so smoothstep(0.08, 0.55, map.g) is about 0.93
    // for almost the whole surface -- the subtraction was a near-constant offset, it
    // compressed the entire index distribution into the two END bands, and the two
    // middle colours were never drawn at all. Hence two-tone banding.
    //
    // The fix is the distribution, not the threshold: the reference's map.g is a
    // SPOTNESS channel that is near zero almost everywhere and high in a few places,
    // because it was rendered as one. So the noise is squared and biased down, which
    // is what makes it sparse, and the subtraction is then gated on it being sparse.
    float spotness = pow(clamp(map.g, 0.0, 1.0), 3.0);
    identity -= smoothstep(0.18, 0.62, spotness) * 0.6 * u_spots;
    identity = clamp(identity, 0.0, 0.999);

    // Limb darkening, applied to the INDEX rather than to a colour, because the
    // reference gets it for free: energy falls toward the edge, so the band index
    // falls with it. Written explicitly because with four bands and a hard quantizer
    // it is otherwise too subtle to see, and a star with a uniformly hot face reads as
    // a disc rather than as a sphere.
    // 0.42 was far too aggressive an exponent for a 4-band quantizer: it drove the
    // outermost band hard enough that the limb read as a dark outline rather than as a
    // cooler edge, which is the opposite of what limb darkening is for.
    float limb = pow(clamp(z, 0.0, 1.0), 0.22);
    identity *= 0.72 + 0.28 * limb;

    // One assignment, and the index arithmetic is the reference's: pick a band INDEX
    // out of 'colors', then spread those indices across the four surface bands. With
    // the default 4 colours the two coincide and this is the identity.
    float index = floor(clamp(identity, 0.0, 0.999) * u_colors);
    surface = surfaceBand(index * (3.0 / max(1.0, u_colors - 1.0)));

    // NO DITHER ON THE SURFACE, and removing it is what finally stopped the speckle.
    //
    // Three separate things were blamed for the salt-and-pepper and two of them were
    // real but minor: the granulation frequency, and the width of the smoothstep. The
    // cause was the dither itself.
    //
    // Dithering works by trading a hard edge for a pattern -- which only reads as
    // smoother when the two colours either side of the edge are CLOSE. Here they are
    // not: the bands are white against orange, which is most of the palette's range.
    // So the dither was not softening the band boundary, it was filling it with
    // high-contrast noise, and no amount of lowering its amplitude helped because even
    // a small perturbation of two far-apart flat colours is visible as two flat colours
    // alternating.
    //
    // The reference only ever dithers the CORONA, and its corona's two bands are
    // adjacent in the ramp. Its surfaces are FLAT regions with hard edges -- which is
    // what the screenshots show, and what the cell-shaded look actually is. I had added
    // surface dithering as a "deviation" to stop four bands reading as four rings, and
    // it was the deviation causing the artefact.
    gl_FragColor = vec4(surface, 1.0);
    return;
  }

  // ---------------------------------------------------------------- corona and flares
  float radial = r / bodyR;
  float animation = u_phase * TAU * (1.0 + floor(u_activity * 2.999));

  // The harmonics are functions of a DIRECTION, and that matters.
  //
  // The reference computes the corona's warp from a normal: it takes the centred
  // screen position, normalises it into the plane, and rotates it by the camera. So
  // the two components are the direction's, bounded in -1..1, and they vary smoothly
  // all the way out to the corner of the frame.
  //
  // The first attempt here passed 'radial' into those harmonics instead. radial grows
  // to about 3 at the corners of the canvas, so the corona's edge term was being
  // driven far outside its intended range and the corona flooded the entire frame --
  // which looked like a threshold problem and was a substitution of one quantity for
  // another. The direction is what belongs here.
  vec2 dir = p / max(r, 1e-4);

  // The corona's warp, transcribed: four harmonics of the screen-space normal, so the
  // edge is ragged rather than a clean falloff. Four is not decoration -- one harmonic
  // is a circle.
  // Transcribed term for term from coronaColor, with normal.x -> dir.x and normal.y ->
  // dir.y. Four harmonics, and the count is not decoration: one is a circle.
  float warp = sin(dir.x * 3.0 - dir.y * 5.0 + animation * 0.7) * 0.2
             + sin(dir.x * 7.0 + dir.y * 2.0 - animation * 1.3) * 0.1;
  float harmonics = sin(dir.x * 2.0 + dir.y * 2.0 + warp + animation)
                  + sin(dir.x * 4.0 - dir.y * 3.0 + warp * 2.0 - animation * 2.0)
                  + sin(dir.x * 7.0 + dir.y * 6.0 - warp * 3.0 + animation * 3.0)
                  + sin(dir.x * 11.0 + dir.y * 9.0 - warp * 3.0 + animation * 4.0);
  // The BASE is down and the HARMONIC amplitude is up, which is what makes the edge
  // ragged rather than merely soft. At 0.2 + harmonics*0.03 the constant term was half
  // the total, so the corona read as a smooth glow with a wobble in it.
  float width = u_corona * (0.11 + harmonics * 0.05) * 0.9;
  float density = clamp(1.0 - (radial - 1.0) / max(0.002, width), 0.0, 1.0);
  float coronaCoverage = pow(density, 0.7) * min(1.0, u_corona * 1.7);

  // ------------------------------------------------------------------- prominence loops
  //
  // Transcribed from flareLoop. A loop is anchored on the sphere by a HASH rather than
  // placed: angle, depth, and therefore radius all come from hash(seed + order), so 32
  // of them cost three hashes each and no state. Then it is drawn in a local frame
  // built from the anchor's OUTWARD direction and the tangent to it, as an ellipse arc
  // with two independent wobbles and a hashed gap.
  //
  // The two wobbles are what stop it reading as an ellipse. One bends the arc along its
  // length; the other displaces it perpendicular, and they run at different rates and
  // different phases, so the loop wobbles rather than undulates.
  float flare = 0.0;
  float strength = smoothstep(0.15, 0.35, u_flares);
  for (int index = 0; index < 32; index++) {
    float order = float(index);
    // Threshold: higher orders need more flares to appear at all, so raising the
    // parameter adds loops at the rim rather than making all 32 bigger.
    // Higher orders need a higher setting, so raising 'flares' adds loops rather than
    // inflating the existing ones. Divided by 40 rather than 20 because at 20 the first
    // order's threshold already exceeded the default 'flares' and it was being skipped
    // -- the largest loop, the best-anchored one, gone.
    float threshold = order / 40.0;
    if (strength <= smoothstep(threshold, threshold + 0.2, u_flares)) continue;

    float seed = u_seed + order;
    float anchorAngle = hash(seed) * TAU + animation * 0.2;
    float anchorDepth = hash(seed + 1.0) * 2.0 - 1.0;
    float anchorRadius = sqrt(max(0.0, 1.0 - anchorDepth * anchorDepth));

    vec2 anchor = vec2(cos(anchorAngle) * anchorRadius, sin(anchorAngle) * anchorRadius) * bodyR;
    // Front hemisphere only. The reference gets this from the anchor's z facing the
    // camera; projecting to 2D drops z, so this substitutes a facing test on the
    // anchor's own direction, which is the same condition.
    float facing = smoothstep(-0.15, 0.35, anchorDepth * -1.0 + 0.35);
    if (facing <= 0.001) continue;

    vec2 outward = normalize(anchor + vec2(1e-5, 0.0));
    vec2 tangent = vec2(-outward.y, outward.x);
    vec2 local = vec2(dot(p, outward), dot(p, tangent));

    float height = (0.05 + u_flares * 0.2) * bodyR;
    float widthL = height * 0.7;
    float loopCenter = 0.9 * bodyR;

    float arcPosition = local.y / widthL;
    float firstWobble = sin(arcPosition * 3.0 + animation * 2.0 + seed);
    local.x += firstWobble * height * 0.2;
    float secondWobble = sin((local.x - loopCenter) / height * 5.0 - animation * 3.0 + seed);
    local.y += secondWobble * widthL * 0.15;

    float ellipse = length(vec2((local.x - loopCenter) / height, local.y / widthL));
    // Thickness. The first version multiplied by bodyRadius AND used a width already
    // scaled by it, which put the tube at 0.004 -- about a pixel at 460px -- so all 32
    // loops rendered and none of them were visible. A prominence tube is a few per
    // cent of the star's radius, which is what 0.05 of 'height' is.
    // CLAMPED, and unclamped this flooded the entire canvas.
    //
    // (0.4 + firstWobble - secondWobble) is a sum of two sines in [-1,1] around 0.4,
    // so it goes NEGATIVE for part of every loop's life. The reference then adds an
    // ABSOLUTE 0.08 to it for the second smoothstep edge, so a slightly negative
    // thickness still leaves edge0 < edge1 and smoothstep behaves.
    //
    // Scaling the second edge by the thickness instead -- which is what tightening the
    // falloff tempted -- makes both edges negative together, and smoothstep with
    // edge0 > edge1 is UNDEFINED in GLSL. It returned 1, so line was 1 across the whole
    // frame, so the corona's coverage was ~0.5 everywhere and the dither drew a
    // half-tone field over the entire canvas.
    //
    // The symptom pointed at the corona's harmonics and at the radial/direction
    // substitution above, and both of those were wrong for other reasons and worth
    // fixing anyway. This one was arithmetic in the dark.
    float thickness = max(0.004, (0.4 + (firstWobble - secondWobble)) * height * 0.055);
    float line = 1.0 - smoothstep(thickness, thickness + thickness * 2.2, abs(ellipse - 1.0));

    // The gap. Without it this is an ellipse, which is a hoop on the sky rather than
    // a prominence anchored at one end.
    float centre = hash(seed + 2.0) * TAU;
    float arcWidth = (hash(seed + 3.0) - hash(seed + 4.0)) * TAU;
    float gap = abs(atan(sin(atan(local.y / widthL, (local.x - loopCenter) / height) - centre),
                         cos(atan(local.y / widthL, (local.x - loopCenter) / height) - centre)));
    float arcCoverage = 1.0 - smoothstep(abs(arcWidth), abs(arcWidth) + 0.4, gap);

    flare = max(flare, line * facing * strength * arcCoverage);
  }

  float coverage = clamp(max(coronaCoverage, flare), 0.0, 1.0);
  if (coverage <= 0.0) discard;
  if (bayer4(gl_FragCoord.xy) >= coverage) discard;

  // The band index is INVERTED against brightness, and that was a real fault rather
  // than a taste call: brightness peaks at the corona's inner edge, so indexing by it
  // put the dimmest colour where the corona is thickest. The inner edge should be the
  // hot one and the falloff should walk outward down the palette.
  // THE LOOPS GET THEIR OWN COLOUR, and this is what makes them visible.
  //
  // They were being drawn, and then banded into the CORONA palette alongside the
  // corona itself -- and since the corona is brightest at exactly the radius the loops
  // stand at, a loop and the corona behind it landed on the same index and there was
  // nothing to see. All 32 were rendering the entire time.
  //
  // A prominence is at or above photosphere temperature: it is denser and hotter than
  // the corona it stands in, which is the whole reason it is visible against the sky at
  // all. So a loop takes the star's hottest band and the corona never competes.
  if (flare > 0.02) {
    gl_FragColor = vec4(mix(u_surface1, u_surface0, clamp(flare * 1.6, 0.0, 1.0)), 1.0);
    return;
  }

  float brightness = max(density, flare);
  float band = min(u_coronaColors - 1.0, floor(clamp(1.0 - brightness, 0.0, 0.999) * u_coronaColors));
  gl_FragColor = vec4(coronaBand(band), 1.0);
}`;

function rgb(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
}

export function glStarSupported(): boolean {
  if (typeof document === undefined) return false;
  try {
    const c = document.createElement("canvas");
    return !!(c.getContext("webgl2") ?? c.getContext("webgl") ?? c.getContext("experimental-webgl"));
  } catch {
    return false;
  }
}

/**
 * The reference's own defaults, from `generators/star.lua`.
 *
 * `colors` default 4, and the corona palette is built with 2 stops -- `{ star = colors,
 * corona = 2 }` -- which is why there are exactly two corona bands and four surface ones
 * rather than a palette of eight.
 */
export const STAR_DEFAULTS = {
  colors: 4,
  coronaColors: 2,
  activity: 0.4,
  granulation: 0.5,
  spots: 0.3,
  corona: 0.5,
  // 0.5, not the reference's 0.3: at 0.3 the loops are drawn but sit inside the
  // corona's own radius and lose against it, so the star reads as having no
  // prominences at all. The parameter is a slider; this is where it has to sit for
  // them to be visible at the settings a chart would actually pick.
  flares: 0.5,
} as const;

export function glStar(opts: StarOpts): StarGL | null {
  if (typeof document === undefined) return null;
  const canvas = document.createElement("canvas");
  let gl: WebGLRenderingContext | null = null;
  const attrs: WebGLContextAttributes = { preserveDrawingBuffer: true, alpha: true, antialias: false };
  try {
    gl =
      (canvas.getContext("webgl2", attrs) as WebGL2RenderingContext | null) ??
      (canvas.getContext("webgl", attrs) as WebGLRenderingContext | null) ??
      (canvas.getContext("experimental-webgl", attrs) as WebGLRenderingContext | null);
  } catch {
    return null;
  }
  if (!gl) return null;

  const compile = (type: number, src: string): WebGLShader | null => {
    const sh = gl!.createShader(type);
    if (!sh) return null;
    gl!.shaderSource(sh, src);
    gl!.compileShader(sh);
    if (!gl!.getShaderParameter(sh, gl!.COMPILE_STATUS)) {
      console.warn("[GalaxyMap] star shader failed", gl!.getShaderInfoLog(sh));
      gl!.deleteShader(sh);
      return null;
    }
    return sh;
  };

  const vs = compile(gl.VERTEX_SHADER, VERT);
  const fs = compile(gl.FRAGMENT_SHADER, FRAG);
  if (!vs || !fs) return null;

  const prog = gl.createProgram();
  if (!prog) return null;
  gl.attachShader(prog, vs);
  gl.attachShader(prog, fs);
  gl.linkProgram(prog);
  if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
    console.warn("[GalaxyMap] star link failed", gl.getProgramInfoLog(prog));
    return null;
  }
  gl.useProgram(prog);

  const buf = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
  const aPos = gl.getAttribLocation(prog, "a_pos");
  gl.enableVertexAttribArray(aPos);
  gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);

  const u = (n: string) => gl!.getUniformLocation(prog, n);
  const d = STAR_DEFAULTS;
  gl.uniform1f(u("u_seed"), 1 + ((opts.seed * 0.6180339887) % 1) * 9);
  gl.uniform1f(u("u_colors"), opts.colors ?? d.colors);
  gl.uniform1f(u("u_coronaColors"), opts.coronaColors ?? d.coronaColors);
  gl.uniform1f(u("u_activity"), opts.activity ?? d.activity);
  gl.uniform1f(u("u_granulation"), opts.granulation ?? d.granulation);
  gl.uniform1f(u("u_spots"), opts.spots ?? d.spots);
  gl.uniform1f(u("u_corona"), opts.corona ?? d.corona);
  gl.uniform1f(u("u_flares"), opts.flares ?? d.flares);
  // The reference's BodyRadius, in BodyScreenRadius units. A star is a disc that
  // fills much less of its frame than a planet does, because the corona needs room.
  gl.uniform1f(u("u_bodyRadius"), 0.46);
  const set3 = (n: string, hex: string) => gl!.uniform3f(u(n), ...rgb(hex));
  set3("u_surface0", SURFACE_BANDS[0]);
  set3("u_surface1", SURFACE_BANDS[1]);
  set3("u_surface2", SURFACE_BANDS[2]);
  set3("u_surface3", SURFACE_BANDS[3]);
  set3("u_corona0", CORONA_BANDS[0]);
  set3("u_corona1", CORONA_BANDS[1]);

  const dpr = () => window.devicePixelRatio || 1;
  let raf = 0;
  let disposed = false;

  const resize = () => {
    const px = Math.max(1, Math.round(opts.canvasPx * dpr()));
    if (canvas.width !== px) {
      canvas.width = px;
      canvas.height = px;
    }
    canvas.style.width = `${opts.canvasPx}px`;
    canvas.style.height = `${opts.canvasPx}px`;
    gl!.viewport(0, 0, px, px);
    gl!.uniform2f(u("u_resolution"), px, px);
  };
  resize();

  const t0 = performance.now();
  const uTime = u("u_time");
  const uPhase = u("u_phase");
  const frame = (now: number) => {
    if (disposed) return;
    resize();
    const s = (now - t0) / 1000;
    gl!.uniform1f(uTime, s);
    gl!.uniform1f(uPhase, s);
    gl!.drawArrays(gl!.TRIANGLES, 0, 3);
    if (opts.animate !== false) raf = requestAnimationFrame(frame);
  };
  frame(t0);

  return {
    canvas,
    dispose() {
      disposed = true;
      if (raf) cancelAnimationFrame(raf);
      gl = null;
    },
  };
}