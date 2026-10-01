/**
 * A live WebGL renderer for the system overlay's black hole.
 *
 * ## This is a transcription, not a description
 *
 * The source is vendored next to this file, in `../reference/`:
 * `BlackHoleRing.gdshader`, `BlackHole.gdshader` and the `BlackHole.tscn` that
 * carries the uniform values, from Deep-Fold's PixelPlanets (MIT).
 *
 * That last file is the one that matters and the one I did not read for a very
 * long time. The shaders declare DEFAULTS, and the scene OVERRIDES most of them:
 *
 *     ring_perspective   declared 4.0    scene sets 14.0
 *     disk_width         declared 0.1    scene sets 0.065
 *     size               declared 50.0   scene sets 6.598
 *     OCTAVES            no default      scene sets 3
 *     radius             declared 0.5    scene sets 0.247
 *     light_width        declared 0.05   scene sets 0.028
 *     rotation           declared 0.0    scene sets 0.766
 *
 * Reading the declarations instead of the scene is why this looked wrong for so
 * long. `ring_perspective` alone: at the declared 4.0 the disc is foreshortened
 * four to one and reads as a fat ellipse; at the scene's 14.0 it is fourteen to
 * one and reads as the thin sweep it is actually meant to be. Every attempt to
 * fix the shape by tuning was compensating for a constant that was never right.
 *
 * ## The palette runs bright to dark, which is the opposite of what looks obvious
 *
 * `posterized = floor((disk + light_d) * 4)` indexes straight into the colour
 * array, and the scene's array starts at near-white and ends at dark red. So a
 * LOW value is bright and a high value is dark. It has to be transcribed in that
 * order or every colour decision comes out inverted, which is exactly what an
 * earlier version did.
 *
 * ## Two sprites, and the order is the whole depth cue
 *
 * `BlackHole.tscn` has two children: `BlackHole` at index 0 and `Disk` at index 1.
 * Godot draws later siblings on top, so the disc is drawn OVER the horizon. That
 * is what puts a near side in front of the singularity, and it is why the photon
 * ring is broken where the band crosses it rather than being a complete circle.
 *
 * They are also different sizes: in the scene `BlackHole` spans 100x100 and `Disk`
 * spans 300x300, concentric, so the horizon is a THIRD of the canvas. Its UV is
 * therefore the canvas UV scaled by three about the middle, which the shader does
 * itself -- and which means the scene's `radius` is ALREADY in the right units.
 * Dividing it by three as well, on the reasonable-sounding grounds that the sprite
 * is a third of the canvas, divides twice and makes the horizon three times too
 * small. It was, and it is the reason the horizon read as a barely visible dot
 * while the reference's is a prominent ringed circle.
 */

const VERT = `
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  v_uv = a_pos * 0.5 + 0.5;
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`;

