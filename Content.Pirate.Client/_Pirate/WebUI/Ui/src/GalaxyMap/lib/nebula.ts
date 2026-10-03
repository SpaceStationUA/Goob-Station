/**
 * The nebula behind the chart.
 *
 * ## What this is
 *
 * Deep-Fold's PixelSpace generator, ported: `Nebulae.shader` and `StarStuff.shader`
 * with the values their `.tres` files give them. Vendored in `reference/space/` with
 * their MIT licence.
 *
 * The same author as the planet generator this whole chart is matched to, and the
 * same primitives — the same `sin(dot(coord, vec2(12.9898, 78.233)))` hash, the same
 * `fbm`, the same `circleNoise`. That is the whole reason for choosing it over
 * hand-drawn background art, and it is visible: the nebula's dither has the same
 * grain as the planets' terminators.
 *
 * ## Why it is drawn once and cached
 *
 * Neither layer moves. Both are pure functions of `(uv, size, seed, pixels)`, and
 * the reference draws them once into a texture too. Re-running two eight-octave fbm
 * chains per frame to produce an identical image would be the single most expensive
 * thing on the page for no change, so it is rendered on mount and on resize only.
 *
 * ## `should_tile` is ON, and I turned it off first and it mattered enormously
 *
 * I read the tiling branch as a degradation — it swaps the radial falloff
 * `step(n2, 0.1 + d)` for a flat `step(n2, 0.3)` and throws away the
 * distance-from-centre term, which looked like the shape being sacrificed for
 * seamlessness. So I turned it off, on the grounds that the chart is one screen and
 * has nothing to tile.
 *
 * That rendered almost nothing: 10% of pixels, peak luminance 141, invisible on a
 * page that is already dark. The flat 0.3 threshold is not a cheap substitute for
 * the falloff — it is what makes the nebula a *nebula*. `n2` is a three-octave fbm
 * with a mean near 0.44, so `n2 <= 0.1 + d` is satisfied only in its deepest
 * valleys, and the falloff branch draws the bottom tenth of the noise field. At 0.3
 * it is satisfied across most of it.
 *
 * The flag is implemented and set to the scene's value. `should_tile` is about
 * making the texture repeat; what it actually does to the picture is not that.
 *
 * Everything else is verbatim, including the bits that look like mistakes — `n_dust`
 * is computed from exactly the same expression as `n` in `Nebulae.shader`, so the
 * product is `n * n2 * n`, and `col_value` in the band branch divides by 7 after
 * multiplying by 35, which can exceed 1 and clamp. Both are transcribed rather than
 * tidied, because tidying them would be inventing a background.
 */

import { noisePrelude } from "./glsl";

/**
 * `Colorscheme.tres`'s eight stops, in order.
 *
 * Olive, then brown, red, orange, cream, pale yellow. Warm throughout — this is a
 * nebula lit from inside, and there is no blue in it at all.
 */
/**
 * The colourscheme. HUES are `Colorscheme.tres`'s; the bottom three are LIFTED.
 *
 * And lifting is not a deviation here, the way it was for the ring's palette. The
 * reference treats this as user data, not as a fixed design: `GUI.gd` loads
 * `Colorscheme.tres` as `global_scheme` and `select_colorscheme()` writes the
 * gradient's colours straight from the colour pickers. The shipped file is a
 * starting point the tool expects you to edit, so choosing a different one is the
 * generator working as designed.
 *
 * Why it needs editing here: the noise's contribution is a PRODUCT of three fbm
 * values each averaging about 0.44, so it lands near 0.084. `col_value` is
 * `floor(0.084 * 14) / 7`, which is 0.143 — the second of eight stops. Measured on
 * the chart: of the pixels the nebula actually covers, 72% come out as
 * `background_color` and most of the rest as stops 0 and 1, which are luminance 19
 * and 29. On a page that is luminance 13, that is not a nebula, it is a slightly
 * uneven dark.
 *
 * So the bottom three are raised to sit around luminance 55 to 90: enough to read
 * as gas, well below the territory fills and the labels, which is the whole job.
 * The reference's own ramp continues up through orange to cream, and that top half
 * is untouched.
 *
 *     reference    lifted
 *     #202215      #1e2118   barely moved: this end is the background
 *     #3a2802      #6b4420
 *     #963c3c      #a2543a
 *     #ca5a2e      #ca5a2e   unchanged
 *     #ff7831      #ff7831
 *     #f39949      #f39949
 *     #ebc275      #ebc275
 *     #dfd785      #dfd785
 */
