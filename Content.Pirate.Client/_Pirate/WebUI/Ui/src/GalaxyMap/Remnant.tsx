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
  const up = `jet-up-${props.seed}`;
  const down = `jet-down-${props.seed}`;

  /**
   * TWO gradients, one per pole, and the second one is reversed.
   *
   * The first version gave both jets a single gradient and let them share it, which
   * looks like it should work and does not. `gradientUnits` defaults to
   * objectBoundingBox, so the gradient's coordinates are relative to EACH referencing
   * element's own box — and the two boxes are mirrored, because the upper path's apex
   * is its bbox's BOTTOM and the lower path's apex is its bbox's TOP.
   *
   * So `y1 = 1, y2 = 0` means "hot at the pole" for one jet and "hot at the tip" for
   * the other. The symptom is exactly what was reported: the two jets pulse to the
   * same side, so the object reads as one thing flexing rather than two plumes
   * behaving independently.
   *
   * The fix is not to offset the animation, it is to give each pole its own ramp with
   * its own direction, and the durations differ so they are not in lockstep either.
   */
  /**
   * One pole's ramp.
   *
   * `y1` carries the hot stop and `y2` trails it by exactly one unit, so the ramp
   * keeps its length while it travels instead of stretching.
   *
   * `keyTimes` is load-bearing and was missing for two rounds. Gradient coordinates
   * are in objectBoundingBox units, so anything outside 0..1 is off the element
   * entirely — and the previous values ran to -0.9 and -1.9, which put the whole
   * ramp outside the jet for a large part of every cycle. Measured, the lower jet was
   * entirely ABSENT for four frames out of twelve and the upper barely moved: a
   * highlight that spends half its time not on the shape is a blink, not a jet.
   *
   * So 0.8 of the cycle carries the highlight from the pole to the tip, and the last
   * 0.2 carries it off the end and into the gap before the next one starts. That gap
   * is real — plasma leaves, and the next knot follows — and it is short.
   */
  const ramp = (id: string, y1: string, y2: string, dur: string) => (
    <linearGradient id={id} x1="0" y1="1" x2="0" y2="0">
      <animate
        attributeName="y1"
        values={y1}
        keyTimes="0;0.8;1"
        calcMode="linear"
        dur={dur}
        repeatCount="indefinite"
      />
      <animate
        attributeName="y2"
        values={y2}
        keyTimes="0;0.8;1"
        calcMode="linear"
        dur={dur}
        repeatCount="indefinite"
      />
      <stop offset="0%" stop-color="#ffffff" stop-opacity="1" />
      <stop offset="18%" stop-color={JET_HOT} stop-opacity="0.85" />
      <stop offset="55%" stop-color={JET_COOL} stop-opacity="0.4" />
      <stop offset="100%" stop-color={JET_COOL} stop-opacity="0" />
    </linearGradient>
  );

  /**
   * The plume itself.
   *
   * `dir` is +1 for the upper pole and -1 for the lower, and it decides which ramp
   * the path uses. The apex is always at the pole and the tip always away from it, so
   * the geometry is one expression and only the fill differs.
   */
  const jet = (dir: 1 | -1) => {
    const r = reach();
    const apex = dir * props.px * 0.08;
    const tip = dir * r;
    const w = r * 0.1;
    return (
      <path
        d={`M 0 ${apex} L ${-w} ${tip} L ${w} ${tip} Z`}
        fill={`url(#${dir === 1 ? up : down})`}
      />
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
          {/* The upper jet: hot stop from the pole (its bbox's bottom, y=1) out to
              the tip (y=0) and off the end. */}
          {ramp(up, "1;0;-1", "2;1;0", "1.35s")}
          {/* The lower jet: same travel, opposite screen direction. Its apex is its
              bbox's TOP, so the numbers are mirrored rather than reused — which is
              the whole of the bug this file's two gradients exist to fix. The
              duration differs too, so the two are never in phase even in direction. */}
          {ramp(down, "0;1;2", "1;2;3", "1.7s")}
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
