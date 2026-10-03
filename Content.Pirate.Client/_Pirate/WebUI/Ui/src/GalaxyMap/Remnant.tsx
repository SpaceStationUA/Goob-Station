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
   * Beam geometry, as a fraction of the plume's length rather than as pixels.
   *
   * The first version was a wedge: `M 0 apex L -w tip L w tip Z`, a point at the pole
   * opening to half the plume's length at the tip. Measured that is width/length
   * 0.20 -- a cone twenty times wider than a relativistic jet, which is collimated by
   * exactly the thing that makes it visible in the first place. It read as a flat
   * trapezoid with hard straight sides, which is what "always full width" is looking
   * at.
   *
   * A jet is nearly parallel. So the base is a narrow cap at the pole and the tip is
   * only a little wider, and both are fractions of the LENGTH so the proportions
   * survive a change of panel size.
   */
  const BEAM_BASE = 0.026;
  const BEAM_TIP = 0.042;

  /** The knot is wider than the beam, which is the whole of what a travelling knot IS. */
  const KNOT_FLARE = 1.9;

  /**
   * One soft edge, as a luminance mask, self-contained.
   *
   * A hard-edged wedge is the giveaway that this is a polygon, and the flanks are the
   * first thing the eye finds. The profile is a bell with a flat core rather than a
   * linear ramp, which is what a beam's intensity across its width actually looks
   * like.
   *
   * `userSpaceOnUse` because the two layers have different widths, and a mask in
   * bounding-box units would rescale with each path -- the plume's flank would then be
   * softened in proportion to the KNOT's width, which is the wrong flank to soften.
   * The coordinates are the un-rotated jet's own, so the mask turns with the jet inside
   * the `rotate(-18)` group without any extra transform.
   *
   * The gradient is declared INSIDE the mask, so it cannot be referenced from
   * elsewhere and cannot leak into the document as a stray gradient.
   */
  const mask = (id: string, halfWidth: number) => {
    const pad = halfWidth * 1.3;
    return (
      <mask
        id={id}
        maskUnits="userSpaceOnUse"
        x={-pad}
        y={-props.px}
        width={pad * 2}
        height={props.px * 2}
      >
        <linearGradient
          id={`edge-grad-${id}`}
          gradientUnits="userSpaceOnUse"
          x1={-halfWidth}
          y1="0"
          x2={halfWidth}
          y2="0"
        >
          <stop offset="0%" stop-color="#000000" />
          <stop offset="26%" stop-color="#4a4a4a" />
          <stop offset="42%" stop-color="#d8d8d8" />
          <stop offset="50%" stop-color="#ffffff" />
          <stop offset="58%" stop-color="#d8d8d8" />
          <stop offset="74%" stop-color="#4a4a4a" />
          <stop offset="100%" stop-color="#000000" />
        </linearGradient>
        <rect x={-pad} y={-props.px} width={pad * 2} height={props.px * 2} fill={`url(#edge-grad-${id})`} />
      </mask>
    );
  };

  /**
   * The plume itself: a STANDING structure, hot at the pole and fading to the tip,
   * with no animation at all.
   *
   * Every animated attempt at this failed the same way, and the reason is worth
   * writing down because it took three rounds. Driving the plume's whole brightness
   * from a travelling gradient means the plume only exists where the gradient is:
   * the highlight is somewhere, the rest of the jet is not. Mirrored across the two
   * poles -- which it must be, since the two bounding boxes are mirrored -- that puts
   * one highlight at each pole's base at the same instant, so the pair reads as ONE
   * jet with the other missing.
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
   * decide whether the jet is visible -- that is the plume's job now. That separation
   * is what lets the animation values leave 0..1 without the jet going dark.
   *
   * The band is centred in the path's own bounding box, so it occupies the middle
   * ~16% of the length: a knot, not a second plume.
   */
  const knot = (id: string, y1: string, y2: string, phase: string) => (
    <linearGradient id={id} x1="0" y1="1" x2="0" y2="0">
      <animate attributeName="y1" values={y1} dur={`${KNOT_PERIOD}s`} begin={phase} repeatCount="indefinite" />
      <animate attributeName="y2" values={y2} dur={`${KNOT_PERIOD}s`} begin={phase} repeatCount="indefinite" />
      <stop offset="0%" stop-color={JET_HOT} stop-opacity="0" />
      <stop offset="42%" stop-color="#ffffff" stop-opacity="0.95" />
      <stop offset="58%" stop-color={JET_HOT} stop-opacity="0.55" />
      <stop offset="100%" stop-color={JET_HOT} stop-opacity="0" />
    </linearGradient>
  );

  /**
   * One pole: a collimated beam, and a wider band travelling along it.
   *
   * `dir` is +1 for the upper pole and -1 for the lower. The apex is always at the
   * pole and the tip always away from it, so the geometry is one expression and only
   * the fills differ -- four gradients and two masks in total, and every one of them
   * mirrored relative to its partner.
   *
   * The knot path is the SAME shape scaled about the axis by KNOT_FLARE, and its
   * gradient confines it to a short band. So the flare is local and travels with the
   * knot: the beam stays narrow and a travelling bulge runs along it, which is what
   * makes it a jet with pulses in it rather than a striped ribbon.
   */
  const jet = (dir: 1 | -1) => {
    const r = reach();
    const apex = dir * props.px * 0.08;
    const tip = dir * r;
    const p = dir === 1 ? "up" : "down";
    const path = (k: number) => {
      const w = (t: number) => (t === 0 ? props.px * BEAM_BASE : r * BEAM_TIP) * k;
      return `M ${-w(0)} ${apex} L ${-w(1)} ${tip} L ${w(1)} ${tip} L ${w(0)} ${apex} Z`;
    };
    return (
      <>
        <path
          d={path(1)}
          fill={`url(#plume-${p}-${props.seed})`}
          mask={`url(#beam-${props.seed})`}
        />
        <path
          d={path(KNOT_FLARE)}
          fill={`url(#knot-${p}-${props.seed})`}
          mask={`url(#flare-${props.seed})`}
        />
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
              y1 = 1, y2 = 0; the lower's apex is its bbox's top, so the numbers mirror
              to y1 = 0, y2 = 1. */}
          {plume(`plume-up-${props.seed}`, "1", "0")}
          {plume(`plume-down-${props.seed}`, "0", "1")}
          {/* Knots, pole to tip and off the end. Same period, half a cycle apart, so
              the two are never at the same radius. `begin` is negative, which starts
              an animation mid-cycle -- SMIL allows it, and unlike the earlier attempt
              it is decorative rather than load-bearing, because the plume behind it no
              longer depends on the knot being on screen. */}
          {knot(`knot-up-${props.seed}`, "1;0.02;-1", "2;1.02;0", "0s")}
          {knot(`knot-down-${props.seed}`, "0;0.98;1.98", "1;1.98;2.98", `-${KNOT_PERIOD / 2}s`)}
          {/* TWO masks for the whole object, one per layer -- not two per pole.

              The soft edge is a profile symmetric about the jet's axis, and the two
              poles are mirror images, so the upper pole's beam edge is exactly the
              lower pole's. Two masks serve four paths; a per-pole copy would be four
              masks asserting a symmetry one mask already guarantees.

              Each is sized to ITS OWN layer's half-width, which is why they are two and
              not one shared edge: a shared edge in bounding-box units would soften the
              narrow beam in proportion to the wide flare and put the falloff in the
              wrong place entirely. */}
          {mask(`beam-${props.seed}`, reach() * BEAM_TIP)}
          {mask(`flare-${props.seed}`, reach() * BEAM_TIP * KNOT_FLARE)}
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
