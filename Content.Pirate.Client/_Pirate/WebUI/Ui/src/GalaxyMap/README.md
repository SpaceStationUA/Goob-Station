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
- **Rotation is a pre-rendered strip, not a per-frame render.** `planetSheet`
  renders N frames into one horizontal PNG and CSS steps through them with an
  integer `steps()`. The alternative is a canvas and a `toDataURL` per system per
  frame, which at eighteen systems is not something you can do sixty times a
  second. The strip's end offset has to be in pixels, not a percentage:
  percentage `background-position` is measured against (container − image), which
  is negative here.
- **A full turn must land exactly back on the start.** The obvious refinement —
  scaling the shift by `cos(latitude)` so the poles hold still — is *not* a rigid
  rotation: a mid-latitude pixel advances by `cos(lat)` of a texture period rather
  than a whole one, so the loop drifts. A constant shift is the only one that
  closes for every latitude at once.
- **Cloud drift has to be periodic with the loop too.** Scaling the cloud rate
  (`spin * 0.82`) makes the deck a non-integer fraction of a turn, so the land
  closes and the clouds do not — a small jump once per loop. Shear as a *sine of
  the phase* is periodic by construction: the deck runs ahead through the middle
  of the turn and falls back by the end, and the seam is exactly zero.
- **`cloudThreshold` is a cut-off on the noise, not a fraction of the disc, and
  LOWER MEANS MORE CLOUD.** The original shader has the same inverted meaning,
  which is why it is not called `cloudCover`. The first values were guessed
  against nothing and came out so heavy the surface was completely buried; they
  are now calibrated against the actual fbm distribution.
- **The cloud layer is a cellular-displaced fbm, not an fbm.** Value noise alone
  warps into fog. `circleNoise` (ported from their `Clouds.gdshader`, which
  credits a shadertoy author) accumulates into a turbulent field that DISPLACES
  the fbm coordinate, and that is what gives cloud edges instead of haze. The
  cell count has to be real: scaling it by the surface period put less than one
  cell on the whole globe, which produced a single spiral.
- **Rotation needs land to carry it.** At base period 2 the largest continent
  covered half the disc, most of every planet was empty ocean, and rotation read
  as "the one green patch slid off". Period 3 fixes it, and is also what makes
  slow rotation legible at map size at all.
- **A gas giant's cloud deck is physically opaque and visually a waste.** At the
  threshold that makes it literally cloud, the latitude bands vanish and it
  becomes a featureless cream ball. Half cover keeps the banding showing through
  the weather, which is the better of the two.
- **Detail tops out around 128–256px.** The continent shapes are identical at every
  size, because the shape is the planet and frequency is fixed in sphere-UV; only
  the octave count scales. Past ~128 the extra octaves stop being visible and a
  bigger sprite is just a bigger disc. 256 is the useful ceiling for a detail view.

**The map does not animate.** At 16–40px a rotation is invisible and the strip
would cost N times the pixels for nothing, so the chart keeps drawing single
stills. The machinery is built and exercised by the spike; it belongs in a system
detail view or the holotable card, neither of which exists yet. Clouds DO ship on
the map, because a still cloud pattern is visible at any size.

The loop-closure check in `tools/check-dom.mjs` earns its keep: it caught the
cloud-drift bug, and its first version was itself vacuous — comparing the last
frame against the first passes whether or not the rotation closes, because those
two are one step apart in the sequence either way. It now asserts that `spin: 1`
is pixel-identical to `spin: 0`, which is the actual property, and it was
negative-controlled by reintroducing both bugs to confirm it goes red.

Still open: the type is hashed from the id here, which clusters by chance
(Persepolis and Burzsia both came out lava). Real data from the lore side fixes
that. The `tint` path toward a nation colour is implemented and unused — at 45% it
desaturated each planet into a muddy version of the territory it already sits
inside, and the owner is unambiguous from the fill behind it.

`PLANET_ALGO_VERSION` is part of the sprite cache key, and bumping it is what
invalidated the cache when the halo and the star path changed. Change the noise
and every planet in the game changes, which is the same trap as the bake
fingerprint.

## The system overlay

