/**
 * A quasar's bipolar jets, live on a canvas.
 *
 * ## Why this is a shader and not SVG
 *
 * It was SVG, and it looked cheap next to the accretion disc — visibly cheaper, which
 * is the tell. The reason is the rule this project already wrote down: the ring and the
 * disc are WebGL because **their shapes are carved by noise**, and the pulsar is canvas
 * 2D because it is gradients and nothing else. The jet was SVG gradients on a polygon,
 * so nothing in it was carved by anything. A gradient-filled trapezoid has no interior,
 * so there is nothing in it to look at.
 *
 * Concretely, four things a polygon cannot do and this does:
 *
 *  - **Structure along the beam.** The plasma is filamentary — it is stretched by the
 *    same acceleration that collimates it, so the texture is *anisotropic*, drawn out
 *    along the beam. Noise sampled in a frame scaled `12.0` across the beam and `1.4`
 *    along it gives exactly that, and it is why the beam has an interior.
 *  - **A wandering axis.** `axis(t)` bends the beam with low-frequency noise. A real
 *    jet precesses and wiggles; a perfectly straight one is a ruler.
 *  - **Soft ends.** The envelope reaches zero at the tip. The polygon had to be *cut*
 *    there, and a cut is a cut at any resolution.
 *  - **A shock, not a gradient band.** The travelling knot used to be a moving
 *    `linearGradient`. Here it is a gaussian in `t` that also *widens* the beam, so it
 *    is a brightening and a swelling at once.
 *
 * ## Both poles in one pass
 *
 * The two jets are drawn by the same loop over `abs(t)`, so they are antipodal by
 * construction rather than by two sets of mirrored constants that can drift apart. That
 * was the bug the SVG version had three rounds to shake: two gradients, one per pole,
 * and every one of them mirrored relative to its partner.
 *
 * ## Coordinates
 *
 * `v_uv` runs 0..1 with y **down**, matching Godot and the DOM, so `t < 0` is the
 * upper pole. The tilt is applied to the sample point rather than to the geometry, so
 * there is no transform to keep in sync with the mask.
 *
 * The noise primitives and the hash are `glsl.ts`, shared with the background, the ring
 * and `planet.ts`, so every object on this chart breaks down into the same grain.
 */

import { noisePrelude } from "./glsl";

/** Follows the reference shaders: the background uses 8, the disc 4. */
const OCTAVES = 4;

/**
 * Reach, as a fraction of the canvas's half-width, and the beam's half-width at the
 * pole and at the tip, likewise.
 *
 * These are the same proportions the SVG version settled on after measurement —
 * tip width over length 0.102 — so this is a change of technique, not of intent, and
 * the geometry check still has something true to say.
 */
export const JET = {
  /**
   * ALL of these are fractions of the panel's WIDTH, which is the unit the SVG
   * fallback used (`px * 0.46`) and therefore the unit the geometry check measures in.
   *
   * They were first written as fractions of the HALF-width, which is the unit `p` in
   * the shader is in, and the beam came out twice as long and ten times too wide --
   * reaching the panel edge with a flat white interior and no filaments, which looked
   * like a bug in the shader and was a unit error in a constant. The SVG and the
   * shader have to agree on the unit or there is no single number to check.
   */
  /** How far out the beam runs. The SVG's reach was px * 0.46. */
  reach: 0.46,
  /** Half-width at the pole. */
  base: 0.026,
  /** Half-width at the tip. Nearly parallel to the base; 1.77x would be a cone. */
  tip: 0.019,
  /** The beam's rotation, in radians. Matches the SVG's rotate(-18). */
  tilt: -0.314,
  /** Knot travel period, seconds. */
  period: 1.6,
} as const;

const VERT = `
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  // Y is flipped, for the same reason the ring's is: Godot's UV has (0,0) at the top
  // left and WebGL's at the bottom left, so a straight port puts every half-space test
  // on the wrong half. Once, here, and everything downstream is in Godot's convention.
  v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`;

