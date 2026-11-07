/**
 * A ring system, live on a canvas.
 *
 * ## This is a transcription
 *
 * The source is vendored next to this file, in `../reference/Ring.gdshader` with
 * the `GasPlanetLayers.tscn` that carries its uniform values, from Deep-Fold's
 * PixelPlanets (MIT).
 *
 * The previous ring here was SVG geometry — two ellipse halves, three flat ribbons
 * each — designed rather than transcribed. It looked like a wire hoop laid across
 * the planet, and the reason is visible in the shader: **the reference's ring is
 * mostly noise.** `ring *= fbm(uv_center * size)` with four octaves, and then the
 * alpha is `step(0.28, ring)`, so the noise does not tint the ring, it CARVES it.
 * The divisions in a real ring are where the noise fell below the cut. Three flat
 * ribbons have no such thing, which is why they read as a hoop.
 *
 * Three more things the transcription settles that the geometry version could not:
 *
 *  - **The planet's hole belongs to the ring.** The shader cuts it in its own upper
 *    half, sized in its own uv, so the occlusion is exact and does not depend on
 *    the planet sprite being opaque across its disc. The HTML version had that by
 *    accident, via the sprite's alpha and a z-index, and it broke the first time
 *    the two layers were not both positioned.
 *  - **The ring is lit from outside the canvas.** `light_origin` is `(-0.1, 0.3)`,
 *    off the left edge, and `posterized` adds `pow(light_d, 2) * 2`, so there is a
 *    gradient across the ring from lit to shadowed. A ring with one fill has no
 *    light on it and reads as cut paper.
 *  - **`ring_perspective` is 6.0**, not the declared 4.0. See `ring-consts.ts`.
 *
 * ## What is not the reference's
 *
 * The size. The reference's ring reaches 3.16 planet radii, which around a 200px
 * planet is 632px across, and the overlay panel is 268px. `holeScaleFor` scales the
 * canvas to fit and the planet's hole with it, so every ring here is a little less
 * grand than the reference's. That is a real deviation and it is deliberate.
 *
 * The chart's markers keep the SVG path (`WorldRing.tsx`) — at 16px there is no
 * interior for four octaves of noise to show in, and the chart is pure SVG.
 */

import {
  ALPHA_CUT,
  COLORS,
  DARK,
  LIGHT_ORIGIN,
  NOISE_SIZE,
  OCTAVES,
  PERSPECTIVE,
  PIXELS,
  RING_OUTER,
  RING_WIDTH,
  TIME_SPEED,
  holeScaleFor,
} from "./ring-consts";

const VERT = `
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  // Y IS FLIPPED, and the whole shader depends on it.
  //
  // Godot's UV has (0,0) at the TOP left. WebGL's has (0,0) at the BOTTOM left. So
  // a straight port puts the reference's \`if (uv.y < 0.5)\` on the wrong half, and
  // that line is the planet's hole: the reference uses it to cut the planet out of
  // the ring's FAR arm. Flipped, it cut the far arm out of the NEAR side instead,
  // which deleted the near arm exactly where it should cross in front of the planet
  // and left the far arm drawn across the planet's face.
  //
  // It looked almost right, which is why it survived a screenshot. The planet sprite
  // paints over the ring anyway, so the far arm's mistake was hidden — and what was
  // actually visible was the near arm's absence: the ring stopped dead at the
  // planet's edge on both sides instead of passing round it.
  //
  // Flipping once, here, puts everything downstream into Godot's convention: the
  // hole test, \`light_origin\` at y=0.3, and the sense of \`rotation\`, which is
  // clockwise on screen in a y-down space and counter-clockwise in a y-up one.
  v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`;

const FRAG = `
precision highp float;
varying vec2 v_uv;

uniform float u_pixels;
uniform float u_rotation;
uniform vec2  u_lightOrigin;
uniform float u_ringWidth;
uniform float u_perspective;
uniform float u_holeScale;
uniform float u_size;
uniform float u_seed;
uniform float u_time;
uniform float u_timeSpeed;
uniform vec3  u_c0;
uniform vec3  u_c1;
uniform vec3  u_c2;
uniform vec3  u_d0;
uniform vec3  u_d1;
uniform vec3  u_d2;
uniform int   u_nColors;

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

float fbm(vec2 coord) {
  float value = 0.0;
  float scale = 0.5;
  for (int i = 0; i < ${OCTAVES}; i++) {
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
  // Pixelise, exactly as the reference does, and BEFORE the light distance is
  // taken — light_d is computed from the quantised uv. v_uv arrives y-flipped from
  // the vertex shader, so everything below is in Godot's y-down space.
  vec2 uv = floor(v_uv * u_pixels) / u_pixels;

  float light_d = distance(uv, u_lightOrigin);
  uv = rotate(uv, u_rotation);

  vec2 uv_center = uv - vec2(0.0, 0.5);

  // Tilt. This is the ring's foreshortening and it is what stops it reading as a
  // hoop laid on the disc.
  uv_center *= vec2(1.0, u_perspective);
  float center_d = distance(uv_center, vec2(0.5, 0.0));

  // Two circles of different sizes; only the intersection is kept. So this is a
  // FILLED annulus, and the noise below is what turns it into bands.
  float ring = smoothstep(0.5 - u_ringWidth * 2.0, 0.5 - u_ringWidth, center_d);
  // Reversed edges, as in the reference: clamp((0.4 + w - center_d) / w).
  ring *= smoothstep(center_d - u_ringWidth, center_d, 0.4);

  // The planet's hole, cut by the ring rather than by the sprite behind it.
  if (uv.y < 0.5) {
    ring *= step(1.0 / u_holeScale, distance(uv, vec2(0.5)));
  }

  // The material turns independently of the ring's own shape.
  uv_center = rotate(uv_center + vec2(0.0, 0.5), u_time * u_timeSpeed);
  ring *= fbm(uv_center * u_size);

  // Six tones in two ramps, not one ramp: the reference switches to its
  // dark_colors outright once posterized passes 1.0, rather than carrying on
  // through colors.
  //
  // (Backticks cannot appear in this comment: they terminate the template literal
  // the shader lives in.)
  float posterized = floor((ring + pow(light_d, 2.0) * 2.0) * 4.0) / 4.0;
  posterized = min(posterized, 2.0);
  vec3 col;
  if (posterized <= 1.0) {
    float f = posterized * float(u_nColors - 1);
    col = f < 0.5 ? u_c0 : (f < 1.5 ? u_c1 : u_c2);
  } else {
    float f = (posterized - 1.0) * float(u_nColors - 1);
    col = f < 0.5 ? u_d0 : (f < 1.5 ? u_d1 : u_d2);
  }

  // A STEP, and the fbm decides where it lands — which is what puts divisions in
  // the ring instead of a clean edge.
  if (ring < ${ALPHA_CUT}) discard;
  gl_FragColor = vec4(col, 1.0);
}`;

