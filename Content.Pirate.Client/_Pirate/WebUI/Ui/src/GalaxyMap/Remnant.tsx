/**
 * What a star leaves behind.
 *
 * Four kinds, one family, and the family is the point:
 *
 *     star ──> dwarf        a core that stopped collapsing, still cooling
 *          └─> remnant      the explosion, and the shell it leaves
 *              └─> pulsar   a collapsed core, spinning, beams out of its poles
 *                  └─> blackhole   collapsed past the point of no return
 *
 * Every one of these is an observed object, and that was the test for adding any of
 * them. It is why there is no white hole here: a white hole solves the equations of
 * general relativity and nothing has ever been observed that requires one, which
 * makes it a different kind of thing to put on a map that otherwise shows real
 * objects in real positions.
 *
 * ## Why the pulsar is canvas 2D and not a shader
 *
 * Because it does not need one. A pulsar is a hard point with two soft beams, and
 * that is gradients — which canvas draws with a handful of `createRadialGradient`
 * calls and WebGL would need a whole program for. The black hole and the ring are
 * shaders because they are *carved by noise*, which is a different problem: the
 * shader is not there for speed, it is there because the reference's shapes cannot be
 * drawn any other way without losing the divisions in them.
 *
 * The one thing here that IS animated is the beam sweep, and it is the only animation
 * on this chart. That is deliberate and it is the pulsar's actual signature — a
 * pulsar is distinguished from every other kind of neutron star by the fact that its
 * emission is beamed and rotating. Draw it as a static dot and it is just a small
 * bright thing, which is what a white dwarf is.
 */

import { createEffect, createSignal, onCleanup, onMount, Show } from "solid-js";
import { JET, glJet } from "./lib/gl-jet";
import BlackHole from "./BlackHole";

export type RemnantKind = "pulsar" | "quasar";

/**
 * Palette.
 *
 * Cold, and deliberately almost monochrome. A pulsar's emission is synchrotron
 * radiation and it is blue-white; the temptation is to make it violet or cyan for
 * character and the result reads as a magic item rather than the most violent
 * steady signal in the sky. These are the only colours, and there are three of them:
 * a white core, a cold blue body, and the beam's own blue at low alpha.
 */
const CORE = "#ffffff";
const BODY = "#cfe2ff";
const BEAM = "rgba(150, 196, 255, 0.5)";
const JET_HOT = "#eaf4ff";
const JET_COOL = "rgba(120, 170, 255, 0.28)";

export interface PulsarProps {
  /** Width of the whole thing in CSS pixels, beams included. */
  px: number;
  seed: number;
  /** False for `prefers-reduced-motion`: a static beam, no rAF. */
  animate?: boolean;
}

/**
 * A pulsar: a hard point with two opposed beams.
 *
 * The beams are cones that narrow toward the poles and fade with distance, which is
 * the shape synchrotron emission actually makes. They are drawn additively, so
 * where two overlap the middle is brighter — and that is what sells the rotation,
 * because the brightness is a function of the angle between the beams and the
 * viewer rather than of the rotation alone.
 */
