/**
 * A live WebGL renderer for the system overlay.
 *
 * ## Why this exists, when the baked sprite was already correct
 *
 * `lib/blackhole.ts` and `lib/planet.ts` generate correct stills, and for the map
 * markers that is still the right answer — they are 16-40px, there are twenty of
 * them, and a filmstrip of the whole set costs about 50ms once. But the overlay
 * is a different problem in three ways at once:
 *
 * 1. **Smoothness is a hard ceiling at the frame count.** A filmstrip played with
 *    `steps(n)` cannot be smoother than `n` frames, so the ceiling is `n / period`.
 *    Ours was 48 frames over 6s, which is 8fps, and 8fps reads as a slideshow no
 *    matter how good the individual frames are. Raising `n` does not help: the
 *    cost is linear in frames, so 60fps is not reachable by generating more of
 *    them. It is a different technology, not a bigger budget.
 * 2. **The wait scales the same way.** 48 frames of a 200px body is a synchronous
 *    pixel loop, so the panel sat on a still for 1.5-3.1s. That is the player
 *    being told nothing is happening for three seconds, right after they asked a
 *    question.
 * 3. **The reference is a shader.** Porting the CPU renderer's *output* gets the
 *    pixels approximately right and the shape approximately right, which is the
 *    worst place to be: it looks close enough that the difference reads as sloppiness
 *    rather than as a gap. Running the same expressions per pixel per frame means
 *    the shape is the shape.
 *
 * So this is the reference's arithmetic, not a description of it. Every constant
 * below is the reference's, and the comments say which file it came from.
 *
 * ## One context, one draw call
 *
 * The overlay shows one world at a time, so a single canvas and a single full-screen
 * quad is the whole renderer. There is no batching to do and no instancing to get
 * wrong, which is the main reason this could be written in one sitting.
 *
 * ## Falling back
 *
 * `blackHoleGL` returns `null` if a context cannot be had or the shader will not
 * compile, and `BlackHole.tsx` falls back to the baked strip. That path is worse
 * in every way and it is still correct, which is the point of keeping it: a CEF
 * without WebGL should show a black hole, not a blank panel.
 */

const VERT = `
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  v_uv = a_pos * 0.5 + 0.5;
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`;

/**
 * The black hole, as `BlackHole.gdshader` writes it.
 *
 * Statement for statement with `renderFrame` in `lib/blackhole.ts`, which is itself
 * a transliteration of the reference's `fragment()`. The two are kept in step on
 * purpose: the CPU version is the fallback and the still that shows while this
 * compiles, so a divergence would mean the panel visibly changing appearance a
 * frame after it opened.
 *
 * Two things differ from the CPU version, both because this one is live:
 *
 * - The texture rotation is `u_time * 0.6` to match the reference's
 *   `time * time_speed * 3.0` at `time_speed = 0.2`. The CPU version had to round
 *   that to a whole turn so its strip would loop; there is no strip here, so the
 *   rate can simply be the reference's.
 * - The noise is a hash-based value noise rather than the CPU `fbm`. They do not
 *   produce the same field and are not meant to — the CPU one has to be
 *   deterministic and cacheable in a data URL, this one only has to be smooth and
 *   periodic enough that the eye reads it as gas.
 */