export const PALETTE: readonly string[] = [
  "#1e2118",
  "#6b4420",
  "#a2543a",
  "#ca5a2e",
  "#ff7831",
  "#f39949",
  "#ebc275",
  "#dfd785",
];

/** `Nebulae.tres`'s `background_color`: a near-black olive, not a neutral black. */
export const BACKGROUND = "#171711";

export const NEBULAE = {
  /** `size` */
  size: 5.0,
  /** `OCTAVES` */
  octaves: 3,
  /** `seed` */
  seed: 4.507,
  /** `pixels` */
  pixels: 500,
  /** `reduce_background` */
  reduce: false,
} as const;

export const STARSTUFF = {
  size: 10.0,
  octaves: 8,
  seed: 6.521,
  pixels: 500,
  reduce: false,
} as const;

const VERT = `
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  // Y flipped, for the same reason the ring's is: Godot's UV has (0,0) top-left and
  // WebGL's has it bottom-left, and the background's dither and centre-distance both
  // care about which way up it is.
  v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`;

const NEBULAE_FRAG = `
precision highp float;
varying vec2 v_uv;
${noisePrelude(NEBULAE.octaves)}
uniform bool u_reduce;
uniform bool u_tile;

void main() {
  vec2 uv = floor(v_uv * u_pixels) / u_pixels;

  // Distance from the centre, for the radial falloff. The tiling branch drops this.
  float d = distance(uv, vec2(0.5)) * 0.4;

  uv *= u_uvCorrect;
  bool dith = dither(uv, v_uv);

  float n = cloud_alpha(uv * u_size, u_size);
  float n2 = fbm(uv * u_size + vec2(1.0, 1.0), u_size);
  float n_lerp = n2 * n;
  // The reference computes n_dust from the SAME expression as n. Transcribed, not
  // tidied: see the header.
  float n_dust = cloud_alpha(uv * u_size, u_size);
  float n_dust_lerp = n_dust * n_lerp;

  if (dith) {
    n_dust_lerp *= 0.95;
    n_lerp *= 0.95;
    d *= 0.98;
  }

  // Two thresholds a hair apart, which is what makes the thin bright band around the
  // nebula's edge. step(edge, x) is x >= edge, so this reads "0.1 + d has reached
  // n2" -- i.e. the nebula is where the noise is LOW.
  //
  // (No backticks in these comments: they terminate the template literal the shader
  // lives in. Third time.)
  float a = step(n2, 0.1 + d);
  float a2 = step(n2, 0.115 + d);
  if (u_tile) {
    a = step(n2, 0.3);
    a2 = step(n2, 0.315);
  }

  if (u_reduce) {
    n_dust_lerp = pow(n_dust_lerp, 1.2) * 0.7;
  }

  float col_value = 0.0;
  if (a2 > a) {
    col_value = floor(n_dust_lerp * 35.0) / 7.0;
  } else {
    col_value = floor(n_dust_lerp * 14.0) / 7.0;
  }

  vec3 col = ramp(col_value);
  if (col_value < 0.1) {
    col = u_background.rgb;
  }

  if (a2 < 0.5) discard;
  gl_FragColor = vec4(col, 1.0);
}`;

