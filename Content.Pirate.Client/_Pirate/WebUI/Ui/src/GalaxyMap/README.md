# GalaxyMap — the Orion Spur chart

An in-game galactic chart: a hex-quantised political map of the Spur, drawn as
SVG inside the CEF webview. This directory is the **browser harness** — it runs
and is art-directed with no game, no server and no engine build.

```bash
npm install
TUI_IFACE=GalaxyMap npm run dev     # http://localhost:5173
```

## Localisation

Two separate things, and the split matters:

- **Content** — territory names, system names, blurbs — is data. It rides on the
  model as `LocalizedText { en, uk? }` and comes from the bridge in game.
- **Chrome** — every label, tooltip and unit the page itself owns — is in
  `lib/i18n.ts`.

`lib/i18n.ts` is written for the bridge to replace: `installStrings()` swaps the
table wholesale and `setLocales()` swaps the locale list, so the game can push
its own strings (resolved from its `.ftl` files) and nothing below changes. The
built-in table is the browser harness's fallback and the shape the C# side should
serialise. Locale codes are the game's (`en-US`, `uk-UA`) so nothing needs
renaming at the boundary.

The alternative — page keeps its own table, game adds parallel `.ftl` entries —
means two sources of truth that drift. It is cheaper to start, which is why the
built-in table exists, but the seam is already in place so it does not have to
stay that way.

## Admin vs player

Same binary, different payload. `GalaxySource.permissions.paint` decides whether
the brushes and UNDO exist; the chart itself is identical either way, so a player
still reads every name, every border and every dispute flag. The DOM check flips
the permission and asserts the tools disappear while the map survives — and that
a click still selects, so read-only does not degrade into inert.

## Planet spike

`lib/planet.ts` generates pixel planets in the page. Ported from Deep-Fold's
MIT-licensed PixelPlanets Godot shaders; the algorithm is small enough that a
clean TypeScript version was less work than vendoring a port. `Spike.tsx` is the
disposable harness that compares them — open it from the PLANETS button, and
PLANETS ON MAP swaps the chart's markers over.

**A planet is a pure function of `(seed, type, size, light, tint)`.** So the model
carries a seed and a type and nothing else — no PNGs, no art pipeline, and two
clients holding the same model draw the same worlds. `StarSystem.planetType` is
optional data so the lore editors pick a world's character; absent means hash the
id, which is what the placeholder map relies on.

Four things the spike settled, none of which were obvious in advance:

- **Generate at display size, never scale down.** Pixel art does not survive
  downscaling, so the octave count is tied to the sprite: the finest octave has to
  land near one pixel, and an octave finer than that is not detail, it is
  per-pixel noise. The first pass looked like moss for exactly that reason. The
  sprite cache is keyed on size for the same reason — key it on the seed alone and
  a zoom silently freezes every planet at the old resolution, which is the same
  bug that once froze the star markers.
- **Dither between discrete palette entries, not along a gradient.** Offsetting the
  height and blending within a continuous ramp produces a band of intermediate
  colours, so the dithered planet came out visibly *blurrier* than the undithered
  one. Confining the mix to a thin window at each threshold is what makes it read
  as pixel art rather than a smudge.
- **A small sprite cannot afford a dark side.** Below ~24px the night half is most
  of the disc, so a planet at map scale stops reading as a lit body and starts
  reading as a hole punched in the territory behind it. The night floor is lifted
  and the night colour lightened for small sprites only.
- **`kind` decides star vs world.** The first cut hashed every system across the
  orbital list, so Sol came out a green terran planet. Seven systems are
  `kind: "star"` in the data and the generator was ignoring that field entirely.
  A star is not a ninth planet type: it is self-luminous, so it skips the
  terminator entirely and gets brightness falling off from the centre, granulation
  and seeded flares. Running a star through the planet lighting model draws a
  planet, which is the whole mistake.