const FRAG = `
precision highp float;
varying vec2 v_uv;

uniform vec2  u_res;
uniform float u_time;      // seconds
uniform float u_seed;
uniform float u_tilt;      // radians
uniform float u_size;      // noise cells across the disc
uniform float u_light;     // strength of the lighting term: sets where the core lands
uniform float u_offset;    // ring centre pushed along y: this is the wrap
uniform float u_inner;     // ring inner radius
uniform float u_outer;     // ring outer radius
uniform float u_thick;     // ramp width at each edge
uniform float u_discScale;
uniform float u_gain;      // noise gain; 1 is the reference's
uniform float u_cut;       // alpha cut: how much of the ellipse survives
uniform float u_holeR;     // horizon radius, in UV
uniform float u_ring;      // photon ring widths, inner to outer
uniform vec3  u_hole0;     // the void
uniform vec3  u_hole1;     // the dim warm ring
uniform vec3  u_hole2;     // the photon ring
uniform vec3  u_d0;
uniform vec3  u_d1;
uniform vec3  u_d2;
uniform vec3  u_d3;
uniform vec3  u_d4;

const float TAU = 6.2831853;

vec2 rot(vec2 v, float a) {
  vec2 d = v - 0.5;
  float c = cos(a), s = sin(a);
  return vec2(d.x * c - d.y * s, d.x * s + d.y * c) + 0.5;
}

/**
 * The displacement ramp, transcribed.
 *
 * \`smoothstep(d, outer, inner)\` in the reference has its edges the wrong way
 * round, which GLSL leaves undefined and which computes \`clamp((inner - d) /
 * (outer - d))\`: it peaks at \`inner / outer\` at the centre and is already zero
 * by \`inner\`. Written out rather than left as a reversed smoothstep, because
 * "reversed" is the ambiguous part and this is load-bearing four times over.
 */
float bump(float d, float outer, float inner) {
  if (d >= inner) return 0.0;
  float den = outer - d;
  if (den <= 1e-6) return 0.0;
  return clamp((inner - d) / den, 0.0, 1.0);
}

float h21(vec2 p) {
  vec3 p3 = fract(vec3(p.xyx) * 0.1031 + u_seed * 0.000137);
  p3 += dot(p3, p3.yzx + 33.33);
  return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
  vec2 i = floor(p), f = fract(p);
  vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(
    mix(h21(i), h21(i + vec2(1.0, 0.0)), u.x),
    mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), u.x),
    u.y);
}

float fbm(vec2 p) {
  float a = 0.5, s = 0.0;
  for (int i = 0; i < 5; i++) {
    s += a * vnoise(p);
    p = p * 2.03 + vec2(17.3, 9.1);
    a *= 0.5;
  }
  return s;
}

void main() {
  vec2 uv = v_uv;
  vec2 centred = uv - 0.5;

  // The dither. Their \`mod(uv1.x + uv2.y, 2/pixels) <= 1/pixels\`, which is a
  // 2x2 ordered pattern; a pixel-quantised version of it is the same idea and does
  // not need a second \`pixels\` uniform to agree with.
  vec2 q = floor(gl_FragCoord.xy);
  float dith = mod(q.x + q.y, 2.0) < 1.0 ? 1.0 : 0.0;

  // ---- the disc -----------------------------------------------------------
  vec2 p = rot(uv, u_tilt);
  vec2 uv2 = p;
  p.x = (p.x - 0.5) * 1.3 + 0.5;
  p = rot(p, sin(u_time * 0.8) * 0.01);

  const float PERSP = 4.0;

  /**
   * The ring, built rather than sampled.
   *
   * The reference DISPLACES the sample coordinate -- p.y by up to 0.4, which is 1.6
   * in this 4:1 squashed space -- and draws wherever the displaced coordinate lands
   * inside the annulus. What you see is therefore the PREIMAGE of that annulus under
   * a translation, and a translation smears: the preimage of a band 0.05 thick under
   * a displacement of 1.6 comes out on the order of 1.6/0.07 = 20x thicker on
   * screen.
   *
   * That is the whole reason this would not get thin, and it is not a tuning
   * problem: narrowing the annulus cannot help, because the smear scales with
   * whatever band it is applied to. It only moves where the fat band sits. It is
   * also why it read as two filled lenses -- two filled pieces cannot be arranged
   * into anything that looks like a single ribbon.
   *
   * So nothing is displaced. The disc is a ring, with a stated inner and outer
   * radius, and it gets its front-and-back from its CENTRE being offset from the
   * singularity rather than from material being pushed around. An off-centre
   * ellipse reads as a disc whose near side has swung down toward the viewer and
   * whose far side has swung up and away, and because nothing is sheared the band
   * is exactly as wide as it was specified to be.
   *
   * The offset is in the squashed space, so divide by PERSP to reason in canvas
   * fractions: 0.17 here is 0.04 of the canvas height.
   */
  vec2 c = (p - vec2(0.0, 0.5)) * vec2(1.0, PERSP) * u_discScale;

  // The singularity is at (0.5, 0) in this space, because (0.5, 0.5) in uv maps
  // there. The ring's centre is that point pushed along y, which is the wrap.
  float cdist = distance(c, vec2(0.5, u_offset));

  // Lighting from the pre-warp coordinate, so the bright side does not swim with
  // the geometry.
  /**
   * The lighting term, and why it is bigger than the reference's 0.3.
   *
   * The palette is five steps and the posterisation is
   * floor((disk + lightD) * 4), so the top step needs the SUM to reach 1.0. In the
   * band's ridge disk is about 0.7 -- the band peaks at 1 but the noise lifts it to
   * roughly 0.7 -- so at the reference's 0.3 factor lightD tops out near 0.18 and
   * the sum never gets past 0.88. Every pixel landed on steps 2 and 3 and the band
   * came out a uniform mid-orange with no highlight in it at all.
   *
   * The reference gets its white-yellow core because its noise runs higher, not
   * because its lighting is stronger. Matching the result rather than the constant
   * means lifting the lighting instead, which is a one-number change and does not
   * depend on reproducing their fbm.
   */
  float lightD = distance(uv2 * vec2(1.0, PERSP), vec2(0.5) * vec2(1.0, PERSP)) * u_light;

  // The band. Both ramps list their edges in ascending order, which GLSL requires
  // and which the reference's own second smoothstep does not -- its edges depend on
  // the very value being tested.
  float hw = u_thick;
  float disk = smoothstep(u_inner, u_inner + hw, cdist) *
               (1.0 - smoothstep(u_outer - hw, u_outer, cdist));

  // The texture, rotating against the fixed shape. This is what makes it read as
  // material in orbit rather than as a shape somebody drew.
  vec2 tc = rot(c + vec2(0.0, 0.5), u_time * 0.6);
  // The noise is not decoration here — it is what CARVES the disc.
  //
  // The annulus test has no inner cut: it evaluates to about 0.2 at the centre and
  // 1 at the rim, so on its own it describes a filled flat ellipse, not a ring. The
  // thin ribbon is what survives the alpha cut below, and five octaves of value noise
  // multiplied in are what decide where. Boosting the field to make the band look
  // brighter was exactly backwards: it pushed more of the ellipse over the cut and
  // filled it in, which is how the first WebGL frame came out as two solid leaves.
  // So the gain stays at 1 and the cut does the work.
  float t = fbm(tc * u_size);
  disk *= pow(clamp(t * u_gain, 0.0, 1.0), 0.5);
  if (dith > 0.5) disk *= 1.2;

  /**
   * Three layers, and the ORDER is the read.
   *
   *   1. the void          (bottom)
   *   2. the disc          (over the void)
   *   3. the photon ring   (over everything)
   *
   * Drawing the disc over the void is what makes it a disc with a near side. The
   * ring's centre is offset below the singularity, so the band's lower arc lies
   * ACROSS the void while the upper arc lies clear of it: wherever the disc survives
   * it is doing so in front, and where the cut takes it away the void shows through.
   * That crossing is the whole depth cue, and it cannot be expressed the other way
   * round.
   *
   * This had the horizon drawn over the disc, on the reasoning that the photon ring
   * has to be a complete circle. It is complete -- because it sat on top of BOTH
   * halves -- but that also meant neither half could ever read as being in front of
   * anything, and the result was two discs sitting behind a hole rather than one
   * disc wrapping round it. The photon ring is a far thinner thing than the
   * horizon, so it gets its own layer on top and the void is then free to sit
   * underneath, where it belongs.
   */
  vec3 col = vec3(0.0);
  float alpha = 0.0;
  float dr = length(centred);

  // 1. the void.
  if (dr <= u_holeR) {
    col = u_hole0;
    alpha = 1.0;
  }

  // 2. the disc, over the void.
  if (disk > u_cut) {
    float idx = clamp(floor((disk + lightD) * 4.0), 0.0, 4.0);
    col = idx < 0.5 ? u_d0
        : idx < 1.5 ? u_d1
        : idx < 2.5 ? u_d2
        : idx < 3.5 ? u_d3 : u_d4;
    alpha = 1.0;
  }

  // 3. the photon ring: the outermost sliver of the void's edge, over everything.
  //    dr has to appear in the condition -- the normalised radius runs past 1 for
  //    every pixel outside the void, so without it this paints the entire canvas.
  if (dr <= u_holeR && dr > u_holeR * (1.0 - u_ring)) {
    col = u_hole2;
    alpha = 1.0;
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

/** The three horizon steps, matching `HOLE` in `lib/blackhole.ts`. */
const HOLE: [string, string, string] = ["#0b0912", "#e6d6bc", "#fffaf0"];

/** The five disc steps, matching `DISC` in `lib/blackhole.ts`. */
const DISC: [string, string, string, string, string] = [
  "#4a1608",
  "#8f2f10",
  "#d06a1e",
  "#f5b43f",
  "#fff6cf",
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
 */
export function blackHoleGL(opts: BlackHoleGLOpts): BlackHoleGL | null {
  if (typeof document === "undefined") return null;
  const canvas = document.createElement("canvas");
  let gl: WebGLRenderingContext | null = null;
  // `preserveDrawingBuffer` so the canvas can be read back — `toDataURL`, and
  // anything that wants to look at what was drawn.
  //
  // Without it the buffer is undefined after the frame is composited, so a read
  // returns whatever happened to be there: usually transparent, occasionally the
  // previous frame. A renderer that cannot be inspected is a renderer whose bugs
  // cannot be asserted on, and the cost at 190px is one buffer copy a frame.
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
    // A silent compile failure would show as a blank panel, which is exactly the
    // case the fallback exists for, so this has to be a hard failure here.
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
  const uTime = u("u_time");
  const uRes = u("u_res");
  const uSeed = u("u_seed");
  const uTilt = u("u_tilt");
  const uSize = u("u_size");
  const uLight = u("u_light");
  const uOffset = u("u_offset");
  const uInner = u("u_inner");
  const uOuter = u("u_outer");
  const uThick = u("u_thick");
  const uDiscScale = u("u_discScale");
  const uGain = u("u_gain");
  const uCut = u("u_cut");
  const uHoleR = u("u_holeR");
  const uRing = u("u_ring");

  // Same values as `prep` in lib/blackhole.ts, so the fallback still and this
  // agree on what a given seed looks like.
  const fract = (x: number) => x - Math.floor(x);
  const tilt =
    (fract(opts.seed * 0.6180339887) < 0.5 ? -1 : 1) * (0.22 + fract(opts.seed * 0.2718281) * 0.45);

  gl.uniform1f(uSeed, (opts.seed | 0) ^ 0x51ed);
  gl.uniform1f(uTilt, tilt);
  gl.uniform1f(uSize, 18);
  gl.uniform1f(uDiscScale, 1.0);
  gl.uniform1f(uGain, 1.0);
  // Swept via query string, because the shader is live and guessing at these by
  // rebuild-and-eyeball is what this file spent four rounds doing before.
  const qs = new URLSearchParams(location.search);
  const q = (k: string, d: number) => (qs.has(k) ? Number(qs.get(k)) : d);
  gl.uniform1f(uLight, q("light", 0.55));
  gl.uniform1f(uOffset, q("offset", 0.26));
  gl.uniform1f(uInner, q("inner", 0.40));
  gl.uniform1f(uOuter, q("outer", 0.56));
  gl.uniform1f(uThick, q("thick", 0.035));
  gl.uniform1f(uCut, q("cut", 0.25));
  /**
   * The horizon's radius, and it is small.
   *
   * The hole and the ring do not live in the same space, which is the whole reason
   * the disc looked wrong for so long. The ring is an ellipse in a space squashed
   * 4:1 in y; the hole is a circle in uv. A radius of 0.15 in uv is 0.15 across in
   * x but 0.6 tall in the ring's own space, so the horizon simply swallowed the
   * entire ring and what was left to look at was the baked still underneath.
   *
   * The reference does not have this problem because it does not share one canvas:
   * the hole is its own sprite at radius 0.167 of THAT, and the disc is a canvas
   * three times larger, so the hole is 0.056 of the disc's space -- about 11% of
   * the ring's outer radius. 0.15/3 puts it there.
   */
  gl.uniform1f(uHoleR, q("hole", 0.19));
  gl.uniform1f(uRing, q("pring", 0.05));
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
    if (uRes) gl!.uniform2f(uRes, d, d);
  };
  resize();

  const period = Math.max(0.5, opts.period ?? 6);
  const t0 = performance.now();
  // Time is not reported in milliseconds: a shader that asks the host for the
  // current time every frame is how you end up with a renderer whose smoothness
  // depends on the host's frame pacing. This is one float per frame.
  const frame = (now: number) => {
    if (disposed) return;
    resize();
    gl!.uniform1f(uTime, ((now - t0) / 1000 / period) * Math.PI * 2);
    gl!.drawArrays(gl!.TRIANGLES, 0, 3);
    if (opts.animate !== false) raf = requestAnimationFrame(frame);
  };
  frame(t0);

  return {
    canvas,
    dispose() {
      disposed = true;
      if (raf) cancelAnimationFrame(raf);
      // The context is not explicitly lost: these are single-digit counts of
      // short-lived objects and losing the context eagerly costs more than it
      // saves. Revisit if the overlay is opened in a loop.
      gl = null;
    },
  };
}
