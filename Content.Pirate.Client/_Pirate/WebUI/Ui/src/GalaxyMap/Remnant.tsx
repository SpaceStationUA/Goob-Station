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

import { onCleanup, onMount, Show } from "solid-js";
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
export function Quasar(props: { px: number; seed: number }) {
  const reach = () => props.px * 0.46;

  /** One period for both knots, so the pair never drifts into looking accidental. */
  const KNOT_PERIOD = 1.6;

  /**
   * The plume itself: a STANDING structure, hot at the pole and fading to the tip,
   * with no animation at all.
   *
   * Every animated attempt at this failed the same way, and the reason is worth
   * writing down because it took three rounds. Driving the plume's whole brightness
   * from a travelling gradient means the plume only exists where the gradient is:
   * the highlight is somewhere, the rest of the jet is not. Mirrored across the two
   * poles — which it must be, since the two bounding boxes are mirrored — that puts
   * one highlight at each pole's base at the same instant, so the pair reads as
   * ONE jet with the other missing. Half-cycle phase offsets were tried and measured:
   * they changed nothing, because the mirrored traversal plus a shared stop list
   * already puts the two a half cycle apart whether or not `begin` says so. Removing
   * the offset entirely still gave correlation -0.77, which is why a negative
   * correlation was never evidence of the offset working.
   *
   * So the plume is standing and always lit, and only the KNOT travelling through it
   * moves. A quasar is a continuous jet with pulses in it, not a jet that blinks.
   *
   * The two poles' numbers are mirrored, which is the whole reason there are two
   * gradients rather than one: the upper path's apex is its bbox's bottom and the
   * lower path's apex is its bbox's top.
   */
  const plume = (id: string, y1: string, y2: string) => (
    <linearGradient id={id} x1="0" y1={y1} x2="0" y2={y2}>
      <stop offset="0%" stop-color="#ffffff" stop-opacity="0.95" />
      <stop offset="14%" stop-color={JET_HOT} stop-opacity="0.7" />
      <stop offset="52%" stop-color={JET_COOL} stop-opacity="0.3" />
      <stop offset="100%" stop-color={JET_COOL} stop-opacity="0.04" />
    </linearGradient>
  );

  /**
   * The travelling knot: a narrow bright band moving from the pole out past the tip.
   *
   * Its own stops are transparent-hot-transparent, so WHERE the gradient is does not
   * decide whether the jet is visible — that is the plume's job now. That separation
   * is the fix, and it is also why the animation values can leave 0..1 without the
   * jet going dark: a band that has left the element contributes nothing, and the
   * plume underneath is still there.
   */
  const knot = (id: string, y1: string, y2: string, phase: string) => (
    <linearGradient id={id} x1="0" y1="1" x2="0" y2="0">
      <animate attributeName="y1" values={y1} dur={`${KNOT_PERIOD}s`} begin={phase} repeatCount="indefinite" />
      <animate attributeName="y2" values={y2} dur={`${KNOT_PERIOD}s`} begin={phase} repeatCount="indefinite" />
      <stop offset="0%" stop-color={JET_HOT} stop-opacity="0" />
      <stop offset="42%" stop-color="#ffffff" stop-opacity="0.9" />
      <stop offset="58%" stop-color={JET_HOT} stop-opacity="0.5" />
      <stop offset="100%" stop-color={JET_HOT} stop-opacity="0" />
    </linearGradient>
  );

  /**
   * The plume, drawn as two stacked paths of the SAME shape.
   *
   * `dir` is +1 for the upper pole and -1 for the lower. The apex is always at the
   * pole and the tip always away from it, so the geometry is one expression and only
   * the fills differ — four gradients in total, two per pole, and every one of them
   * mirrored relative to its partner.
   */
  const jet = (dir: 1 | -1) => {
    const r = reach();
    const apex = dir * props.px * 0.08;
    const tip = dir * r;
    const w = r * 0.1;
    const d = `M 0 ${apex} L ${-w} ${tip} L ${w} ${tip} Z`;
    const p = dir === 1 ? "up" : "down";
    return (
      <>
        <path d={d} fill={`url(#plume-${p}-${props.seed})`} />
        <path d={d} fill={`url(#knot-${p}-${props.seed})`} />
      </>
    );
  };

  return (
    <div class="quasar" style={{ width: `${props.px}px`, height: `${props.px}px` }}>
      {/* The disc turns faster than the black hole's own. The obvious reason is that
          a quasar is not a stellar black hole. The real one is perceptual: the disc
          is thin and mostly dark, so a slow rotation changes about one per cent of
          the panel's pixels however long you wait, and the eye reads one per cent
          over six seconds as nothing happening. */}
      <BlackHole px={props.px} seed={props.seed} frames={0} period={2.4} />
      <svg
        class="quasar-jets"
        width={props.px}
        height={props.px}
        viewBox={`${-props.px / 2} ${-props.px / 2} ${props.px} ${props.px}`}
        aria-hidden="true"
      >
        <defs>
          {/* Standing plumes. Upper apex is its bbox's bottom, so hot-at-pole reads
              y1 = 1, y2 = 0; the lower's apex is its bbox's top, so the numbers
              mirror to y1 = 0, y2 = 1. */}
          {plume(`plume-up-${props.seed}`, "1", "0")}
          {plume(`plume-down-${props.seed}`, "0", "1")}
          {/* Knots, pole to tip and off the end. Same period, half a cycle apart, so
              the two are never at the same radius. `begin` is negative, which starts
              an animation mid-cycle — SMIL allows it, and unlike the previous attempt
              it is now decorative rather than load-bearing, because the plume behind
              it no longer depends on the knot being on screen. */}
          {knot(`knot-up-${props.seed}`, "1;0.02;-1", "2;1.02;0", "0s")}
          {knot(`knot-down-${props.seed}`, "0;0.98;1.98", "1;1.98;2.98", `-${KNOT_PERIOD / 2}s`)}
        </defs>
        <g transform="rotate(-18)">
          {jet(1)}
          {jet(-1)}
        </g>
      </svg>
    </div>
  );
}

/** Dispatch for the overlay: whichever body this kind is. */
export default function Remnant(props: { kind: RemnantKind; px: number; seed: number }) {
  return (
    <Show when={props.kind === "quasar"} fallback={<Pulsar px={props.px} seed={props.seed} />}>
      <Quasar px={props.px} seed={props.seed} />
    </Show>
  );
}