- **Only a star should bleed past its own edge.** The atmosphere halo was on every
  sprite and read as a sticker. It is now the star's corona alone, and a planet's
  sprite is exactly its disc. That also fixed the noise problem below: the halo,
  not the count, was what made a full set of sprites look busy.
- **A sprite on every star and every world, sized by importance** — 40/30/21/16px
  for capital down to minor. The earlier cut drew capitals only, on the theory that
  a full set would be noise. That was wrong, and the halo was the reason it looked
  like noise. The size ramp does the hierarchy work the ring was doing.
- **The capital ring has to clear the sprite.** At 40px a sprite covers a `r=10`
  ring completely, and capitals stop reading as capitals. The ring radius is now
  derived from the sprite size and sits outside the corona.
- **Detail tops out around 128–256px.** The continent shapes are identical at every
  size, because the shape is the planet and frequency is fixed in sphere-UV; only
  the octave count scales. Past ~128 the extra octaves stop being visible and a
  bigger sprite is just a bigger disc. 256 is the useful ceiling for a detail view.

Still open: the type is hashed from the id here, which clusters by chance
(Persepolis and Burzsia both came out lava). Real data from the lore side fixes
that. The `tint` path toward a nation colour is implemented and unused — at 45% it
desaturated each planet into a muddy version of the territory it already sits
inside, and the owner is unambiguous from the fill behind it.