// The prelude declares uniforms and so does every caller that uses it, and it does not
// declare a precision. Without this the compile fails on the prelude's own uniforms
// before it reaches a line of ours, and the log points at line 2 of a file whose
// line 2 is somebody else's declaration.
const FRAG =
  "precision highp float;\n" +
  noisePrelude(OCTAVES) +
  `
uniform float u_reach;
uniform float u_base;
uniform float u_tip;
uniform float u_tilt;
uniform float u_period;
uniform float u_phase;
uniform float u_time;

varying vec2 v_uv;

void main() {
  vec2 uv = v_uv;

  // The tilt, applied to the sample point rather than to the geometry, so there is no
  // transform to keep in sync with anything else.
  float c = cos(u_tilt);
  float s = sin(u_tilt);
  vec2 p = vec2(uv.x - 0.5, uv.y - 0.5);
  p = vec2(p.x * c - p.y * s, p.x * s + p.y * c);

  float t = p.y;
  float x = p.x;
  float at = abs(t);

  // The axis wanders. Low frequency, so the beam bends over its length instead of
  // buzzing. A straight beam is a ruler.
  float axis = (fbm(vec2(0.0, at * 3.0) + u_phase, 1.0) - 0.5) * 0.03;
  float across = x - axis;

  // The envelope along the beam: bright at the pole, gone by the tip. Declared before
  // the width because the width depends on it.
  //
  // The SVG had to CUT the polygon at the tip, and a cut is a cut at every resolution.
  // Here the tip goes to zero, and -- the part that matters -- the beam also NARROWS as
  // it fades, so it tapers to a point instead of ending in a rounded bulb. Fading only
  // the brightness of a constant-width beam gives exactly that bulb, because the
  // gaussian profile is widest at the tip and a dim wide end reads as a lozenge.
  float along = smoothstep(u_reach, u_reach * 0.18, at);
  along *= smoothstep(0.0, 0.05, at);

  // Near-parallel, and narrowing toward the tip with the envelope rather than widening
  // to it.
  float w = u_base + (u_tip - u_base) * pow(at, 0.75);
  w *= mix(0.22, 1.0, along);

  // The knot, as a fraction of the way out. Wraps, so it leaves at the tip and a new
  // one leaves the pole.
  float k = fract(at / u_reach - u_time / u_period);

  // Two gaussians per cycle: the shock itself, and a second one behind it that is
  // wider and dimmer. One travelling feature reads as a dot going round a circle; two
  // with different widths read as a wake.
  float shock = exp(-pow((k - 0.14) / 0.085, 2.0));
  float wake = 0.45 * exp(-pow((k - 0.02) / 0.20, 2.0));

  // The knot SWELLS the beam. That is what makes it a bulge and not a bright dot, and
  // it is the thing a moving gradient could not do at all.
  w *= 1.0 + 1.1 * shock + 0.4 * wake;

  // Across the beam: a bell with a flat core, not a linear ramp. This is the mask the
  // SVG version needed two luminance masks for, and here it is one expression.
  float u = abs(across) / max(w, 1e-4);
  float profile = exp(-u * u * 2.2);
  // The cutoff starts at 0.35 rather than at the edge, so the flank has somewhere to
  // fade to zero over several pixels. Cutting at the silhouette instead gives a step,
  // and a step on a thin bright shape is the single most obvious way to tell it is a
  // polygon.
  profile *= smoothstep(1.0, 0.35, u);

  // The interior. Anisotropic on purpose: 12.0 across the beam and 1.4 along it, so
  // the texture is drawn out into filaments the way plasma accelerating out of a pole
  // actually is. Sampled in the beam's OWN frame, so the structure rides the bend
  // rather than sliding across it.
  // 240 across the beam and 5 along it, so about four noise cells fit ACROSS a beam
  // that is 0.02 wide and several fit along its length. The first attempt used 12.0
  // across, which over a beam 0.02 wide samples a range of 0.24 -- less than one
  // noise cell, so the whole beam got a single flat value and the interior texture
  // that is the entire reason for using a shader was simply not there.
  float fil = fbm(vec2(across * 240.0, at * 5.0) + u_phase, 1.0);
  // Amplitude matters more than frequency here. At 0.75 the filaments were there but
  // the beam still read as milk; the contrast between a filament and the gap beside it
  // is what makes it look like plasma rather than a painted shape.
  float turb = 0.28 + 1.15 * fil * fil;
  // The base is smooth and the far end is turbulent, which is right: the shear grows
  // with distance from the source.
  turb = mix(1.0, turb, smoothstep(0.0, 0.55, at));

  float a = profile * along * turb;
  // The shock is additive on top of the beam's own brightness, so it reads as brighter
  // rather than as a different colour laid over the top.
  a += profile * along * (shock * 0.9 + wake * 0.25);
  // Optically thin. A jet you cannot see through is a painted ribbon, and this one is
  // drawn OVER the accretion disc on purpose -- so any opacity here is opacity the disc
  // loses.
  a *= 0.78;
  a = clamp(a, 0.0, 1.0);

  if (a < 0.004) discard;

  // Colour from cold blue-white at the pole through to a dim blue at the tip. The knot
  // is pushed towards white, because a shock front is the hottest thing in the beam.
  float heat = clamp(1.0 - at / u_reach, 0.0, 1.0);
  vec3 cold = vec3(0.42, 0.58, 0.86);
  vec3 mid = vec3(0.78, 0.86, 1.0);
  vec3 col = mix(cold, mid, heat);
  col = mix(col, vec3(1.0), clamp(shock * 0.8 + wake * 0.3, 0.0, 1.0));

  gl_FragColor = vec4(col, a);
}`;