export function Pulsar(props: PulsarProps) {
  let canvas!: HTMLCanvasElement;

  onMount(() => {
    const dpr = () => window.devicePixelRatio || 1;
    const reduced =
      props.animate === false ||
      (typeof matchMedia !== "undefined" && matchMedia("(prefers-reduced-motion: reduce)").matches);

    // Beams sweep once every `period` seconds. Long, because a pulsar's period is
    // milliseconds to seconds in reality and anything fast on a chart reads as a
    // glitch rather than as rotation.
    const period = 9000;
    const t0 = performance.now();
    let raf = 0;
    let dead = false;

    const draw = (now: number) => {
      if (dead) return;
      const d = Math.max(1, Math.round(props.px * dpr()));
      if (canvas.width !== d) {
        canvas.width = d;
        canvas.height = d;
      }
      const g = canvas.getContext("2d");
      if (!g) return;
      g.setTransform(1, 0, 0, 1, 0, 0);
      g.clearRect(0, 0, d, d);

      const cx = d / 2;
      const cy = d / 2;
      // Beams run past the edge of the box: a pulsar's beams are effectively
      // unbounded, and clipping them at the sprite's edge puts a hard stop across
      // the one part of it that should have no edges.
      const reach = d * 0.62;
      // The core is deliberately tiny relative to the box. A neutron star is a
      // city-sized object that would fit inside this pixel; the whole point is that
      // it is very small and very bright, so the halo does the work of making it
      // findable and the core stays hard.
      const coreR = Math.max(1.2, d * 0.018);

      const spin = reduced ? 0.6 : ((now - t0) / period) * Math.PI * 2;

      g.globalCompositeOperation = "lighter";

      // Two beams, 180 degrees apart.
      for (let i = 0; i < 2; i++) {
        const a = spin + i * Math.PI;
        g.save();
        g.translate(cx, cy);
        g.rotate(a);
        // Cone: wide at the core, narrowing as it leaves. A parallel-sided beam
        // reads as a laser sight.
        // The cone is a hard polygon and its EDGES show, which reads as a folded
        // paper bowtie rather than as light. Clipped to a soft-edged sprite first,
        // the gradient inside it has no edge left to give away.
        const grad = g.createRadialGradient(0, 0, coreR, 0, 0, reach);
        grad.addColorStop(0, BEAM);
        grad.addColorStop(0.35, "rgba(150, 196, 255, 0.22)");
        grad.addColorStop(1, "rgba(150, 196, 255, 0)");
        // Narrower at the tip than the cone below, so the gradient does the
        // falloff and the polygon only supplies the direction.
        g.fillStyle = grad;
        g.beginPath();
        g.moveTo(0, -coreR * 0.6);
        g.quadraticCurveTo(-reach * 0.2, -reach * 0.5, 0, -reach);
        g.quadraticCurveTo(reach * 0.2, -reach * 0.5, 0, -coreR * 0.6);
        g.closePath();
        g.fill();
        g.restore();
      }

      // Halo, then the core on top of it. Two radial gradients rather than one,
      // because a single soft blob has no hard edge and the hard edge is the entire
      // visual signature.
      const halo = g.createRadialGradient(cx, cy, 0, cx, cy, d * 0.2);
      halo.addColorStop(0, "rgba(207, 226, 255, 0.55)");
      halo.addColorStop(0.4, "rgba(160, 200, 255, 0.16)");
      halo.addColorStop(1, "rgba(160, 200, 255, 0)");
      g.fillStyle = halo;
      g.beginPath();
      g.arc(cx, cy, d * 0.2, 0, Math.PI * 2);
      g.fill();

      g.globalCompositeOperation = "source-over";
      g.fillStyle = BODY;
      g.beginPath();
      g.arc(cx, cy, coreR * 1.9, 0, Math.PI * 2);
      g.fill();
      g.fillStyle = CORE;
      g.beginPath();
      g.arc(cx, cy, coreR, 0, Math.PI * 2);
      g.fill();
    };

    draw(t0);
    if (!reduced) {
      const loop = (now: number) => {
        draw(now);
        if (!dead) raf = requestAnimationFrame(loop);
      };
      raf = requestAnimationFrame(loop);
    }
    onCleanup(() => {
      dead = true;
      if (raf) cancelAnimationFrame(raf);
    });
  });

  return (
    <canvas
      ref={canvas}
      class="pulsar"
      style={{ width: `${props.px}px`, height: `${props.px}px` }}
      aria-hidden="true"
    />
  );
}

/**
 * A quasar: the black hole we already have, plus jets.
 *
 * This is the cheapest of the four and the most striking, because the expensive part
 * -- the noise-carved accretion disc and the photon ring -- is already built and
 * already correct. The jets are two soft cones along the disc's axis, which is the
 * axis the disc's own geometry already implies.
 *
 * They are drawn IN FRONT of the disc, which is correct rather than convenient: a
 * jet is optically thin, so you see it over the disc, and the near jet crossing the
 * disc's face is what gives the object depth in a way the disc alone cannot.
 */
/**
 * The jets, on their own canvas, mounted the way BlackHole mounts its.
 *
 * The SVG below is the FALLBACK and stays in the DOM underneath, for the same three
 * reasons the black hole's still is: no blank panel while the shader compiles, no flash
 * if there is no context, and a correct picture on its own if the canvas never paints.
 * It is also what makes the negative control in check-dom.mjs possible — the shader can
 * be suppressed and the SVG judged on its own.
 */