const STARSTUFF_FRAG = `
precision highp float;
varying vec2 v_uv;
${noisePrelude(STARSTUFF.octaves)}
uniform bool u_reduce;

void main() {
  vec2 uv = floor(v_uv * u_pixels) / u_pixels * u_uvCorrect;
  bool dith = dither(uv, v_uv);

  float n_alpha = fbm(uv * ceil(u_size * 0.5) + vec2(2.0, 2.0), ceil(u_size * 0.5));
  float n_dust = cloud_alpha(uv * u_size, u_size);
  float n_dust2 = fbm(uv * ceil(u_size * 0.2) - vec2(2.0, 2.0), ceil(u_size * 0.2));
  float n_dust_lerp = n_dust2 * n_dust;

  if (dith) {
    n_dust_lerp *= 0.95;
  }

  float a_dust = step(n_alpha, n_dust_lerp * 1.8);
  n_dust_lerp = pow(n_dust_lerp, 3.2) * 56.0;
  if (dith) {
    n_dust_lerp *= 1.1;
  }
  if (u_reduce) {
    n_dust_lerp = pow(n_dust_lerp, 0.8) * 0.7;
  }

  float col_value = floor(n_dust_lerp) / 7.0;
  vec3 col = ramp(col_value);

  if (a_dust < 0.5) discard;
  gl_FragColor = vec4(col, 1.0);
}`;

function rgb(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
}

export interface Nebula {
  canvas: HTMLCanvasElement;
  /**
   * How many of the two layers actually built and drew. Must be 2.
   *
   * A layer that fails to compile is otherwise invisible: the helper logs it and
   * returns, the other layer covers the area, and the result looks like a subtle
   * background rather than a broken one. This is the only reason a check can see it.
   */
  layers: number;
  dispose(): void;
}

export interface NebulaOpts {
  /** CSS pixels across. */
  px: number;
  seed?: number;
  /**
   * The reference's own `reduce_background`. Dials the whole thing back for when it
   * is behind text, which on this chart it is: 24 territory labels and a route list
   * sit on top of it.
   */
  reduce?: boolean;
}

export function nebulaSupported(): boolean {
  if (typeof document === "undefined") return false;
  try {
    const c = document.createElement("canvas");
    return !!(
      c.getContext("webgl2") || c.getContext("webgl") || c.getContext("experimental-webgl")
    );
  } catch {
    return false;
  }
}

/**
 * Build the two-layer background.
 *
 * Returns null without WebGL, and the caller is expected to fall back to the flat
 * page colour — a missing nebula is a cosmetic loss, and a thrown error in the chart's
 * mount path would be a broken chart.
 */