export function glRingSupported(): boolean {
  if (typeof document === "undefined") return false;
  try {
    const c = document.createElement("canvas");
    return !!(
      c.getContext("webgl2") ||
      c.getContext("webgl") ||
      c.getContext("experimental-webgl")
    );
  } catch {
    return false;
  }
}

export interface RingGL {
  canvas: HTMLCanvasElement;
  dispose(): void;
}

export interface RingGLOpts {
  seed: number;
  /** Planet diameter in CSS pixels. The ring is sized relative to its radius. */
  planetPx: number;
  /** Ring canvas in CSS pixels. Defaults to `planetPx * CANVAS_TO_PLANET`. */
  canvasPx?: number;
  /** False for `prefers-reduced-motion`. */
  animate?: boolean;
}

/**
 * The ring's rotation for a system, in radians.
 *
 * Exported because the DOM reports it and the checks assert against it, and two
 * copies of a seeded expression is two copies to drift. The shape constants vary
 * with the system only where the reference's own GUI varies them.
 */
export function rotationFor(seed: number): number {
  const fract = (x: number) => x - Math.floor(x);
  return 0.7 + (fract(seed * 0.2718281) - 0.5) * 0.6;
}

function rgb(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
}

/**
 * The ring's outer radius in planet radii, for a given canvas size.
 *
 * Exported because the SVG path needs the same number to agree with this one, and
 * two renderers that each guess their own radius is how the chart and the overlay
 * ended up showing different objects for the same system.
 */
export function outerRadii(planetPx: number, canvasPx: number) {
  const planetR = planetPx / 2;
  const canvasR = canvasPx / 2;
  return {
    planetR,
    rx: RING_OUTER * canvasR,
    /** The hole the ring leaves for the planet, in canvas px. */
    holeR: planetR,
  };
}

/** A live ring. Returns null if WebGL or the shader is unavailable. */
export function glRing(opts: RingGLOpts): RingGL | null {
  if (typeof document === "undefined") return null;
  const canvasPx = opts.canvasPx ?? opts.planetPx * 3;
  const planetR = opts.planetPx / 2;

  const canvas = document.createElement("canvas");
  let gl: WebGLRenderingContext | null = null;
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
    // A silent compile failure shows as an invisible ring, which is the case the
    // SVG fallback exists for, so it has to be a hard failure here.
    if (!gl!.getShaderParameter(sh, gl!.COMPILE_STATUS)) {
      console.warn("[GalaxyMap] ring shader failed", gl!.getShaderInfoLog(sh));
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
    console.warn("[GalaxyMap] ring link failed", gl.getProgramInfoLog(prog));
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
  const fract = (x: number) => x - Math.floor(x);

  // The scene's shape constants, verbatim. Only the seed varies per system, which is
  // what the reference's own GUI varies.
  gl.uniform1f(u("u_pixels"), PIXELS);
  gl.uniform1f(u("u_rotation"), rotationFor(opts.seed));
  gl.uniform2f(u("u_lightOrigin"), LIGHT_ORIGIN[0], LIGHT_ORIGIN[1]);
  gl.uniform1f(u("u_ringWidth"), RING_WIDTH);
  gl.uniform1f(u("u_perspective"), PERSPECTIVE);
  gl.uniform1f(u("u_holeScale"), holeScaleFor(canvasPx, planetR));
  gl.uniform1f(u("u_size"), NOISE_SIZE);
  gl.uniform1f(u("u_seed"), 1 + fract(opts.seed * 0.6180339887) * 9);
  gl.uniform1f(u("u_timeSpeed"), TIME_SPEED);
  gl.uniform1i(u("u_nColors"), COLORS.length);
  COLORS.forEach((c, i) => gl!.uniform3fv(u(`u_c${i}`), rgb(c)));
  DARK.forEach((c, i) => gl!.uniform3fv(u(`u_d${i}`), rgb(c)));

  const dpr = () => window.devicePixelRatio || 1;
  let raf = 0;
  let disposed = false;

  const resize = () => {
    const d = Math.max(1, Math.round(canvasPx * dpr()));
    if (canvas.width !== d) {
      canvas.width = d;
      canvas.height = d;
    }
    canvas.style.width = `${canvasPx}px`;
    canvas.style.height = `${canvasPx}px`;
    gl!.viewport(0, 0, d, d);
  };
  resize();

  const t0 = performance.now();
  const uTime = u("u_time");
  const frame = (now: number) => {
    if (disposed) return;
    resize();
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