Clicking a star opens a panel describing that system. It exists because of a
measurement, not a hunch: the game window for this UI will be about the size of
the existing webview windows (400x280 for the theme picker, 560x760 for the
arcade), so a marker on the chart is 16-40px and stays 16-40px no matter how good
the generator is. A world has to be drawn somewhere it can be seen, and the only
such place is a panel.

**What the selection actually is.** The chart is a picture of
`NationalityPrototype` — seven nations, and `Profile.Nationality` is the one
profile field that is a *place* rather than an attribute. `SpeciesPrototype` and
`EmployerPrototype` carry sprite sets, skin tones, name datasets and rival lists;
neither has a coordinate or a homeworld. So the territory is the unit of
selection and the star is the unit of reading, which is why the overlay opens on
a marker and describes the system, with the owning nation as a link rather than
as the thing that was clicked.

- **Draw the world at 200px, not at marker size.** The overlay panel is 268px
  wide; 200px is the largest round size that leaves the corona room inside it.
  Anything near 16-40px in here would make the panel pointless.
- **Rotation is generated at 1x, the still at the display ratio.** Two different
  ratios, deliberately. A 200px still at 2x costs ~100ms and must be sharp — it
  is what the panel shows first and what it keeps if the strip never arrives. The
  24-frame strip at 2x costs ~1.9s, so it is generated at 1x, where it costs
  ~540ms; rotation hides resampling and 1x pixel art under
  `image-rendering: pixelated` is still pixel art. An earlier version collapsed
  both onto one ratio and quietly gave the still half resolution on a 2x screen.
- **The still comes first, the rotation is deferred.** Measured on a cold system:
  the still is in the DOM at ~10ms, the strip at ~200ms. Generating the strip
  inline is a synchronous pixel loop ending in `toDataURL`, so doing it on click
  freezes the page for half a second at exactly the moment the player is looking.
  Deferring it to a macrotask costs nothing and makes the first paint complete.
  `check-dom.mjs` asserts the gap, on a deliberately unopened system — measured on
  a cached one it is a cache hit and the check would pass for the wrong reason.
- **Damp the noise toward the poles.** The sphere projection squeezes the surface
  coordinate hard at the top and bottom of the disc, so a row there crosses many
  noise periods at once and the field aliases. Against a hard threshold that does
  not look like fine detail, it looks like a solid band: the sprite grew a flat
  white cap with a horizontal edge across it, which reads as a cropped image
  rather than a pole. Pulling the field toward its mean as the limb approaches
  fixes it. Only visible at overlay size — at 40px it was a slightly pale top.
- **The star corona falls off as g^4, not g^2.** A square falloff looks right at
  16px and wrong at 200px, where the corona is a wide evenly-lit donut with a
  soft edge — a fuzzy blob rather than a star.

### Hit targets

- **The hit radius tracks whatever is actually drawn.** The two states differ by
  more than 4x: a minor marker is a 3.2px dot with sprites off and a 16px sprite
  with them on. A radius sized for the sprite would let a plain dot swallow
  clicks a third of the map away, so the target would change meaning when someone
  toggles the comparison. Padding is generous either way — a person aims at a
  place, not at a six-pixel circle.
- **One click authority.** The per-marker circle is painted *before* the sprite,
  ring and label that belong to it, so a click on the middle of a star lands on
  the sprite regardless of z-index. Routing through the nearest-marker test in the
  root handler makes the outcome independent of paint order. Nearest wins rather
  than first hit, or a star behind another is unclickable depending on list order.
- **Painting beats reading.** An armed brush suppresses the hit targets entirely
  and takes the click, so dragging a stroke across a capital cannot open a panel
  mid-drag.

## Matching the reference generator

The look comes from two things, and both were missing from the first port. Neither
is a detail of the shader — they are the reason a planet from this generator looks
like a planet from it.

- **Band by distance to a light POINT in screen space, not by a dot product.** The
  first version lit the sphere with `dot(normal, light)` and got a smooth 3D
  terminator: correct, and not the reference look. Banding on
  `distance(uv, light_origin)` gives a flat, poster-like terminator with visible
  steps in it. Adding fbm to that distance is the other half — without it the
  steps land on a clean arc and the result reads as a vector illustration of a
  sphere rather than as a world.