export function glJetSupported(): boolean {
  if (typeof document === undefined) return false;
  try {
    const c = document.createElement("canvas");
    return !!(
      c.getContext("webgl2") ??
      c.getContext("webgl") ??
      c.getContext("experimental-webgl")
    );
  } catch {
    return false;
  }
}

export interface JetGL {
  canvas: HTMLCanvasElement;
  dispose(): void;
}

export interface JetGLOpts {
  canvasPx: number;
  seed: number;
  /** Which pole's knots lead. 0 and 1 put them half a cycle apart, as the SVG did. */
  phase?: number;
  animate?: boolean;
}

export function glJet(opts: JetGLOpts): JetGL | null {
  if (typeof document === undefined) return null;
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
    // Silent failure is an invisible jet, so it is a hard failure here and the caller
    // falls back.
    if (!gl!.getShaderParameter(sh, gl!.COMPILE_STATUS)) {
      console.warn("[GalaxyMap] jet shader failed", gl!.getShaderInfoLog(sh));
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
    console.warn("[GalaxyMap] jet link failed", gl.getProgramInfoLog(prog));
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

  gl.uniform1f(u("u_reach"), JET.reach);
  gl.uniform1f(u("u_base"), JET.base);
  gl.uniform1f(u("u_tip"), JET.tip);
  gl.uniform1f(u("u_tilt"), JET.tilt);
  gl.uniform1f(u("u_period"), JET.period);
  gl.uniform1f(u("u_phase"), opts.phase ?? 0);
  // The prelude's own uniforms. `fbm(coord, tilesize)` takes the tile size as its
  // second argument, so u_size is only here because the prelude declares it; the
  // octave count is interpolated into the source, since GLSL ES 1.00 requires a
  // constant loop bound.
  gl.uniform1f(u("u_seed"), 1 + fract(opts.seed * 0.6180339887) * 9);
  gl.uniform1f(u("u_size"), 1);
  gl.uniform1f(u("u_pixels"), opts.canvasPx);
  gl.uniform1f(u("u_time"), 0);

  const dpr = () => window.devicePixelRatio || 1;
  let raf = 0;
  let disposed = false;

  const resize = () => {
    const d = Math.max(1, Math.round(opts.canvasPx * dpr()));
    if (canvas.width !== d) {
      canvas.width = d;
      canvas.height = d;
    }
    canvas.style.width = `${opts.canvasPx}px`;
    canvas.style.height = `${opts.canvasPx}px`;
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