const FRAG = `
precision highp float;
varying vec2 v_uv;

uniform vec2  u_res;
uniform float u_time;          // seconds, already multiplied by time_speed
uniform float u_seed;
uniform float u_rotation;
uniform float u_timeSpeed;
uniform float u_diskWidth;
uniform float u_perspective;
uniform float u_size;
uniform float u_pixels;        // disc UV quantisation
uniform float u_holePixels;
uniform float u_holeRadius;
uniform float u_holeLightWidth;
uniform vec3  u_hole0;         // the void
uniform vec3  u_hole1;         // white ring
uniform vec3  u_hole2;         // orange outer ring
uniform vec3  u_d0;            // cream
uniform vec3  u_d1;            // yellow
uniform vec3  u_d2;            // orange
uniform vec3  u_d3;            // deep orange
uniform vec3  u_d4;            // red-brown

/**
 * smoothstep(distance_to_centre, outer, inner), transcribed.
 *
 * The reference calls smoothstep with its first argument where an edge belongs, so
 * this is a call with edge0 > edge1 -- undefined per the GLSL spec, and in practice
 * clamp((x - edge0) / (edge1 - edge0)) with x = 0.2. That is
 * clamp((inner - d) / (outer - d)): it peaks at inner/outer at the centre and is
 * already ZERO by inner. It is not a plateau.
 *
 * The full smoothstep CURVE is applied, not the linear ratio. Reading it as linear
 * was one of three transcription errors that had this rendering as a rounded cigar
 * rather than a swept band.
 */
float bump(float d, float outer, float inner) {
  if (d >= inner) return 0.0;
  float den = outer - d;
  if (den <= 1e-6) return 0.0;
  float t = clamp((inner - d) / den, 0.0, 1.0);
  return t * t * (3.0 - 2.0 * t);
}

/** The reference's hash: sin-based, and tiling at 2*size by size. */
float rnd(vec2 coord) {
  vec2 m = vec2(2.0, 1.0) * floor(u_size + 0.5);
  coord = mod(coord, m);
  return fract(sin(dot(coord, vec2(12.9898, 78.233))) * 15.5453 * u_seed);
}

float vnoise(vec2 coord) {
  vec2 i = floor(coord);
  vec2 f = fract(coord);
  float a = rnd(i);
  float b = rnd(i + vec2(1.0, 0.0));
  float c = rnd(i + vec2(0.0, 1.0));
  float d = rnd(i + vec2(1.0, 1.0));
  vec2 cubic = f * f * (3.0 - 2.0 * f);
  return mix(a, b, cubic.x) + (c - a) * cubic.y * (1.0 - cubic.x) + (d - b) * cubic.x * cubic.y;
}

/** Three octaves, because the scene says three. */
float fbm(vec2 coord) {
  float value = 0.0;
  float scale = 0.5;
  for (int i = 0; i < 3; i++) {
    value += vnoise(coord) * scale;
    coord *= 2.0;
    scale *= 0.5;
  }
  return value;
}

vec2 rotate(vec2 coord, float angle) {
  coord -= 0.5;
  coord *= mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
  return coord + 0.5;
}

void main() {
  vec3 col = vec3(0.0);
  float alpha = 0.0;

  // ---- the horizon, underneath -------------------------------------------
  // BlackHole.gdshader. Its sprite is 100x100 and the disc's is 200x200, concentric,
  // so this sprite's UV is the canvas UV scaled by two about the middle.
  {
    vec2 huv = (v_uv - 0.5) * 3.0 + 0.5;
    vec2 uv = floor(huv * u_holePixels) / u_holePixels;
    float d = distance(uv, vec2(0.5));
    vec3 hc = u_hole0;
    if (d > u_holeRadius - u_holeLightWidth) hc = u_hole1;
    if (d > u_holeRadius - u_holeLightWidth * 0.5) hc = u_hole2;
    if (u_holeRadius >= d) { col = hc; alpha = 1.0; }
  }

  // ---- the disc, over the horizon ----------------------------------------
  // BlackHoleRing.gdshader, in its order, with the scene's constants.
  {
    vec2 uv = floor(v_uv * u_pixels) / u_pixels;

    // dither(UV, uv): the RAW uv as the first argument, the quantised one as the
    // second. Passing the quantised value for both is a different pattern.
    float dith = mod(v_uv.x + uv.y, 2.0 / u_pixels) <= 1.0 / u_pixels ? 1.0 : 0.0;

    uv = rotate(uv, u_rotation);
    vec2 uv2 = uv;

    // Compress x, or the disc looks stretched out.
    uv.x -= 0.5;
    uv.x *= 1.3;
    uv.x += 0.5;

    uv = rotate(uv, sin(u_time * u_timeSpeed * 2.0) * 0.01);

    vec2 l_origin = vec2(0.5);
    float d_width = u_diskWidth;

    // The warp. The distance is taken from the CURRENT uv -- after the rotation and
    // the x compression, before the y displacement. Mixing the two frames is a real
    // bug and was one.
    if (uv.y < 0.5) {
      float dd = distance(vec2(0.5), uv);
      uv.y += bump(dd, 0.5, 0.2);
      d_width += bump(dd, 0.5, 0.3);
      l_origin.y -= bump(dd, 0.5, 0.2);
    } else if (uv.y > 0.53) {
      float dd = distance(vec2(0.5), uv);
      uv.y -= bump(dd, 0.4, 0.17);
      d_width += bump(dd, 0.5, 0.2);
      l_origin.y += bump(dd, 0.5, 0.2);
    }

    float light_d =
      distance(uv2 * vec2(1.0, u_perspective), l_origin * vec2(1.0, u_perspective)) * 0.3;

    vec2 uv_center = uv - vec2(0.0, 0.5);
    uv_center *= vec2(1.0, u_perspective);
    float center_d = distance(uv_center, vec2(0.5, 0.0));

    // Two circles of different sizes; only the intersection. This describes a FILLED
    // ellipse, not a ring -- the thin band is what survives the alpha cut below, and
    // the fbm decides where that boundary falls.
    float disk = smoothstep(0.1 - d_width * 2.0, 0.5 - d_width, center_d);
    disk *= smoothstep(center_d - d_width, center_d, 0.4);

    uv_center = rotate(uv_center + vec2(0.0, 0.5), u_time * u_timeSpeed * 3.0);
    disk *= pow(fbm(uv_center * u_size), 0.5);
    if (dith > 0.5) disk *= 1.2;

    float posterized = floor((disk + light_d) * 4.0);
    posterized = min(posterized, 4.0);

    // The alpha is a STEP, not a ramp: opaque or not, with the palette chosen
    // independently. Treating it as a ramp is what turns the band into a smear.
    if (disk >= 0.15) {
      vec3 dc = u_d0;
      if (posterized >= 3.5) dc = u_d4;
      else if (posterized >= 2.5) dc = u_d3;
      else if (posterized >= 1.5) dc = u_d2;
      else if (posterized >= 0.5) dc = u_d1;
      col = dc;
      alpha = 1.0;
    }
  }

  if (alpha < 0.5) discard;
  gl_FragColor = vec4(col, 1.0);
}`;