function Jets(props: { px: number; seed: number; reducedMotion: boolean }) {
  const [painted, setPainted] = createSignal(false);
  let host: HTMLDivElement | undefined;

  createEffect(() => {
    const inst = glJet({
      canvasPx: props.px,
      seed: props.seed,
      // Half a cycle between the poles' knots, as the SVG had. The shader draws both
      // poles in one pass, so this is a single offset rather than two mirrored ones
      // that could drift apart.
      phase: props.seed * 0.37,
      animate: !props.reducedMotion,
    });
    if (!inst) return;
    setPainted(false);
    host?.appendChild(inst.canvas);
    const grace = requestAnimationFrame(() => setPainted(true));
    onCleanup(() => {
      cancelAnimationFrame(grace);
      inst.dispose();
      inst.canvas.remove();
    });
  });

  return (
    <>
      <svg
        class="quasar-jets"
        width={props.px}
        height={props.px}
        viewBox={`${-props.px / 2} ${-props.px / 2} ${props.px} ${props.px}`}
        aria-hidden="true"
        style={{ visibility: painted() ? "hidden" : "visible" }}
      >
        <defs>
          {/* The fallback: standing plumes, travelling knots, soft flanks. Plainer
              than the shader and deliberately so -- it is the picture that is correct
              when there is no WebGL context, not a second attempt at the shader. */}
          {fallbackDefs(props.seed, props.px)}
        </defs>
        <g transform="rotate(-18)">
          <path d={jetPath(1, props.px, 1)} fill={`url(#plume-up-${props.seed})`} mask={`url(#beam-${props.seed})`} />
          <path d={jetPath(1, props.px, FLARE)} fill={`url(#knot-up-${props.seed})`} mask={`url(#flare-${props.seed})`} />
          <path d={jetPath(-1, props.px, 1)} fill={`url(#plume-down-${props.seed})`} mask={`url(#beam-${props.seed})`} />
          <path d={jetPath(-1, props.px, FLARE)} fill={`url(#knot-down-${props.seed})`} mask={`url(#flare-${props.seed})`} />
        </g>
      </svg>
      <div class="world-gl quasar-jets-gl" ref={(el) => (host = el)} hidden={!painted()} />
    </>
  );
}

export function Quasar(props: { px: number; seed: number; reducedMotion?: boolean }) {
  return (
    <div class="quasar" style={{ width: `${props.px}px`, height: `${props.px}px` }}>
      {/* The disc turns faster than the black hole's own. The obvious reason is that
          a quasar is not a stellar black hole. The real one is perceptual: the disc
          is thin and mostly dark, so a slow rotation changes about one per cent of
          the panel's pixels however long you wait, and the eye reads one per cent
          over six seconds as nothing happening. */}
      <BlackHole px={props.px} seed={props.seed} frames={0} period={2.4} />
      <Jets px={props.px} seed={props.seed} reducedMotion={props.reducedMotion ?? false} />
    </div>
  );
}

/** Beam proportions, shared by the shader and the SVG fallback so they cannot drift. */
const BEAM_BASE = JET.base;
const BEAM_TIP = JET.tip;
const FLARE = 1.9;

/** The polygon the shader replaces, and the fallback's beam. */
function jetPath(dir: 1 | -1, px: number, k: number) {
  const r = px * 0.46;
  const apex = dir * px * 0.08;
  const tip = dir * r;
  const w = (t: 0 | 1) => (t === 0 ? px * BEAM_BASE : r * BEAM_TIP) * k;
  return `M ${-w(0)} ${apex} L ${-w(1)} ${tip} L ${w(1)} ${tip} L ${w(0)} ${apex} Z`;
}

/**
 * The fallback's soft edge, and its knots.
 *
 * These are here because a fallback that is VISIBLY WORSE than the thing it replaces is
 * a regression for anyone without WebGL, not a graceful degradation. So the SVG keeps
 * the two properties that were measured as mattering: soft flanks rather than hard
 * polygon sides, and travelling knots rather than a static striped beam.
 *
 * It does not keep the shader's interior structure, the wandering axis or the taper,
 * and it is not trying to. It is the picture that is correct when there is no context.
 */