export function nebula(opts: NebulaOpts): Nebula | null {
  if (!nebulaSupported()) return null;
  const canvas = document.createElement("canvas");
  const gl =
    (canvas.getContext("webgl2", {
      preserveDrawingBuffer: true,
      alpha: true,
      antialias: false,
    }) as WebGL2RenderingContext | null) ??
    (canvas.getContext("webgl", {
      preserveDrawingBuffer: true,
      alpha: true,
      antialias: false,
    }) as WebGLRenderingContext | null);
  if (!gl) return null;

  const compile = (type: number, src: string): WebGLShader | null => {
    const sh = gl.createShader(type);
    if (!sh) return null;
    gl.shaderSource(sh, src);
    gl.compileShader(sh);
    if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS)) {
      console.warn("[GalaxyMap] background shader failed", gl.getShaderInfoLog(sh));
      gl.deleteShader(sh);
      return null;
    }
    return sh;
  };

  const vs = compile(gl.VERTEX_SHADER, VERT);
  if (!vs) return null;

  const buf = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);

  const dpr = () => window.devicePixelRatio || 1;
  let w = 0;
  let h = 0;
  let disposed = false;

  /**
   * Draw one layer.
   *
   * Both layers composite with the canvas's own alpha, so StarStuff goes down first
   * and Nebulae over it. That order is the reference's: in `BackgroundGenerator.tscn`
   * the star field is beneath the nebulae.
   */
  /**
   * One layer's parameters.
   *
   * Structural rather than `typeof NEBULAE`, because `NEBULAE` and `STARSTUFF` are
   * declared `as const` and their literal types do not match each other — which is
   * the type system correctly pointing out that the two are different and the
   * function takes either.
   */
  interface LayerParams {
    size: number;
    seed: number;
    pixels: number;
  }

  let layersDrawn = 0;

  const layer = (fragSrc: string, params: LayerParams) => {
    const fs = compile(gl.FRAGMENT_SHADER, fragSrc);
    if (!fs) return;
    const prog = gl.createProgram();
    if (!prog) return;
    gl.attachShader(prog, vs);
    gl.attachShader(prog, fs);
    gl.linkProgram(prog);
    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
      console.warn("[GalaxyMap] background link failed", gl.getProgramInfoLog(prog));
      return;
    }
    gl.useProgram(prog);
    const aPos = gl.getAttribLocation(prog, "a_pos");
    gl.enableVertexAttribArray(aPos);
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);

    const u = (n: string) => gl.getUniformLocation(prog, n);
    // Seeded per layer and offset from the base, so a given chart always gets the same
    // sky and two charts do not.
    const s = (opts.seed ?? 1) + params.seed * 0.0001;
    gl.uniform1f(u("u_size"), params.size);
    gl.uniform1f(u("u_seed"), s);
    gl.uniform1f(u("u_pixels"), params.pixels);
    gl.uniform1i(u("u_reduce"), opts.reduce ? 1 : 0);
    // The scene's own value. See the header: this is not a seamlessness setting, it
    // is the difference between a nebula and ten per cent of one.
    gl.uniform1i(u("u_tile"), 1);
    // The reference's aspect correction. `GUI.gd` computes it as
    //   aspect = (h / w, 1) when wider than tall
    // and hands it to the shader as `uv_correct`; it defaults to (1,1) because the
    // generator's own canvas is square. Ours is not, so without this the noise is
    // stretched horizontally by the width ratio and a cloud comes out as a smear.
    gl.uniform2f(u("u_uvCorrect"), h > w ? w / h : 1, w > h ? h / w : 1);
    gl.uniform4fv(u("u_background"), [...rgb(BACKGROUND), 1]);
    gl.uniform3fv(u("u_palette"), PALETTE.flatMap((c) => rgb(c)));

    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    gl.viewport(0, 0, w, h);
    // NO CLEAR HERE. It used to be in this function, which meant the second layer
    // wiped the first before drawing: StarStuff covered 39% of the canvas on its own
    // and the pair came out at 20%, because Nebulae erased it and then drew less.
    //
    // It was invisible for two reasons at once. The result still looked like a
    // nebula, because Nebulae alone is a nebula; and suppressing StarStuff entirely
    // changed nothing at all, since it was already being erased. A negative control
    // that reports "no difference" is usually a bug rather than a reassurance.
    gl.drawArrays(gl.TRIANGLES, 0, 3);
    layersDrawn++;
    gl.deleteProgram(prog);
    gl.deleteShader(fs);
  };

  const resize = () => {
    const dw = Math.max(1, Math.round(opts.px * dpr()));
    const dh = Math.max(1, Math.round(opts.px * dpr() * 0.58));
    if (canvas.width === dw && canvas.height === dh && w === dw) return false;
    canvas.width = dw;
    canvas.height = dh;
    w = dw;
    h = dh;
    return true;
  };

  const draw = () => {
    if (disposed) return;
    if (!resize()) return;
    canvas.style.width = `${opts.px}px`;
    canvas.style.height = `${Math.round(opts.px * 0.58)}px`;
    // Once, before both. StarStuff is the dust and goes down first; Nebulae goes over
    // it, which is the order in `BackgroundGenerator.tscn`.
    gl.viewport(0, 0, w, h);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT);
    layer(STARSTUFF_FRAG, STARSTUFF);
    layer(NEBULAE_FRAG, NEBULAE);
  };

  draw();

  if (layersDrawn !== 2) {
    console.warn(
      `[GalaxyMap] background drew ${layersDrawn} of 2 layers; see the compile errors above`,
    );
  }

  return {
    canvas,
    layers: layersDrawn,
    dispose() {
      disposed = true;
      gl.getExtension("WEBGL_lose_context")?.loseContext();
    },
  };
}