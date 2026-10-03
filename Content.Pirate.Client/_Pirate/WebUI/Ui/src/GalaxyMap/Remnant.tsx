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
  const grad = `jet-${props.seed}`;
  /**
   * The jet's brightness travels by sliding the GRADIENT along the plume.
   *
   * Two previous attempts. Animating the plume's opacity said that something was
   * happening without showing anything happening, and discrete knots travelling
   * outward were geometrically right and looked like grey blocks -- scaling a
   * trapezoid vertically just produces a bigger trapezoid, and a screen-blended
   * off-white trapezoid on a dark background is a rectangle.
   *
   * Moving the gradient has neither problem: there is no shape involved at all, so
   * there is nothing to read as an artefact, and the motion is continuous. The
   * bright stop slides from the pole to the tip and the gradient's extent moves with
   * it, so the ramp never tears.
   *
   * SMIL rather than CSS because the thing being animated is an attribute of an
   * SVG gradient element, and CSS cannot reach that. The chart's marks use the same
   * technique for the same reason.
   */
  const slide = (dur: string) => (
    <>
      <animate
        attributeName="y1"
        values="1;0.1;-0.9"
        dur={dur}
        repeatCount="indefinite"
      />
      <animate
        attributeName="y2"
        values="2;1.1;0.1"
        dur={dur}
        repeatCount="indefinite"
      />
    </>
  );
  return (
    <div class="quasar" style={{ width: `${props.px}px`, height: `${props.px}px` }}>
      {/* The disc turns faster than the black hole's own. Two reasons, and the
          second is the real one.

          The obvious one is that a quasar is not a stellar black hole: it is
          feeding, so its disc is not the same object and there is no reason for it
          to behave identically.

          The real one is perceptual. The disc is thin and mostly dark, so a slow
          rotation changes about one per cent of the panel's pixels however long you
          wait, and the eye reads one per cent over six seconds as nothing happening.
          Halving the period roughly doubles the fraction of the structure that has
          moved between any two frames, which is what actually reads as motion. */}
      <BlackHole px={props.px} seed={props.seed} frames={0} period={2.4} />
      <svg
        class="quasar-jets"
        width={props.px}
        height={props.px}
        viewBox={`${-props.px / 2} ${-props.px / 2} ${props.px} ${props.px}`}
        aria-hidden="true"
      >
        <defs>
          <linearGradient id={grad} x1="0" y1="1" x2="0" y2="0">
            {slide("1.35s")}
            <stop offset="0%" stop-color="#ffffff" stop-opacity="1" />
            <stop offset="18%" stop-color={JET_HOT} stop-opacity="0.85" />
            <stop offset="55%" stop-color={JET_COOL} stop-opacity="0.4" />
            <stop offset="100%" stop-color={JET_COOL} stop-opacity="0" />
            <stop offset="100%" stop-color={JET_COOL} stop-opacity="0" />
          </linearGradient>
        </defs>
        {/* Both poles, on the disc's own axis rather than the screen's, so the two
            read as belonging to the same object. */}
        <g transform="rotate(-18)">
          <path
            d={`M 0 ${-props.px * 0.08} L ${-reach() * 0.1} ${-reach()} L ${reach() * 0.1} ${-reach()} Z`}
            fill={`url(#${grad})`}
          />
          <path
            d={`M 0 ${props.px * 0.08} L ${-reach() * 0.1} ${reach()} L ${reach() * 0.1} ${reach()} Z`}
            fill={`url(#${grad})`}
          />
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
