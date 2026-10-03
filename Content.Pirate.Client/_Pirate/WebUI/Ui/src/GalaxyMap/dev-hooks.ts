/**
 * Test hooks, and only in a dev build.
 *
 * `check-dom.mjs` needs to render a planet strip and read individual frames back
 * out of it. It cannot do that with a dynamic `import()` of the module: Vite
 * rewrites the specifier to a URL that only resolves inside a bundled graph, so
 * the import comes back as a module with no exports and the check silently
 * measures nothing. The other checks go through `window` for the same reason.
 *
 * These used to live in `Spike.tsx`, which was a disposable comparison harness.
 * That was an accident of history: the hooks are how the browser tests see the
 * renderers at all, so deleting the harness deleted the tests' only handle on
 * them. They are here instead, where deleting a panel cannot take them.
 *
 * `__galaxySetPermission` is different in kind — it is how the read-only checks
 * put the page into the state a player sees — and it is installed by `App`, which
 * owns the permission signal. See `App.tsx`.
 */
import { blackHoleUri } from "./lib/blackhole";
import { blackHoleGL } from "./lib/gl";
import { planetSheet, planetUri } from "./lib/planet";

/**
 * Dev-only, and the guard is the point.
 *
 * A hook that exists in a shipped build is a way for anything with a reference to
 * the window to drive the renderers, and in this page that includes content
 * injected through the model's localised strings. `import.meta.env.DEV` is a
 * compile-time constant, so the whole block is removed from the production bundle
 * rather than merely skipped at runtime.
 */
export function installTestHooks(): void {
  if (!import.meta.env.DEV) return;
  const w = window as unknown as Record<string, unknown>;
  w.__galaxySheet = planetSheet;
  w.__galaxyStill = planetUri;
  w.__galaxyBlackHole = blackHoleUri;
  /**
   * The black hole's disc, doppler on or off, in one A/B.
   *
   * This exists because of a specific failure. The Doppler beaming read as a plausible
   * 1.33:1 left/right asymmetry, and a negative control with it switched OFF read 1.32:1
   * -- indistinguishable. The disc's own `light_d` gradient is already asymmetric by
   * about that much, so any assertion of the form "the disc's halves differ" would have
   * passed forever, meant nothing, and looked like coverage.
   *
   * The fix is not a better threshold. It is to render BOTH settings on one page, at the
   * same seed and the same rotation phase, and compare them to each other. The baseline
   * is then measured rather than guessed, which is the only reason the comparison can be
   * trusted: the number it has to beat is a property of this disc rather than a constant
   * somebody picked.
   *
   * `animate: false` on both, and that is load-bearing. Animated, the two canvases start
   * at slightly different `performance.now()` values and the disc's rotation and its
   * fbm differ, so the comparison measures rotation noise rather than the Doppler term.
   * Still, both draw one frame at u_time = 0 and are pixel-identical apart from the
   * uniform under test.
   */
  w.__galaxyBlackHoleAB = (px: number, seed: number) => {
    const mk = (doppler: number, ring: number) => {
      const inst = blackHoleGL({ seed, px, animate: false, time: 3.7, doppler, photonRing: ring });
      if (!inst) return null;
      return inst.canvas;
    };
    return {
      on: mk(0.24, 0.9),
      off: mk(0.0, 0.9),
      // A third, to show the photon ring is separable from the Doppler term: same
      // disc, no ring. Without it a ring that brightens one side would be credited to
      // the beaming.
      noRing: mk(0.24, 0.0),
    };
  };
}