/** `true` if this browser will give us a context at all. */
export function glSupported(): boolean {
  if (typeof document === "undefined") return false;
  try {
    const c = document.createElement("canvas");
    return !!(c.getContext("webgl2") || c.getContext("webgl") || c.getContext("experimental-webgl"));
  } catch {
    return false;
  }
}

export interface BlackHoleGL {
  canvas: HTMLCanvasElement;
  dispose(): void;
}

/** The horizon's three steps, from the scene's `colors` for BlackHole.gdshader. */
const HOLE: [string, string, string] = ["#272737", "#ffffeb", "#ed7b39"];

/**
 * The disc's five steps, from the scene's `colors` for BlackHoleRing.gdshader.
 *
 * BRIGHT to dark. `posterized` indexes straight into this, so the order is load
 * bearing and it is the opposite of the one that looks obvious.
 */
const DISC: [string, string, string, string, string] = [
  "#ffffeb",
  "#fff540",
  "#ffb84a",
  "#ed7b39",
  "#bd4035",
];

function rgb(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
}

export interface BlackHoleGLOpts {
  seed: number;
  /** Sprite width in CSS pixels. */
  px: number;
  /** Seconds for one turn of the disc's texture. */
  period?: number;
  /** False for `prefers-reduced-motion`: one frame, no rAF. */
  animate?: boolean;
}

/**
 * A live black hole. Returns `null` if WebGL or the shader is unavailable, and the
 * caller falls back to the baked strip.
 *
 * The values below are the scene's, not the declarations'. They are named so it is
 * obvious where each came from, because getting this wrong is what made this look
 * wrong for an embarrassingly long time.
 */