- **Pick land by comparing four DISPLACED fields against each other.** The first
  version walked a single field through three thresholds, which produces smooth
  bands following that field's own contours: elevation shading, not land.
  Comparing `fbm2/3/4` — each displaced along the light direction by an amount
  proportional to `fbm1` — puts the boundaries where two independent fields cross,
  which is where they get thin, broken and island-like. The displacement is what
  makes it cohere: the comparison resolves toward the light more often than away
  from it, so land brightness correlates with position on the disc.

Which is also why the palette is two lists rather than one: `sea` is banded by
distance to the light, `land` is chosen by the comparison. The original draws these
as two separate composited layers, and the split is the look, not an
implementation detail.

### Everything has to be discrete

This was the last thing to fall over and it is the easiest to regress silently,
because a screenshot of a smooth-shaded planet still looks like a planet. A
continuous terminator multiply on top of the banding put **1858 distinct colours
in a 128px world**; making every layer discrete brings that to **337**, and a star
to **59**. `check-dom.mjs` asserts both, because nothing else would notice.

The corollary: **one cell field lights up its cell WALLS**, because F1 distance's
contours are the walls. A star built from `1 - worley()` is a honeycomb — which is
exactly what the second attempt produced. The original uses the *product* of two
cell fields, which only lights up where two centres nearly coincide and is
irregular. The product is then skewed (measured 37/37/18/9 across four bands), and
a linear scale cannot fix a skew, so the quartiles are stretched onto even spacing
by a piecewise remap: 19/35/26/20.

Three more things that were wrong in ways that only showed up on screen:

- **Gas giants are not latitude-banded.** The obvious implementation — bands of
  constant latitude warped by noise — is wrong twice: at low warp the boundaries
  are ruler-straight, and raising it turns them into stacked rectangles with
  vertical ends, because a low-frequency fbm makes plateaus rather than swirls.
  The original draws no bands at all; it samples the cellular cloud field and
  picks the palette from distance-to-light plus cloud depth. The horizontal
  banding is a by-product, because `circleNoise` shears alternate rows and biases
  the field into streaks on its own.
- **Flares belong in the corona.** Painting them on the disc put four narrow
  `cos^40` spikes across a lit sphere, which reads as a hard white cross or a lens
  artefact. A flare is emission *outside* the surface, and putting it in the only
  part of the sprite that is not the surface costs nothing — the disc pixels no
  longer compute it.
- **Land palettes must be low-contrast.** The comparison hands the brightest band
  to roughly half of whatever passes the land test, so a wide land ramp washes out
  the entire lit side. In the original the land layer composites over the banded
  sea and is never banded itself, so its four tones are near neighbours and the
  terminator darkening comes from the shared shade term.

## Layout

| Path | What it is |
|---|---|
| `WorldSprite.tsx` | One generated world, still-then-turning. Lives outside the spike because the spike is disposable. |
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
- **Solid's `<For>` does not wrap a per-item fragment, so all markers are siblings
  in one `<g>`.** Anything that walks up from a marker to find *its* label or
  sprite finds the first one in the chart instead — silently, and with the right
  answer often enough to look fine. Two overlay checks passed only because the
  capital they happened to compare against was the first marker in the list. Both
  markers and labels now carry `data-sys` so tests address them by id.
- **One probe point cannot serve two claims about different radii.** "A planet has
  no halo" wants a point outside its disc; "a star bleeds" wants a point outside
  the star's disc but inside its corona. A planet's disc has radius d/2 and a
  star's has radius d/3 with its corona reaching d/2, so the box corner — the
  obvious single probe — is outside both and transparent for every type. It would
  have passed a check asserting the exact opposite of the truth.
- **An assertion that cannot fail is worse than no assertion**, because it is
  reported as coverage. Three in a row here: a ratio check written as
  `natural >= css` passes trivially at dpr 1 (it must be `css * dpr`); a
  timing check measured on an already-opened system reads a cache hit; and a
  deferral check phrased as "a world is on screen" passes even when the strip is
  generated synchronously 199ms in. All three were negative-controlled by breaking
  the code they cover.
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
