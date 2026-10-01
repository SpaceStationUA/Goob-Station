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
}
