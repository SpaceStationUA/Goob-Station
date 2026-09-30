# GalaxyMap — the Orion Spur chart

An in-game galactic chart: a hex-quantised political map of the Spur, drawn as
SVG inside the CEF webview. This directory is the **browser harness** — it runs
and is art-directed with no game, no server and no engine build.

```bash
npm install
TUI_IFACE=GalaxyMap npm run dev     # http://localhost:5173
```

## Layout

| Path | What it is |
|---|---|
| `lib/hex.ts` | Pointy-top axial hex grid. Pure math, no policy. |
| `lib/model.ts` | The data shapes the page renders. No game types. |
| `lib/geometry.ts` | Claims → cells → smoothed outlines. The interesting file. |
| `lib/transform.ts` | Light-years → pixels. Everything downstream is px. |
| `lib/source.ts` | `GalaxySource` seam, plus the browser `FixtureSource`. |
| `lib/devmap.ts` | Placeholder content, shaped like the real Spur. |
| `Chart.tsx` | Pure SVG presentation. No state beyond its own size. |
| `App.tsx` | State, chrome, the side panel, the paint tool. |

## Checks

```bash
npm run typecheck    # tsc
npm run check:hex    # edge -> neighbour table, verified against geometry
npm run check        # claims, cells, outlines, borders, systems
npm run gaps         # how close each pair of claims is, and whether they border
npm run render       # standalone SVG, for looking at without a browser
```

`check` and `check:hex` exist because this geometry fails *silently and
plausibly*. Several bugs here produced short, well-formed segments and a chart
that merely looked wrong, so the assertions target the invariant rather than the
symptom. Three worth knowing about:

- **The edge table is verified, not trusted.** A wrong `EDGE_TO_NEIGHBOUR` entry
  makes boundary extraction pick wrong edges. It showed up as borders
  fragmenting into ~160 disconnected blobs instead of ~10 chains, while the
  per-segment checks all passed.
- **Assert on `raw`, not the smoothed loop.** The wobble subdivides every
  segment down to ~0.5 LY, so a chain that jumped 36 LY still comes out as a run
  of short plausible steps. Checking the smoothed loop reported 0.68 LY and
  passed while the chart was visibly broken.
- **Adjacency is read from `ownership`, never from border chains.** A chain that
  wanders at a three-way junction spans two territory pairs, so the chains are
  the wrong place to query anything.

## Design notes

**The hex lattice is logical only and is never drawn as-is.** What you see is a
smoothed outline of a cell set, so the grid is invisible structure and the
visible shape is an organic blob that merely happens to be quantised. The
graticule is a separate, faint, toggleable layer.

**Claims may overlap.** Hand-drawn outlines cannot tile perfectly, and a
fraction-of-a-light-year gap is worse than useless: one unclaimed cell in it
severs the shared border along its whole length. Contested cells go to whichever
claim they sit *deepest* inside, which splits the contested band down the
middle. Cells no claim covers are unclaimed space — that is the frontier, and it
is meant to be large.

**Borders are stroked from the fill's own loops.** An earlier version computed
border chains separately, and they drew straight lines across the map. A
territory outline already traces its entire boundary, so reusing it cannot
disagree with the fill. A shared border is stroked once from each side, which
just makes nation-to-nation lines slightly stronger — desirable, and impossible
to get wrong.

**The wobble is driven by distance along the border, not by point index.**
Index-driven noise is uncorrelated between neighbouring samples, so it only ever
adds sub-pixel jitter; it cannot bend a long straight run. A straight edge of a
claim quantises into a long staircase, and the rounding then flattens that
staircase dead straight — which looks nothing like a coastline. Distance-driven
noise is coherent over `wobbleWavelengthLy` and actually bends it.

## Still to do

- Real content replaces `lib/devmap.ts` (a bake command emitting committed cell
  lists, per the tier-1/tier-2 model).
- `BridgeSource` to replace `FixtureSource` in game, plus the holotable host.
- Two-tier typography: the small prefixed `REGION: …` tier is specced, not built.
- Zoom, pan, and the paint tool's drag-to-paint.