export function blackHoleGL(opts: BlackHoleGLOpts): BlackHoleGL | null {
  if (typeof document === "undefined") return null;
  const canvas = document.createElement("canvas");
  let gl: WebGLRenderingContext | null = null;
  // preserveDrawingBuffer so the canvas can be read back. Without it the buffer is
  // undefined after compositing, and a renderer that cannot be inspected is one
  // whose bugs cannot be asserted on.
  const attrs: WebGLContextAttributes = {
    preserveDrawingBuffer: true,
    alpha: true,
    antialias: false,
  };
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
    // A silent compile failure shows as a blank panel, which is the case the
    // fallback exists for, so this has to be a hard failure here.
    if (!gl!.getShaderParameter(sh, gl!.COMPILE_STATUS)) {
      console.warn("[GalaxyMap] shader failed to compile", gl!.getShaderInfoLog(sh));
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
    console.warn("[GalaxyMap] program link failed", gl.getProgramInfoLog(prog));
    return null;
  }
  gl.useProgram(prog);

  const buf = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
  const aPos = gl.getAttribLocation(prog, "a_pos");
  gl.enableVertexAttribArray(aPos);
  gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);

  const u = (name: string) => gl!.getUniformLocation(prog, name);
  const fract = (x: number) => x - Math.floor(x);

  // Per-system variation, the way the reference's GUI varies its own. The shape
  // constants stay at the scene's values: those are the look, and varying them is
  // how the disc ended up a different object from the reference's.
  gl.uniform1f(u("u_seed"), 1 + fract(opts.seed * 0.6180339887) * 9);
  gl.uniform1f(u("u_rotation"), 0.766 + (fract(opts.seed * 0.2718281) - 0.5) * 0.5);
  gl.uniform1f(u("u_timeSpeed"), 0.2);
  gl.uniform1f(u("u_diskWidth"), 0.065);
  gl.uniform1f(u("u_perspective"), 14.0);
  gl.uniform1f(u("u_size"), 6.598);
  gl.uniform1f(u("u_pixels"), 300);
  gl.uniform1f(u("u_holePixels"), 100);
  // The scene's radius, UNCHANGED.
  //
  // It is tempting to divide these by three, because the horizon sprite is a third
  // of the disc's -- and doing so makes the horizon three times too small, which is
  // what it was. The shader already converts: it measures the distance in huv, which
  // IS the horizon sprite's own uv, scaled up from the canvas. So the scene's 0.247
  // is already in the right units and dividing it as well divides twice.
  gl.uniform1f(u("u_holeRadius"), 0.247);
  gl.uniform1f(u("u_holeLightWidth"), 0.028);
  gl.uniform3fv(u("u_hole0"), rgb(HOLE[0]));
  gl.uniform3fv(u("u_hole1"), rgb(HOLE[1]));
  gl.uniform3fv(u("u_hole2"), rgb(HOLE[2]));
  DISC.forEach((c, i) => gl!.uniform3fv(u(`u_d${i}`), rgb(c)));

  const dpr = () => window.devicePixelRatio || 1;
  let raf = 0;
  let disposed = false;

  const resize = () => {
    const d = Math.max(1, Math.round(opts.px * dpr()));
    if (canvas.width !== d) {
      canvas.width = d;
      canvas.height = d;
    }
    canvas.style.width = `${opts.px}px`;
    canvas.style.height = `${opts.px}px`;
    gl!.viewport(0, 0, d, d);
    gl!.uniform2f(u("u_res"), d, d);
  };
  resize();

  const period = Math.max(0.5, opts.period ?? 6);
  const t0 = performance.now();
  const uTime = u("u_time");
  const frame = (now: number) => {
    if (disposed) return;
    resize();
    // Live, so there is no strip to close: the texture may turn at the reference's
    // own 0.6 turns a second indefinitely.
    gl!.uniform1f(uTime, (now - t0) / 1000);
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
