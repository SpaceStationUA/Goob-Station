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
- **The paint contract is asserted, not assumed.** A source that mutates its
  model in place leaves every geometry check green while the page silently stops
  updating, because the bug lives in the view layer's input and not in the data.

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

**A change must arrive as a new object.** `FixtureSource` builds a fresh model
and a fresh ownership map on every edit. Handing the view the object it already
holds — even after mutating its contents — changes nothing on screen, which is
exactly why painting appeared to do nothing while the underlying data was
correct throughout. `npm run check` asserts this contract directly.

**Territory names are placed, not just centred.** The centroid of a cell set
routinely lands on a capital, and a letterspaced uppercase title is a third of
the map wide, so nudging the anchor clear is nowhere near enough — placement
scores the label's whole box against every system marker. Territory names also
draw *under* place names: a title will happily bury the capital sitting beneath
it, and place names are the thing you actually look up. Unclaimed space gets a
small dim watermark rather than a title, because it is a hole rather than a
nation and its centroid is the dead centre of the chart.

**Names are localised on the page, not by the server.** `LocalizedText` carries
every language we ship and the page resolves against the client's locale, so
switching language is instant and needs no round trip. Note that
`uk-UA/_Pirate/contractors/nationality.ftl` holds *genitive* forms — they were
written to follow a label like "Національність:", so they read as "of the
Republic of Biesel". A map label stands alone and needs the nominative, which
`devmap.ts` converts by hand. System names have no locale entries at all yet and
are transliterations pending review.

## Still to do

- Real content replaces the invented polygons and star positions in
  `lib/devmap.ts` (a bake command emitting committed cell lists, per the
  tier-1/tier-2 model). The nations and their ids are already the real ones.
- Ukrainian *system* names reviewed by a native speaker; the territory names are
  converted from the locale file by hand and need the same eye.
- `BridgeSource` to replace `FixtureSource` in game, plus the holotable host.
- Two-tier typography: the small prefixed `REGION: …` tier is specced, not built.
- Zoom, pan, and the paint tool's drag-to-paint.