`PLANET_ALGO_VERSION` is part of the sprite cache key, and bumping it is what
invalidated the cache when the halo and the star path changed. Change the noise
and every planet in the game changes, which is the same trap as the bake
fingerprint.

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
npm run check        # claims, cells, outlines, borders, systems, paint, disputes
npm run check:dom    # drives a real browser; needs playwright-core, skips without
npm run check:all    # check + check:dom
npm run gaps         # how close each pair of claims is, and whether they border
npm run render       # standalone SVG, for looking at without a browser
```

`check`, `check:hex` and `check:dom` exist because this code fails *silently and
plausibly*. Several bugs here produced short, well-formed segments and a chart
that merely looked wrong, so the assertions target the invariant rather than the
symptom. The three layers matter because they cannot see each other: the geometry
checks were all green while every star marker sat frozen at the wrong pixel
position, because that bug lived in the view layer's input and not in the data.

`check:dom` is the one that would have caught that. It loads the page in a real
browser and compares what the page *drew* against what the current transform says
it should have drawn, at three viewport sizes — which is also how browser zoom
behaves, since Cmd± changes the size of the CSS viewport. It also drives the
pointer to confirm the hover highlight tracks the cursor. Install it with
`npm i -D playwright-core` and `npx playwright install chromium`; it skips with a
message rather than failing when no browser is available, so it is safe to leave
in CI.

Worth knowing about:

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

**Solid runs a `<For>`/`<Show>` child body inside `untrack()`.** A reactive read
hoisted into a local there is evaluated exactly once and never again. Hoisting
`const P = t().toPx(...)` inside a `<For>` is not a style choice — it silently
freezes that geometry at whatever the viewport was on first paint. Every star
stayed nailed to the position it got for the initial 1200x700 default while the
territory fills, which are read through memos, re-laid-out correctly. The chart
looked *plausible* at the one window size that happened to match and read as
"systems are in the wrong place" everywhere else, and browser zoom — which is
just another way to resize the viewport — moved the fills and not the markers so
the two drifted apart.

`Show` has a second trap in the same place: it normalises its condition to
truthiness (`equals: (a, b) => !a === !b`), so `<Show when={obj}>` will not
re-render for a new object that is still truthy, and its accessor must be read
*inside* the tracked part of the child. The hover highlight is a permanent
element with memoised geometry rather than a `Show` for exactly this reason.

The rule the code now follows: anything depending on the transform is built in a
`createMemo` above the JSX, and the markup only reads plain fields off the
result. That makes the mistake structurally impossible instead of merely
discouraged.

**A drag is one gesture, so it has to be one edit.** Sampling only the cells the
pointer is over between mousemove events leaves a dashed stroke when the mouse
moves faster than the event rate, so each move fills the `hexLine` from the
previous cell. And committing per cell would rebuild the model on every
mousemove — the rebuild re-runs the wobble over every territory outline — while
leaving one undo step per cell, so a single flick would take twenty undos to put
back. So a stroke previews as flat unwobbled hexes and commits once on release.

**Painting is a permission, not a build flag.** The chart is read-only for players
and only an admin gets the brushes, so the page asks the source rather than
assuming. Undo is part of that tool: gating it on the edit count alone left a
read-only viewer with a live UNDO button that reverted an admin's work.

**A memo body runs immediately, so it cannot read a `let` declared below it.**
`canPaint` read `source`, which was declared a few lines further down, and the
whole component threw on first render — a blank page, with no build error, since
TypeScript cannot see the ordering. The same trap bit twice in one sitting; the
second time it was the fix's own new signal.

**`getScreenCTM()` includes the document's zoom, `clientX` does not.** Inverting
that matrix and feeding it client coordinates divides the point by the zoom
factor, so clicks land in the wrong place. `getBoundingClientRect()` is in
unzoomed CSS pixels — the same space as `clientX` — and the SVG is laid out 1:1
with those pixels, so the subtraction is exact at any zoom and under any CSS
transform on an ancestor.

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

**A locale only takes its own translation, never a neighbour's.** The obvious
shortcut — "if it is not English, use the Ukrainian" — hands Ukrainian to a
German client, which is worse than useless because it looks like a working
translation. `pick()` tests that the locale *is* Ukrainian, and anything
untranslated falls back to English rather than to a blank label. This was a real
bug, caught by the check rather than by reading the code.

**Plurals are per-category, not per-language.** Ukrainian has three (one / few /
many) and the "few" band is last-digit 2-4 *excluding* 12-14, so 2 клітинки and
22 клітинки but 12 клітинок. English has two. `PluralText` carries one entry per
category; the check asserts all three render differently, because the usual
failure is collapsing to one hardcoded word and looking fine at n=1.

**Locale names are endonyms and are never translated.** A player hunting for their
own language scans for the script they recognise, so "УКР" has to read "УКР"
even in an English session. Rendering it as "UK" hides the one word they are
looking for behind a pair of Latin letters. It is the only label in the UI that
deliberately ignores the active locale.

**A territory name is drawn in its own colour, so a dark colour is an unreadable
title.** Nothing about that failure is in the code — it depends on the colour —
so `readableOnDark()` lifts any fill below a lightness floor before it is used
for text, preserving hue so "this is Biesel's blue" still reads. Gold on gold and
cyan on cyan had been noticeably harder to read than white on silver, which made
legibility depend on which nation you happened to be looking at. The check
asserts the lift clears a luminance floor, preserves hue, and does not make two
nations collide — worth asserting separately because these colours will come
from prototypes, so a new nation can arrive in a colour nobody looked at.

## Still to do

- Real content replaces the invented polygons and star positions in
  `lib/devmap.ts` (a bake command emitting committed cell lists, per the
  tier-1/tier-2 model). The nations and their ids are already the real ones.
- Ukrainian *system* names reviewed by a native speaker; the territory names are
  converted from the locale file by hand and need the same eye.
- `BridgeSource` to replace `FixtureSource` in game, plus the holotable host.
  `setContested` is the shape a server-validated admin call should take.
- A `strings` push alongside the model push, so the chart's chrome is translated
  from the game's own `.ftl` entries rather than from the built-in table. The
  seam (`installStrings`) is ready and the DOM check exercises it.
- Two-tier typography: the small prefixed `REGION: …` tier is specced, not built.
- Zoom and pan. Deliberately last: the chart is a fixed extent that fits its
  host, and drag-to-paint (done) bought far more than zoom would.
- Contested cells are a flat hatch with no per-claimant identity. A real dispute
  wants "Biesel claims / Izweski claims" rather than a single flag.