const fallbackDefs = (seed: number, px: number) => {
  const r = px * 0.46;
  const bell = (id: string, half: number) => (
    <linearGradient id={`edge-grad-${id}`} gradientUnits="userSpaceOnUse" x1={-half} y1="0" x2={half} y2="0">
      <stop offset="0%" stop-color="#000000" />
      <stop offset="28%" stop-color="#555555" />
      <stop offset="50%" stop-color="#ffffff" />
      <stop offset="72%" stop-color="#555555" />
      <stop offset="100%" stop-color="#000000" />
    </linearGradient>
  );
  const knotStops = (id: string) => (
    <>
      <stop offset="0%" stop-color={JET_HOT} stop-opacity="0" />
      <stop offset="42%" stop-color="#ffffff" stop-opacity="0.95" />
      <stop offset="58%" stop-color={JET_HOT} stop-opacity="0.55" />
      <stop offset="100%" stop-color={JET_HOT} stop-opacity="0" />
    </>
  );
  // Hot at the pole, fading to the tip, and STATIC. An animated plume is what let one
  // pole vanish for a whole frame: driving the plume's brightness from a travelling
  // gradient means the plume only exists where the gradient is.
  const plumeStops = (
    <>
      <stop offset="0%" stop-color="#ffffff" stop-opacity="0.95" />
      <stop offset="14%" stop-color={JET_HOT} stop-opacity="0.7" />
      <stop offset="52%" stop-color={JET_COOL} stop-opacity="0.3" />
      <stop offset="100%" stop-color={JET_COOL} stop-opacity="0.04" />
    </>
  );
  return (
    <>
      <linearGradient id={`plume-up-${seed}`} x1="0" y1="1" x2="0" y2="0">
        {plumeStops}
      </linearGradient>
      <linearGradient id={`plume-down-${seed}`} x1="0" y1="0" x2="0" y2="1">
        {plumeStops}
      </linearGradient>
      {/* The knots travel, pole to tip and off the end, half a cycle apart. The values
          run past 0..1 on purpose: a band that has left the element contributes nothing
          and the plume behind it is still there, which is the separation that stops a
          jet blinking. */}
      <linearGradient id={`knot-up-${seed}`} x1="0" y1="1" x2="0" y2="0">
        <animate attributeName="y1" values="1;0.02;-1" dur="1.6s" repeatCount="indefinite" />
        <animate attributeName="y2" values="2;1.02;0" dur="1.6s" repeatCount="indefinite" />
        {knotStops(`knot-up-${seed}`)}
      </linearGradient>
      <linearGradient id={`knot-down-${seed}`} x1="0" y1="1" x2="0" y2="0">
        <animate attributeName="y1" values="0;0.98;1.98" dur="1.6s" begin="-0.8s" repeatCount="indefinite" />
        <animate attributeName="y2" values="1;1.98;2.98" dur="1.6s" begin="-0.8s" repeatCount="indefinite" />
        {knotStops(`knot-down-${seed}`)}
      </linearGradient>
      {/* Two masks, one per layer, and NOT two per pole: the edge is symmetric about
          the axis and the poles are mirrors, so one serves both. Two rather than one
          because each is sized to its own layer's half-width -- a shared edge in
          bounding-box units would soften the narrow beam in proportion to the flare. */}
      {bell(`beam-${seed}`, r * JET.tip)}
      {bell(`flare-${seed}`, r * JET.tip * FLARE)}
      <mask id={`beam-${seed}`} maskUnits="userSpaceOnUse" x={-r * JET.tip * 1.3} y={-px} width={r * JET.tip * 2.6} height={px * 2}>
        <rect x={-r * JET.tip * 1.3} y={-px} width={r * JET.tip * 2.6} height={px * 2} fill={`url(#edge-grad-beam-${seed})`} />
      </mask>
      <mask id={`flare-${seed}`} maskUnits="userSpaceOnUse" x={-r * JET.tip * FLARE * 1.3} y={-px} width={r * JET.tip * FLARE * 2.6} height={px * 2}>
        <rect x={-r * JET.tip * FLARE * 1.3} y={-px} width={r * JET.tip * FLARE * 2.6} height={px * 2} fill={`url(#edge-grad-flare-${seed})`} />
      </mask>
    </>
  );
};

/** Dispatch for the overlay: whichever body this kind is. */
export default function Remnant(props: { kind: RemnantKind; px: number; seed: number }) {
  return (
    <Show when={props.kind === "quasar"} fallback={<Pulsar px={props.px} seed={props.seed} />}>
      <Quasar px={props.px} seed={props.seed} />
    </Show>
  );
}
