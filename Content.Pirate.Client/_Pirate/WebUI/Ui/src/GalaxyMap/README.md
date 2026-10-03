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

## Ring systems

`lib/gl-ring.ts` is a transcription of the reference's `Ring.gdshader`, with the
uniform values `GasPlanetLayers.tscn` gives it. It replaced a design, and the
design was wrong in three ways that only the source could tell me:

- **The ring is carved by noise.** `ring *= fbm(...)` with four octaves, then
  `step(0.28, ring)` for the alpha — so the divisions in a ring are where the noise
  fell below the cut. Three flat ribbons cannot have divisions. That is the whole
  reason the old ring read as a wire hoop laid across the planet.
- **`ring_perspective` is 6.0**, not the declared 4.0, and the old ring was authored
  against 0.22, which is neither and is what you reach for when asked how open a
  ring should look without the source in front of you.
- **The planet's hole is the ring's own job**, cut in its own uv by
  `if (uv.y < 0.5) ring *= step(1/scale_rel_to_planet, distance(uv, vec2(0.5)))`.
  The ring canvas is three times the planet's and `1/6` is exactly the planet's
  radius in it.

### The ring goes ON TOP of the planet

In `GasPlanetLayers.tscn` the `Ring` node is index 1 and `GasLayers` index 0, and
Godot draws later siblings on top. So the reference paints the ring **over** the
planet and does its own occlusion: the shader cuts the far arm, and the near arm is
left alone so it can lie across the planet's face.

This canvas was first placed *behind* the sprite, reasoning that the sprite is
opaque across its disc so the ring would be occluded for free. That is true and it
is wrong — it hides the near arm too, so the ring stops dead at the planet's edge on
both sides and reads as two stubs. The hole is not a substitute for painting the
ring on top; it is only the far half of the arrangement.

The same class of bug bit twice, in opposite directions, which is worth recording:
first the far half drew over the planet, then the near half was hidden by it. Both
present as "the ring looks wrong" and neither is visible in the DOM.

### Two deviations, both about fit and neither about structure

- **Size.** The reference's ring reaches 3.16 planetary radii, which around a 200px
  planet is 632px across. `CANVAS_TO_PLANET` is 2.1, giving 2.21 — affordable, and
  closer to Saturn's own 2.3 than the reference is. The panel is 490px for the ringed
  case.
- **Palette.** The reference's shadowed tones are dark plums and charcoals, chosen
  against the generator's mid-grey backdrop. This panel is near-black, so that end of
  the ramp landed at luminance 8-20 against a background at 12 and the outer half of
  the ring vanished. The three are lifted, keeping their hue relationships.

### What the checks measure, and what they could not

The occlusion is read off the ring canvas's own pixels: inside the planet's disc the
ring must be absent on the far side of the rotation and present on the near side.

That check passed while the ring was behind the sprite, because z-order does not
change what the canvas contains. So there is a second check that reads a
**screenshot of the composited page**, looking for pixels where blue is above green —
true of the ring's plum tones and of no planet tone, since the planet's palette is
cream and tan throughout. Negative-controlled by putting the ring back behind the
sprite: 0 of 15681 pixels.

Deriving the shader's `rotated.y` in that check by reading the `mat2` is a trap: the
two constructor arguments are the matrix's columns, and getting the convention wrong
puts the check 90 degrees out. It was wrong, and it showed up as 247 lit pixels on the
far side of a boundary that was in the wrong place — a confident wrong answer rather
than an obvious failure. The expression is measured now, and says so.

## The bake

`Resources/Prototypes/_Pirate/Galaxy/orionSpur.yml` is the **intent**: hand-drawn
polygons, in light-years, one per nation. `lib/baked.ts` is the **result**: a
committed cell list. Nothing at runtime ever reads a polygon.

That split is the whole design. A polygon is the only thing a lore editor can
reasonably author, and a cell list is the only thing the game can store, reason
about, hand to a player, and paint over one cell at a time in-round. Asking for
the second in the first's place means asking someone to draw 40 points per nation
and getting it subtly wrong in a way nobody notices until two nations overlap.

```
npm run bake          # write lib/baked.ts
npm run bake:check    # fail if it is stale; runs as part of `npm run check`
```

**One code path.** The bake calls the same `buildOwnership` the browser harness
renders from, so the committed cells and the harness's map cannot be produced by
two different rules. `check-galaxy.ts` asserts it, cell by cell, and
negative-controls that assertion by moving a single cell between two territories.

**The fingerprint** covers the prototype text, the map spec, and
`ASSIGNMENT_VERSION`. It exists because the failure it catches is otherwise
silent: someone edits a border, does not re-bake, and the game ships a map that is
plausible and wrong. `checkBake(committed, expected)` is the runtime half — the C#
side recomputes `expected` from the prototypes it loaded and passes it with the
model, so a stale bake is caught at load. It reports three states, not two:
fresh, stale, and **unverifiable**. Unverifiable is not fresh, and it never
renders as one; the browser harness sits in that state because it has no
filesystem to recompute from.

**Bumping `ASSIGNMENT_VERSION` invalidates every bake on purpose.** If the rules
in `assignCells` move, every cell in the map may move, and that belongs in the diff
rather than in a bug report.

### The YAML parser

There isn't a dependency for it. `tools/yaml-subset.ts` reads the subset the
prototype uses and **refuses everything else by name and line** — flow mappings,
block scalars, anchors, tags, quoted strings, duplicate keys, tabs.

Refusing is the design, not a limitation to apologise for. A permissive parser
that mis-reads a construct puts a border in the wrong place with no error
anywhere, which is the worst failure mode geometry has. A parser that stops and
tells you which line it choked on is one you can hand a prototype. Nineteen cases
pin that behaviour; a tab is refused because a tab is one column of indent to one
reader and eight to another, so the same file parses to a different shape
depending on who is counting.

Two off-by-ones in it are worth recording because both were invisible: `parseSeq`
and `parseMap` each consumed the cursor's line *before* their loop, so every
sequence failed on its first item and every mapping silently lost its first key.
The second one is the dangerous shape — a map missing `width` reads as a
misconfigured map, not as a broken parser.

## Remnants

Four `SystemKind`s that are the endpoints and the middle of stellar evolution, and
they are one family on purpose — the chart can show a star's whole life:

    star ──> dwarf        a core that stopped collapsing, still cooling
         └─> remnant      the explosion, and the shell it leaves
             └─> pulsar   a collapsed core, spinning, beams out of its poles
                └─> blackhole   collapsed past the point of no return

**Every one of these is an observed object.** That was the test for adding any of
them, and it is why there is no white hole: a white hole solves the equations of
general relativity and nothing has ever been observed that requires one, which makes
it a different kind of thing to put on a map that otherwise shows real objects in
real positions.

**The pulsar is canvas 2D and the black hole and the ring are WebGL**, and the
distinction is not an inconsistency. Those two are shaders because their shapes are
*carved by noise* — the divisions in a ring and in an accretion disc are where an fbm
fell below a threshold, and there is no way to draw that with gradients or paths. A
pulsar is a hard point and two soft cones, which is gradients, and a shader for it
would be a whole program to do something `createRadialGradient` already does.

The beam sweep is the only animation on this chart and it is deliberate: a pulsar is
distinguished from every other kind of neutron star by the fact that its emission is
beamed and rotating. Drawn as a static dot it is a white dwarf.

**Both are on the chart as well as the overlay**, and they were MISSING from the
chart for two rounds of work after being added to the overlay. A `SystemKind` that
matches no marker branch is not an error, it is an absence, and every check was
green because every check opened the overlay. That is the same shape of mistake as
the nebula behind an opaque rect, so it is now written down as a rule: **a feature
added to one surface needs an assertion on the other surface or it does not exist.**

The chart's marks are SVG rather than the overlay's canvas, because they are 16 to
40 pixels and the overlay is 190. Four octaves of fbm and a sweeping beam are both
invisible at 16px, and a path costs nothing per marker where a canvas costs a
compositing layer each.

What does have to survive the reduction is contrast, not size. The first pulsar mark
was small AND pale, against a territory fill that is a bright hatch, and it was
correctly reported as invisible. It now has a dark rim under the core, because a
white dot on a hatch disappears into it and a rim is what makes it a dot.

**The quasar's jets move, and it took three attempts to get right.** "Sits in the
same position and looks inert" was the complaint, and each fix failed for a
different reason:

1. *Fading the plume.* Says something is happening without showing anything
   happening. The eye locks onto the largest stationary thing in the frame and the
   jet is that.
2. *Discrete knots travelling outward.* Geometrically right — a jet really does
   carry bright condensations, visible in M87's — and they rendered as grey blocks.
   Scaling a trapezoid vertically produces a bigger trapezoid, and a screen-blended
   off-white trapezoid on a dark background is a rectangle.
3. *Sliding the jet's GRADIENT.* **One gradient, shared.** Wrong, and instructive:

   `gradientUnits` defaults to `objectBoundingBox`, so a gradient resolves against
   each referencing element's OWN box — and the two jets' boxes are mirrored, because
   the upper path's apex is its bbox's bottom and the lower path's apex is its bbox's
   top. So one `y1 = 1, y2 = 0` means "hot at the pole" for one jet and "hot at the
   tip" for the other, and a single animation drove both. The symptom was the two
   jets pulsing to the same side: the object flexed like one thing instead of
   behaving like two plumes.

   Each pole now has its own ramp, its own direction, and its own duration — measured
   correlation **0.20**, where the shared gradient gives **0.85**.

   And then the ramps were themselves wrong. Gradient coordinates are in
   objectBoundingBox units, so anything outside `0..1` is off the element entirely,
   and the values ran to `-0.9` and `-1.9` — which put the whole ramp outside the
   jet for much of every cycle. Measured over sixteen frames, the lower jet was
   completely **absent for two of them** and the upper barely moved its light at all.
   A highlight that spends half its time not on the shape is a blink, not a jet.

   `keyTimes` fixes it: 0.88 of the cycle carries the hot stop from the pole to the
   tip, and the rest carries it off the end into the gap before the next starts.

   And then the phase offset was tried, and it did nothing — which is the finding
   that actually mattered.

   Deleting the half-cycle `begin` offset entirely still measured correlation
   **-0.77**. The correlation never measured the offset. The two bounding boxes are
   mirrored, so the mirrored traversal plus a shared stop list already puts the poles
   half a cycle apart whether or not `begin` says so — and the symptom the offset was
   supposed to cure was never cured. The object still read as ONE jet with the other
   missing, which is what was reported.

   The fix had to be structural rather than a phase value.

4. *The beam was a WEDGE.* `M 0 apex L -w tip L w tip Z` -- a point at the pole opening
   to a fifth of the plume's length at the tip, width/length **0.20**. That is a cone,
   not a jet, and it is what "always full width" is looking at: a relativistic jet is
   collimated by the very thing that makes it visible.

   Now near-parallel, with the widths as fractions of the LENGTH so the proportions
   survive a change of panel size: base 0.026, tip 0.042, measured **0.102**. A jet
   that tapers 1.77x from base to tip is a cone again; this one holds at 0.74x.

   The flanks were hard straight lines, which is the giveaway that a thing is a
   polygon, so each layer gets a luminance mask carrying a bell profile. Two masks,
   not four: the edge is symmetric about the axis and the poles are mirrors, so one
   mask serves both. Two rather than one, because each is sized to *its own* layer's
   half-width -- a shared edge in bounding-box units would soften the narrow beam in
   proportion to the wide flare and put the falloff in the wrong place.

   And the knot is the beam's shape scaled **1.9x** about the axis, with its gradient
   confining it to the middle ~16% of the length. That is what makes the bulge
   *local* and *travelling* -- a beam with a pulse in it, rather than a fatter second
   plume.

5. *The plume is STANDING; only the knot travels.* The actual fix.

   Driving the plume's whole brightness from a travelling gradient means the plume
   exists only where the gradient is. The highlight is somewhere; the rest of the jet
   is not. Mirrored across the two poles — which it must be — that puts one highlight
   at each pole's base simultaneously, so the pair reads as one jet with the other
   missing. No phase relationship can cure it, because the fault is that the
   highlight is the *only* thing there.

   So each jet is now two paths of the same shape: a **standing plume**, hot at the
   pole and fading to the tip, with no animation at all; and a **travelling knot**
   over the top, a narrow band whose own stops are transparent–hot–transparent. The
   separation is the point. Where the gradient is no longer decides whether the jet
   is visible, so the knot's values can leave `0..1` without the jet going dark, and
   no phase arrangement between the poles can un-light one.

   A quasar is a continuous jet with pulses in it, not a jet that blinks.

## What the checks on this object got wrong, in order

   - Round one: **correlation**. Caught the shared gradient. Blind to two dark jets,
     which are perfectly uncorrelated.
   - Round two: **presence**. Caught the ramp leaving the element. Blind to
     imbalance, because both poles can be plainly lit and wildly different.
   - Round three: **correlation again**, plus the offset. The negative control —
     deleting the offset — passed at -0.77, so this whole round proved the assertion
     was measuring nothing it claimed.

   What ships instead:

   | assertion | what only it can fail |
   |---|---|
   | both poles lit in **every** frame | a gradient running off the element |
   | worst-frame brightness ratio **< 3:1** | one gradient shared across both poles |
   | centroid and lit count both **move** | a plume that has stopped animating entirely |
   | plumes static, knots animated | the plume being animated again |
   | tip width / length **< 0.12** | the wedge |
   | knot wider than the beam, **< 3x** | a fatter second plume instead of a knot |
   | every path masked, **2 masks** | hard polygon flanks |

   The geometry is asserted from the `d` attribute, not from pixels. Pixel
   measurement was tried first and reported width/length **1.3** for a beam plainly
   narrower than it is long: the cold-pixel filter cannot distinguish a dark blue jet
   from the dark outer reaches of an orange disc, so the disc dominated every row
   width. The path data is exact and has no filter to tune.

   Which then exposed a nastier version of the same thing. The pixel checks had
   absolute thresholds -- `alpha > 40`, `blue > 140` -- tuned to the old fat beam.
   Narrowing it and softening its flanks made it dimmer, the fixed blue cut deleted
   most of it, and the suite reported "the lower jet is absent in half the frames" and
   a 5.4:1 imbalance. Both were the **check** being wrong, not the renderer. Presence
   and balance are now measured against each pole's own peak in the sample.

   That is the seventh time in this thread that a threshold had outlived the art it
   was measuring.

   Negative-controlled, both failing on the right line:

   | control | fails |
   |---|---|
   | both plumes use the upper pole's numbers | balance, at **4.4:1** |
   | plume animated again | plumes-stand-still |
   | `BEAM_TIP` back to the wedge's 0.10 | collimated, at **0.242** |
   | masks removed | every-path-masked, **0/4** |

   The second control is the point: re-animating the plume did **not** fail presence
   or balance, because a mirrored animated plume still lights both poles. It is
   caught structurally instead, by asserting which gradient moves — because that is
   the fault, not one of its symptoms.

 No shape involved, so nothing to misread, and the
   motion is continuous. The bright stop runs from the pole to the tip and the
   gradient's extent moves with it so the ramp never tears.

The quasar's disc also turns at 2.4s against the black hole's 6s. The obvious reason
is that a quasar is not a stellar black hole. The real one is perceptual: the disc
is thin and mostly dark, so a slow rotation changes about one per cent of the
panel's pixels however long you wait, and the eye reads one per cent over six
seconds as nothing happening.

Worth recording that the metric and the judgement disagreed. The gradient slide
*halved* the measured fraction of pixels changing — 3.1% down to 1.0% — while making
the object obviously more alive, because a travelling highlight changes fewer pixels
than a block flipping. The screenshot is the evidence and the number is the
thermometer.

**The quasar's jets pulse rather than sweep.** The obvious fix to "it just sits in
the same position" is to rotate something, and that would be wrong: a jet is a steady
plume leaving a pole, and a plume that swings is a clock hand. What makes a quasar
look alive is that its output *varies*, so opacity is animated and position is not.

**The quasar is the cheapest of the four and the most striking**, because the
expensive part is already built and already correct. Jets are two soft cones along
the disc's axis, composited with `mix-blend-mode: screen` so the near one crossing
the disc's face brightens rather than hides — a jet is optically thin, so you see it
over the disc, and that crossing is what gives the object depth.

## Planet types

Eight orbital types: terran, ocean, **river**, desert, ice, gas, lava, barren. Plus
`star` and `asteroid`, which are deliberately not planets.

**River worlds** are the eighth, and they are the reference's `LandRivers.gdshader`
rather than a tint of terran. Two things had to be added to the type, because the
river pass already existed and terran and ocean both use it:

- **`riverCutoff`.** The existing test is relative — a river appears where the river
  field is low *compared to how high the land is* — which gives a few streaks on each
  continent. The reference's river world has water over roughly two fifths of its
  surface, because its test is absolute. An absolute cutoff lets one type be mostly
  river without making every type mostly river.
- **`riverOctaves`.** Added, then found to be a no-op: the renderer already caps
  octaves at 6 for a 220px sprite, which is the reference's value. It stays because
  the cap is a size-derived number and a future smaller sprite would fall below it.

The thing that actually fixed the look was the cutoff. At the reference's 0.368 the
rivers merged into one large body and the world read as a terran with inland seas.
At 0.30 they are channels again — 7% of the disc is river water.

## Generated worlds

`lib/planet.ts` generates pixel planets in the page. Ported from Deep-Fold's
MIT-licensed PixelPlanets Godot shaders; the algorithm is small enough that a
clean TypeScript version was less work than vendoring a port. The toolbar's
PLANETS button swaps the chart's markers from dots to their own generated world.

The PLANETS toggle began life as a checkbox in `Spike.tsx`, a disposable comparison
harness that compared every world type side by side. It answered its question and
was deleted, but the toggle graduated to the toolbar first: a system drawn as a
place rather than a pin is a chart decision, not a debugging one.

**A planet is a pure function of `(seed, type, size, light, tint)`.** So the model
carries a seed and a type and nothing else — no PNGs, no art pipeline, and two
clients holding the same model draw the same worlds. `StarSystem.planetType` is
optional data so the lore editors pick a world's character; absent means hash the
id, which is what the placeholder map relies on.

Four things the comparison harness settled, none of which were obvious in advance:

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
stills. The machinery is built and exercised; it belongs in a system
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

### Craters

Added to the airless worlds only — barren, lava, asteroid. A crater on a world
with an atmosphere is a contradiction the eye catches at a glance: there is
nothing left to erode it and nothing to fill it.

Three zones rather than one threshold, because a crater needs a floor that is
unambiguously in shadow and a rim that is a thin arc, and one threshold cannot
tune both. The lit rim needs a **second sample of the same field, displaced
toward the light**: where the displaced copy is *higher*, moving toward the light
walks out of the bowl, which is the far wall — the one whose inward face looks
back at the source. That comparison was backwards in the first attempt, and the
symptom was unmistakable once seen: every crater came out as a complete bright
ring, because both walls were being lit. A ring all the way round is a bubble
outline, not a hole in the ground.

Two measurement notes, because both were wrong first:

- **The frequency curve was inverted in its effect.** `round(d / 52)` clamped to a
  minimum of 2, so a 128px sprite got two cells across the entire sphere and the
  product field never crossed the bowl threshold anywhere — craters covered 28
  pixels of 16384, and only sprites above ~150px had any. Those are the ones with
  room to spare.
- **"How many dark pixels" is not a function of "how many craters."** Counting
  pixels darker than a smoothed copy of themselves gave a ratio of 1.1 on grey
  rock and went *below* 1.0 on lava: a crater darkens a dark patch less than it
  darkens a bright one. The check now differences the two renders directly.

### The asteroid is not a disc

Its outline is the surface field minus the radius, thresholded — the silhouette is
wherever the rock is still high enough to exist at that distance from the middle.
It was a circle with banded noise inside it, which is a grey ball, and the only
shape here that is not a world is the one shape that must not be a sphere.

Sampled in the **disc** plane, not the sphere. The surface detail is wrapped around
a sphere, which is right for shading a globe; an outline is a flat 2D shape, and
warping its coordinate gives a lumpy circle rather than a lump.

Three things were wrong in a row, all visible only on screen:

- **Frequency.** Five cells with four octaves put the finest detail at forty cells
  across the rock. The outline came out fractal-edged and read as a splat. Two
  cells and two octaves: an asteroid is two or three big lobes.
- **The colour ladder was backwards.** `sea` runs lit to shadow with index 0
  lightest, and the brightest band was on the *shadowed* side. The rock came out
  muddy rather than merely dim.
- **The shading spanned the whole palette.** Three far-apart tones over large
  zones is camouflage, and the noise was competing with the terminator instead of
  decorating it. Shading by half-steps toward the neighbours — two extra fixed
  colours — reads as a surface. Note the threshold has to grow with the field
  frequency, because the comparison is a directional derivative and its scale goes
  up with it.

Raising the sprite's `glow` to fit the lobes broke an invariant that had been
carried by a coincidence: the corona branch used to be unreachable for everything
except stars, because every planet's glow was exactly 1. An asteroid now has 1.3,
so that guard tests `isStar` explicitly — otherwise an airless rock grows a warm
corona, which is the exact thing an airless rock must not have.

The check measures the spread of the outline's radius with angle, which a circle
cannot have by definition. Counting dark patches was tried first and cannot work:
a crater also produces dark patches, so it cannot distinguish a lumpy outline from
a smooth one with holes in it. `terran` is the control — same renderer, same
lighting, same dither, and a spread of 0.000.

### Star rays are sampled in polar space

The reference builds its prominences by sampling noise at `(radius, angle)` and
thresholding it against a bound that **rises with radius**. An arc cut off at a
radius is a ray. That shape cannot be produced by sampling noise in Cartesian
space, which is what the previous four `cos^40` lobes were in effect — and on
screen they rendered as a hard white cross ruled across the disc, which reads as a
lens artefact rather than as a star. Four is also simply the wrong number; a
star's limb is crowded.

Getting the coverage right needed arithmetic rather than taste, and it is worth
recording because two attempts of eyeballing it were wrong in opposite
directions. `fbm` here is **not normalised**: three octaves sum to 0.875 with mean
0.4375 and standard deviation near 0.17, so after the 1.6 multiplier the field has
mean 0.7 and standard deviation 0.27. Subtracting 0.15 therefore left **43% of all
angles** producing some ray, each a short one, and half the disc went cream.
Subtracting 0.4 fixed the mean reach and not the spread — and the spread is the
number that decides whether a ray is visible, not the mean.

Two unrelated things were also washing the star out, and both were found by
looking rather than by reasoning:

- A second, wider "warm" zone lightened the inner 71% of the disc toward the top
  palette entry. The granulation's own light bands were already close to that
  entry, so it took the contrast out of the surface.
- The hot core was a **hard-edged white disc at 53% of the radius** — the only
  boundary in the file that was not dithered, and the only one anyone would have
  called a bug. It is now small and dithered at the edge.

No assertion was added for the rays. Their extent is a taste parameter, and the
one measurement I could construct — the radius at which near-white pixels stop,
which a dithered edge should make vary — cannot tell a dithered core from a hard
one, because the granulation scatters near-white pixels either way. A check that
cannot fail is worse than none.

### Gas giants are banded after all

This file spent a while convinced they were not, and the mistake is worth
recording because it survived a round of screenshots. It came from reading
`GasPlanet.gdshader` — which genuinely has no bands, being a cellular cloud field
sampled directly — and then *not checking the conclusion against the other
shader*, because the swirls it produced looked plausible. `GasPlanetLayers.gdshader`
settles it in a comment: `// a band is just one dimensional noise`. It samples fbm
in v alone and then multiplies the turbulence by `pow(band, 2.0) * 7.0`.

So the band term does a specific job: it makes the weather **coherent in latitude**.
Without it, turbulence displaces the boundary by the same amount everywhere and
the result is random mottle. With it, the displacement is strong in some latitudes
and weak in others, and the eye gets long stripes with storms tearing across them.
That is the whole difference between a gas giant and a bowl of soup.

Two tuning errors, both from making the weather too strong:

- At amplitude 1.7 the displacement reached half a band width almost everywhere,
  so the bands were scrambled into broad diagonal patches. Coherence cannot come
  from a displacement large enough to destroy the bands on its own.
- The ramp ran cream to dark brown and back, which is four very distinct stripes
  reading as continents. The darkest entry is a belt, not half the planet.

A consequence worth noting: the branch no longer darkens its own far side. The old
version banded by distance to the light, which doubled as the terminator; latitude
bands do not, so the terminator had to be put back explicitly for this kind.

The check measures the ratio of vertical to horizontal colour change, because
bands of constant latitude change fast going up the disc and slowly going across
it. Two things were needed to make it work. The central 60% only, since the limb
is where the sphere projection compresses everything. And **dither off** — with the
ordered dither on, its per-pixel variance is equal in both directions and swamps
the structure completely: the first run returned 1.04 for a visibly banded gas
giant and 1.00 for a star, which is what a measurement of noise looks like. A star
is the control, and it has to come out *below* the threshold, which is a stronger
control than a second type that merely agrees.

### Ice world melt lakes

The reference's ice world is two instances of the **same** surface shader at
different thresholds, composited: the sheet, and the melt water coming through it.
One threshold cannot produce that, because one threshold gives every body of water
the same size and the same edge complexity — which is why this type used to look
like a world that was half ocean rather than a world with lakes on it. The second
field is decorrelated by frequency as well as by seed, so the ponds do not all
gather along the same coastline.

The lakes reuse the **banded** sea colour rather than a flat blue. They are water
in this lighting model, and a flat fill arrives unlit — bright holes on the night
side, which is the exact artefact the banding exists to prevent.

The check asserts water **area**, and that was decided by measurement. Counting
connected bodies was the obvious thing and it is not reliable: across five seeds
the count went 9/7, 7/8, 17/11, 38/17, 76/6, and on one seed it went *down*,
because where the main field already has water the ponds merge into it instead of
adding to the count. Area is unambiguous in every case — 20×, 200×, 1.3×, 2.7×,
27× — so the threshold is 1.2×, which the weakest seed still clears.

So the fragmentation claim, which is the visually interesting one, is **not**
asserted. It is real and visible; it just is not a property that holds across
seeds, and a check that fails on a legitimate seed is worse than no check.

### Ring systems are geometry, not pixels

The reference bakes rings into the planet texture, on a canvas three times the
planet's resolution and six times its radius. Wrong here for three reasons, and the
first two are about the map rather than the overlay:

- **At map size a baked ring is mush.** Markers are 16–40px. Six times the radius
  is a grey smudge at that size, and 200px of generated pixels per system to
  produce a smudge is not affordable across twenty-odd systems.
- **It makes the sprite non-square.** Every sprite is `d` on a side and each marker
  sizes itself from that, so a ringed world would have to report a different box
  and every consumer would need to know.
- **A ring is an ellipse.** It wants to be vector, and both consumers are already
  SVG — so it is sharp at 16px and at 200px with one path and no pixels.

The far half is emitted **before** the sprite and the near half **after**, and the
sprite does the occluding for free: it is opaque across its disc and transparent
outside it, so the far half is hidden exactly where the planet is.

Three bugs here were each invisible until something other than a screenshot looked
at them:

- **A division that falls outside a half removed that half entirely.** The test for
  "the gap does not touch this arc" returned *empty* instead of the full arc — the
  opposite of correct. Since a gap is in one half or the other, this deleted the
  far side of most rings. It still looked broadly ring-shaped; only counting the
  path elements gave it away.
- **`hash2`'s `period` is for making noise tile, not for decisions.** It reduces its
  first argument modulo `period` before mixing, so `hash2(seed, 5, 32, 91)` has
  **thirty-two** possible outputs no matter how many seeds you throw at it. The
  measured distribution was 9/16/16/13/9/16/3/3/3/13 and 43.8% of seeds fell below
  a 0.34 threshold. That is not a slightly biased hash, it is a 32-valued hash
  wearing a uniform one's clothes. At 2^20 it is flat to 0.2%.
- **The ring has to fit its host.** The overlay's panel leaves 34px of headroom
  either side of a 200px planet, so the ring's outer radius must stay under 1.34x
  the planet's own; the first ratio put it at 330px and clipped it at both edges.
  Map markers have no panel, so they get a proportionally larger ring — the two
  consumers have opposite constraints and one constant cannot serve both.

`StarSystem.rings` is a model field, not a derived value. It works as a hash of the
id, and did at first, but a ring is lore: someone looking at a named giant has to
be able to write "this one has rings" and have it stick. Same reason
`DescriptionKey` exists and is empty everywhere. Undefined means "let the renderer
decide", so a hand-written fixture need not annotate every world.

### A ring is also where differential rotation is affordable

See the note on parallax below.

### Differential rotation between layers: why the cloud deck shears instead

The reference gives every layer its own time multiplier — land at 0.02, cloud at
0.005, a ring at 314× the gas giant's. Ours gives the ground and the deck the same
rate and shears the deck by `CLOUD_SHEAR * sin(spin * TAU)`. That is a deliberate
substitution, and the arithmetic is worth writing down.

A full turn has to land exactly back on the start, and the strip is stepped through
with CSS `steps()`, so frame 0 and frame 24 must be the same image. Any layer whose
rate is not a whole number of turns therefore leaves a seam once per loop. A cloud
deck genuinely slower than the ground is rate 1/3 — a third of a turn — which does
not close. The alternatives are all bad:

- **Rate 1/3 and a loop three times longer.** This works: three ground turns and
  one cloud turn per loop, so both close. Measured cost at 200px, dpr 1: 24 frames
  is 915ms and 789KB; 72 frames is **2572ms and 1158KB**. A 2.5 second generation
  for one panel, per system, on a click. The deferral that makes 915ms tolerable
  does not make 2572ms tolerable.
- **An integer rate of 2 or more.** Closes, but the deck laps the ground, which is
  not weather.
- **A sine of the phase.** Periodic with the loop by construction — the deck runs
  ahead through the middle of the turn and falls back by the end — so the seam is
  exactly zero. It also produces the *appearance* of differential motion, because a
  viewer reads a deck that leads and then trails as faster and slower than the
  ground. That is what ships.

So the substitution is not a simplification of the reference's idea, it is the only
version of that idea that both closes and fits in a frame budget. `check-dom.mjs`
asserts the seam is zero.

**The one place a genuinely different rate is affordable is the ring**, because it
is SVG rather than baked pixels. A flat annulus rotating about its own centre is a
no-op, so nothing is lost by not animating it — but a ring carrying a few radial
divisions would show its rotation, and a rotating division pattern against a
fixed planet is exactly how ring rotation is observed in reality. Not implemented:
the chart is deliberately static (only the overlay animates), and a 16px map ring
has no room for a visible gap. It is the obvious thing to add if the overlay ever
wants to show it off.

### A black hole is a kind, and it is geometry

`SystemKind` gained `"blackhole"` rather than a `collapsed?: true` flag on `"star"`.
Almost everything that branches on that union would otherwise be quietly wrong: a
collapsed system is not a star, it has no planets, and the sprite pipeline would
hand it a star. In the union, the sprite gate, the hit radius and the overlay's own
description each have to handle a case that genuinely differs.

It is **not** a `PlanetType`. A planet is something that can orbit something; a
singularity has nothing, and putting it in `planet.ts` would mean every consumer of
the sprite pipeline had to learn that one entry is not a world. It is drawn like the
ring — `BlackHole.tsx`, reusing `ringHalf` — and reuses that path builder because an
accretion disc *is* an annulus seen at a shallow angle.

Two things about it are not decoration:

- **The horizon is not `#000`.** The page behind the chart is near black, so a true
  black disc is not a black hole, it is a hole in the chart — the exact failure the
  star corona exists to prevent, arrived at from the opposite direction. What makes
  it legible is the light *around* it: a photon ring at the horizon's edge (measured
  luminance 229 against the horizon's 7) and the disc outside that.
- **The disc's near and far halves differ in tone.** The approaching side of an
  accretion disc is brighter and blueshifted, and that is the strongest cue that
  the thing is rotating. A symmetric annulus reads as a ring, and a ring reads as a
  planet with rings.

### HTML does not render inside an SVG `<g>`

The first version of `BlackHole` was a `<div>` with `<div>` children, mounted inside
the chart's `<g>`. It is correct in the overlay panel and it produced **nothing at
all** on the chart: every box measured 0x0, no error was raised, and the landmark
was simply absent — at the one size where it most needed to be seen. A screenshot
of that area would have shown empty space and read as a placement problem.

So the shapes are exported separately from the placement: `BlackHoleShapes` is the
only description of what a black hole looks like, and the component plus the `<g>`
in `Chart.tsx` are a few lines of positioning each. The duplication that remains is
positioning, which really is a property of the host — an HTML panel and an SVG
chart cannot be positioned the same way. `check-dom.mjs` asserts the marker's box
is non-zero, because the failure was silent and a screenshot cannot see it.

### The black hole disc is a preimage, not a shape

The obvious reading of "black hole" is a dark circle inside a flat annulus, and that
is what the first attempt was. It looked like a diagram.

The reference does not bend an ellipse. It builds one in a space it has displaced,
and draws wherever the *displaced* coordinate lands inside the annulus — so what
you see is the **preimage** of an ellipse under a non-linear map. The displacement
gives the upper half of the sprite `+bump(distance_from_centre)` and the lower half
`-bump(...)`, where the bump is 1 at the centre and falls to 0 at the rim. The two
halves are pulled apart in opposite directions, hardest where the disc is closest to
the singularity, so the annulus opens into a twisted structure with a hole in the
middle.

Two consequences, and they decided the architecture:

- **It cannot be SVG geometry.** A ring's boundary is an ellipse and is a path. This
  boundary is the solution set of a nonlinear equation. Approximating it with warped
  control points gives something nearly right that reads as nearly right, which is
  worse than either extreme.
- **It cannot be flat fills.** The disc is `pow(fbm(...), 0.5)` — textured, and the
  texture rotating against a fixed shape is most of what makes it read as material
  in orbit. At the frequency this started at (about five cells across the whole
  structure) it came out as a smooth cut-out bar with a gradient in it; at eighteen
  it reads as gas.

So it is baked into a sprite like a planet, and `BlackHole.tsx` is only the two
hosts that place it. The rotation is a second lever on the same axis: the shape is
fixed and only the texture turns.

`lib/paint.ts` was extracted for this — the noise, colour and dither primitives
that `planet.ts` and `blackhole.ts` both need. Importing them from a module called
"planet" would have been a lie about what that module is.

### Two bugs a single screenshot could not have found

**The gas giant's white ball.** The type carried a separate cloud deck at `0.44` on
top of a surface that is *already* painted from a cellular-displaced turbulence
field, so for a gas giant the surface is the weather and the deck is a second,
redundant one. It covered roughly half the disc in white — and because the cloud
field is seed-dependent, that is not a uniform wash but a lottery. Measured on two
seeds at 128px: one gave a correctly banded giant whose most common colour was the
palette's own cream at 7%, the other spent **28% of its pixels on near-white** and
read as a blank ball. Both were "working". A third of the reason it survived is
that a screenshot of the lucky seed looks perfect.

The assertion is therefore over **five seeds**, on the largest share taken by any
single colour and on the spread of the opaque tones. A blank ball has one colour at
~50% and no spread.

**The asteroid's detached arc.** The silhouette field was multiplied by the polar
damping factor. That factor exists to stop the *sphere* projection from aliasing
near the limb, where `v` compresses and a pixel row crosses many noise periods at
once. The silhouette is sampled in the disc plane and never touches that
projection, so there was nothing to damp — but damping pins the field to exactly
0.5, and the threshold `0.02 + 0.5r` crosses 0.5 at r ≈ 0.96. So the rock stopped
at about 60% of the radius and then came back as a thin detached arc along the
bottom of the sprite, which reads as a rendering fault rather than as a lump.

Caught by connectivity, after one erosion. The erosion is not optional: the
silhouette's outer boundary is dithered, like every other edge in the renderer, so
the raw opaque set is a solid body ringed by hundreds of isolated single pixels and
counts as 300–500 components. Eroding once removes the fringe — and removes the
polar arc too, since that was a thin dithered arc rather than a solid shape. One
operation discards both the noise and the bug.

A "solid enough" share was tried alongside it and reported 0% for every seed
including obviously solid rocks, so it was measuring something other than what its
name said. It is gone rather than left in place looking like coverage.

### The overlay is live, and that was a technology problem not a budget one

The baked filmstrip was correct about the shape and wrong about two other things,
and both of those turned out to be properties of the technique rather than things
to tune:

- **Smoothness is capped at `frames / period`.** A strip played with `steps(n)`
  cannot be smoother than `n` frames, and at 48 frames over 6s that is 8fps. More
  frames cost linearly and do not raise the ceiling — 60fps is a different
  technology, not a bigger budget.
- **The wait scales the same way.** 48 frames of a 200px body is a synchronous
  pixel loop, so the panel sat on a still for 1.5–3.1s. Measured 41ms to first
  moving pixels live, against a multi-second wait baked.

So the black hole is now rendered by a WebGL shader (`lib/gl.ts`), which is the
reference's arithmetic rather than a description of it: same statements, same
constants, so the shape is the shape and not an approximation of it. Measured 11
of 11 successive animation frames differ, against 8 of 96 for the strip.

Two things that came out of doing it live:

- **The annulus test has no inner cut.** It evaluates to about 0.2 at the centre and
  1 at the rim, so on its own it describes a *filled flat ellipse*, not a ring. The
  thin ribbon is what survives the alpha cut, and the fbm is what decides where.
  Boosting the noise to make the band look brighter is exactly backwards: it pushes
  more of the ellipse over the cut and fills it in. The first live frame came out as
  two solid leaves for that reason.
- **`discScale` had to go back to 1.** It was 0.72, reduced because the shared sprite
  clipped a 40° tilt — which is what closed the disc into a lens. At full extent the
  warp separates the two halves into crossing strands, which is the reference's
  topology and was the thing four rounds of baking could not reach.

The baked path is now the **fallback**, not the primary: `blackHoleGL` returns
`null` when there is no context and `BlackHole.tsx` uses the strip. The still is
drawn underneath at all times — one frame, about 30ms — so the panel is never blank
while the shader compiles and never flashes if WebGL turns out to be missing.

### A strip cannot be smooth, and a check that cannot fail is worse

The strip's smoothness was asserted as "under 200ms per frame". That threshold is
an apology: 200ms is 5fps, the strip was 8fps, and the only honest way to state the
requirement was a number loose enough to pass. The live path is asserted instead as
**"differs on essentially every animation frame"**, measured by reading the canvas
back and comparing successive frames — 11 of 11. Freezing `u_time` puts it at 0 of
11, so it can fail.

### Map markers turn

Every world on the chart rotates, the way the reference's preview does. This reverses
an earlier decision that the map should be static, and it is worth recording what
made it affordable when it was not before.

The strip is shown through a per-marker `clipPath`, and **its first frame has to
start at the clip rect's own left edge** — which is `P.x - size / 2`, not `P.x`.
Starting it at `P.x` put the window half a sprite to the left of the strip, so every
marker showed empty space down one side and half a world down the other: a planet
visibly cut in two with a seam down it. The step between frames was already exactly
`-size`, so the animation looped perfectly and `calcMode="discrete"` did its job —
every property the existing check was asserting held. What had to be pinned is the
*alignment*, and "the offsets are a decreasing arithmetic sequence" passes on the
broken version, because it was one. This is the second time on this project that a
loop closing was mistaken for a loop lining up, and the first time was a sprite that
never turned at all.

The cost is a filmstrip per system, and the earlier reasoning against it was that a
16px sprite that turns costs twelve times the pixels and shows nothing. That is true
of the *pixels* and wrong about the *total*: a map marker is 16–40px, so twelve
frames is twelve times 30×30 = 11k pixels per system, and the whole chart animates
for about 50ms. It was never expensive. What made it feel expensive was the overlay's
200px strips, which are a different problem entirely.

Twelve frames at **dpr 1**, and the dpr is the load-bearing part: rotation hides
resampling, and at this size nobody can see the resolution of a strip that is on
screen for four seconds. Quartering the pixels is what takes 50ms rather than 200ms.

The mechanism is **SMIL, not CSS**. An SVG `<image>` has no background to step, so
`background-position` with `steps()` — which is what the overlay uses on an HTML
`<div>` — is not available. `calcMode="discrete"` is the SVG-native equivalent: the
image is `size * frames` wide, a per-marker `<clipPath>` is `size` wide, and
animating the image's `x` through `size`-wide offsets walks the strip one frame at a
time. No extra layer, no restructuring, and the offsets ascend so the last frame is
followed by the first, which is the loop closing.

`prefers-reduced-motion` is honoured, and it is the reason this is a function rather
than a constant: twenty permanently rotating markers is exactly what that setting
exists for.

Reading the animation back needs `x.animVal.value`, not `getAttribute("x")`. SMIL
overrides the *presentation* value and never touches the attribute, so the attribute
reads the authored number forever and a working animation looks dead. That cost a
round of false negatives before it was spotted.

The checks assert the wiring rather than the motion. Waiting long enough to watch a
frame change means either a slow test or a period short enough to be a lie about how
the chart behaves; what can silently break is structural — a missing clip window, a
missing `<animate>`, or an image `size` wide instead of `size * frames` wide, which
renders frame 0 forever and looks exactly like a still.

### Rotation: three separate things were wrong

The overlay looked frozen. It was not one bug.

**The black hole's strip was invisible.** It was mounted under `class="blackhole"`
while the stylesheet only reveals a strip under `.world.turning`, so it sat at
`opacity: 0` and the animation ran on an element nobody could see. Every signal said
it worked: `background-position` advancing, `playState: running`, the strip present in
the DOM. Only reading the computed opacity showed otherwise. A rotating element
nobody can see is the worst failure available to a component whose only reason to
exist is that it rotates, and the fix is to not keep a second parallel set of class
names for the same thing. It now uses `WorldSprite`'s contract verbatim.

**The period was so long it read as static.** 48 seconds a turn over 24 frames is
two seconds a frame. That animates, and it looks like a slideshow. A viewer checking
whether a thing turns gives it about a second. Planets are now 15s over 28 frames
(536ms a frame) and the black hole 11s over 26 (423ms).

**And the gas giant was animating without appearing to move at all.** This is the
interesting one. Two frames 1.6s apart differed by **89 pixels out of 57888**, while
the black hole's differed by 1833. The cause is structural: a band of constant
*latitude* is invariant under a shift in *longitude*, so the palette index came back
identical for the same pixel on every frame. The only longitude dependence was the
turbulence's contribution to where a band *starts*, which moves a boundary without
changing which band a pixel is in — and the bands dominate the image, so almost
nothing changed.

The fix keeps the band structure, which is the thing worth having, and moves the
*tone within* each band using a field that varies in both axes. The reference gets
this for free: its palette comes from `disk + light_d` where `disk` is a full 2D
field, so its tone moves with the weather. Ours was reading the band index and
nothing else.

The swirl is deliberately low-frequency with a high threshold. The first attempt used
a high frequency at 0.07 — about 0.4 of a standard deviation — and shifted the tone
of 60% of the disc, which broke the two properties that make the type read as a gas
giant at all: the largest single tone went from 30% to 43% of the disc, and the
bands' vertical anisotropy collapsed from 1.48 to 1.14. At a low frequency and a
threshold near one deviation it forms a few coherent patches along the bands, which
is also what a storm on a gas giant is, and the stripes survive underneath. Measured
after: frame-to-frame difference 19% (was ~0), anisotropy **2.26** (was 1.48).

The check for this decodes the strip and compares frame 0 with frame 1. The DOM can
only report that an animation is *scheduled*; it cannot report that the result looks
like motion, and on this bug every DOM-level signal was green.

### No world in this project ever turned, and every check passed

`<Show>` in Solid hands its child an **accessor** unless you write `keyed`. The
overlay's sprite host did not:

```tsx
<Show when={sheet()}>
  {uri => <div class="world-turn" style={{ "background-image": `url(${uri})` }} />}
</Show>
```

so `uri` was a function, the inline style was `url(() => uri)`, and that is not a
background image at all — `background-image` computes to `none`. The element
rendered, the animation ran, `background-position` advanced, `background-size` was
correct, `playState` was `running`, and the strip was **invisible**. What the viewer
saw was the still sprite behind it: a planet that never moved.

The black hole worked because its component was written with `keyed`. Same CSS, same
period, same everything else — one missing word.

Every check written for this passed for the entire time it was broken, and that is
the part worth keeping. They asked whether generation was **deferred** and whether
the loop **closed** — both properties of the strip, and the strip was being built
perfectly. Nothing asked whether it was **visible**, and the one check that did look
at visibility asserted `opacity > 0.9`, which a fully transparent element passes
just as readily as a correctly opaque one. An element with no background image and
an element showing a planet are, to that assertion, the same element.

So the visibility check now decodes the strip's own `background-image` and compares
its intrinsic width against `size * frames`. It answers what is *in* the element
rather than how opaque it is, and it also catches a strip that is present but
shorter than it claims — which `background-size` will happily paper over, since that
is set from `px * frames` rather than from the image.

### Smoothness is a frame budget, and the budget is the explanation

The reference is smooth and this was not, and the reason is structural rather than
tunable. Their planet is a fragment shader: the noise is evaluated per pixel per
frame, at whatever rate the browser paints. A baked filmstrip's smoothness is
*frames divided by period*, and the frame count is bounded by what can be generated
without stalling the page.

Measured here: a 200px frame at dpr 1 costs about **32ms** (8 frames 268ms, 24
frames 769ms, 28 frames 911ms). The overlay ran 28 frames over 48s — **1.9fps, a
12.8° step** — which is why it read as a slideshow. It is now 96 frames over 12s:
**8fps, a 3.75° step**, which reads as a turning planet. The black hole is 48 over 6s,
the same 8fps, and its renderer is cheaper.

Getting to 96 frames meant the strip takes about **3.1 seconds** to build, which is
not something to do inside a click. `planetSheetAsync` spreads the work across
macrotasks in batches, so the page stays responsive and the still — which is drawn
at full resolution and is correct on its own — is on screen the whole time. The
strip swaps in when it is ready. The overlay therefore shows a still for about three
seconds after the first visit to a system and animates immediately on every visit
after that, since the strip is cached.

**This is still stepped, and 8fps is not 60.** The honest ceiling: to be genuinely
smooth the planet has to be evaluated per pixel per frame, which for us means a
WebGL port of the renderer for the overlay alone — the baked sprite would still
serve the chart and every still. That is a real piece of work and it is the only way
to close the remaining gap, so it is a decision rather than a tweak. The check
threshold is 200ms a frame, deliberately tightened from the 1000ms it started at:
1000ms is what the 28-frame strip produced, and a threshold that accepts the thing
you are complaining about is not a threshold.

### The black hole: transliterate, do not paraphrase

Comparing side by side with the reference made it obvious that ours was wrong —
theirs is a ribbon that tapers to a point at each end, ours was a slab of uniform
mid-orange with rounded ends. What took four rounds was establishing *why*, and
every round it was a transcription error rather than a design error:

1. **`smoothstep(d, outer, inner)` does not peak at 1.** The reference calls it with
   its edges reversed, which GLSL leaves undefined and which computes
   `clamp((inner - d) / (outer - d))` — so it peaks at `inner / outer` and is
   **already zero by `inner`**. It is not a plateau. Ours was a plateau at 1.0 out
   to `inner`, falling to 0 at `outer`, so the disc was displaced by a whole sprite
   height where theirs moves it 0.4. That is what tore the annulus into a
   disconnected bar and a detached arc.
2. **The displacement ramp must be measured in the same frame as the geometry.**
   Ours computed it from the distance to the sprite centre *before* the rotation
   while the geometry used the rotated coordinate. Two frames, one image.
3. **The band's base width is 0.1 and the warp adds up to 0.6 near the centre**, so
   it varies by an order of magnitude across the disc. Ours used a thin constant
   band on the reasoning that a narrow band looks like a ribbon — it does, but a
   ribbon of constant width with rounded ends reads as a cigar. The taper *is* the
   variation.

The fix for all three was to stop paraphrasing. `renderFrame` is now a line-by-line
transliteration of the reference's `fragment()`: same statements, same order, same
constants, with the two deviations marked in place. Rewriting a 40-line shader in
your own shape is a good way to make the same mistake three times.

Two more, which are structural rather than arithmetic:

- **The horizon draws over the disc.** Ours had the disc on top, on the reasoning
  that a ray crossing in front of the void is the depth cue. It reads correctly and
  renders wrong: the ribbon cut the photon ring in half and left no bright edge to
  read the hole by. The reference gets its depth from the warp — far side displaced
  past the horizon, near side not — which does not need the disc to occlude
  anything, so the occlusion bought nothing and cost the one feature that made the
  hole legible.
- **The disc needs its own canvas to be tipped.** The reference's disc canvas is
  three times the horizon's, so a 0.7 rad tilt has room. Ours shares one box, and at
  full width a 40° tilt swings the ring's ends clean off the sprite. Hence
  `discScale` 0.72 and a gentler tilt.

### A ring needs an interior

One flat band has nothing for the eye to model, so it sits on the disc like a
sticker — which is exactly how it read on a cream gas giant. Three concentric
slices in three tones is the cheapest thing that says "layered", and the middle one
is darkest because that is how a ring reads (a shadowed gap between two lit faces)
and because it separates the ring from a planet of any colour. Both the overlay and
the chart markers use the same helper, so they cannot drift apart.

The tone ramp had it backwards first: outer and middle both dark, inner light. That
is a dark wire hoop traced over the planet — a line drawing, not an object. A ring is
mostly light with a groove in it: lit outer face, dark gap, brightest inner face.

### `above` is the upper arc, and the near half is the lower one

`ringHalf(g, above)` offsets its sweep by `Math.PI` when `above`, which is the
**upper** arc. The near half — the one drawn in front of the planet — is the lower
one, because in an SVG's y-down frame a point nearer the viewer sits lower.

`WorldRing` passed `side() === "front"` where it wanted `"back"`. So the half drawn
in front of the planet crossed its **upper** third and the near half sat behind,
which reads as a hoop drawn over the top of the disc rather than one passing round
it. The chart's own markers were always right, which is why only the overlay ever
looked wrong and why the two had to be compared side by side to notice.

### One radius for both axes, and a whole half cannot detect it

`ringHalf` builds its points with a helper that took a single radius and applied it
to both x and y. For a whole half that is undetectable: at `t = 0` and `t = PI` the
sine is zero, so `rx * sin(t)` and `ry * sin(t)` are the same number and the mistake
has nothing to act on. Only the halves carrying a division were malformed — and since
a division falls in one half or the other, roughly half of every ringed world was
drawn with a band ballooned past its own bounds.

The endpoint error scales with `rx`, not `ry`: at `rx = 128`, `ry = 28` an arc
ending at `t = 1.2` came out at `y = 59` where the ellipse's own lowest point is 28.
SVG does what its spec says and scales the radii up until the arc fits, so the band
inflated to more than twice its size. That is not a thing anyone looks for in a
screenshot, because the ring still read as a ring.

The invariant is now checked without a browser: **every point of a ring path lies on
one of its own two ellipses**, across 288 arcs spanning four geometries and a sweep
of the band's width. Reverting the fix puts all 288 off.

## Layout

| Path | What it is |
|---|---|
| `WorldSprite.tsx` | One generated world, still-then-turning. |
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
- **Solid's `<For>` puts every marker in one `<g>`, so a parent-scoped DOM query
  finds the first marker on the chart.** The ring-ordering check asked
  `far.parentElement.querySelector("image")` and got a planet sixty systems away,
  then reported that the ring was drawn behind its own world. Sibling navigation
  (`nextElementSibling`) is exact and immune to this.
- **A hash's tiling parameter is not a free parameter.** `hash2(ix, iy, period,
  seed)` reduces `ix` modulo `period` so that noise tiles seamlessly. Reach for it
  as a general-purpose hash and the output space collapses to `period` values. It
  looked uniform, it was not, and no screenshot would ever have shown it.
- **An option that changes pixels but not the cache key is invisible.** The sprite
  cache key was a hand-written list of options, and `suppressCraters` was added
  without being added to it. A call asking for a crater-free planet got the cached
  cratered one, and the test written to catch exactly that reported that craters
  had no effect when they plainly did. The key is now derived from the option
  object's own sorted keys, so an option that does not exist cannot change the
  output and one that is added cannot be forgotten.
- **A cross-type control cannot isolate a feature.** The crater check first
  compared an airless world against a world with weather. The rainy world scored
  *higher*, because the metric was counting coastlines and rivers — any dark region
  beside a light one. The control now differences the same world against itself
  with the feature suppressed, and a world with no craters to suppress must come
  back byte-identical. That is an exact control rather than a statistical one.
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

## Why it looked cheap, and why that was a rule violation

Everything else on this chart with noise in it is WebGL -- the ring, the accretion disc,
the background. The jet was SVG gradients on a polygon, so **nothing in it was carved by
anything**. A gradient-filled trapezoid has no interior, so there is nothing in it to
look at. It read as cheap next to the disc because the disc has turbulence in it and the
beam was a rectangle.

So it is `lib/gl-jet.ts` now. Four things a polygon cannot do:

- **Anisotropic interior.** `fbm(vec2(across * 240.0, at * 5.0))` -- about four noise
  cells across the beam, several along it. Plasma is stretched by the same acceleration
  that collimates it, so the texture is drawn out into filaments.
- **A wandering axis.** `axis(t)` from low-frequency noise. A real jet precesses and
  wiggles; a perfectly straight one is a ruler.
- **Soft ends, and a taper.** The SVG had to *cut* the polygon at the tip, and a cut is a
  cut at every resolution. The envelope goes to zero AND narrows the beam, so it comes
  to a point -- fading only the brightness gives a bulb, because the gaussian profile is
  widest at the tip and a dim wide end reads as a lozenge.
- **A shock that swells.** The travelling knot used to be a moving `linearGradient`. Now
  it is a gaussian in `t` that also multiplies the beam's half-width, so it is a
  brightening and a swelling at once.

Both poles come out of one loop over `abs(t)`, so they are antipodal **by construction**
rather than by two sets of mirrored constants that can drift apart -- which was the bug
the SVG version needed three rounds to shake.

The SVG stays underneath as the fallback, with its masks and its travelling knots, because
a fallback that is visibly worse than the thing it replaces is a regression rather than a
degradation. It does not attempt the interior, the wandering axis or the taper.

### Units, and a fault that looked like a broken shader

`JET` was first written as fractions of the panel's **half**-width, where the SVG's had
been fractions of the **whole** panel. The beam came out twice as long and ten times too
wide, ending at the canvas edge with a flat white interior. Both surfaces now share one
table, and `check-galaxy.ts` asserts the proportions as constants -- the browser's
geometry assertions read the SVG path data, which is the *fallback* now and hidden
whenever WebGL exists, and a number checked only on the fallback is a number the shader
can drift from unnoticed. That is exactly how the two disagreed about `reach`.

The interior was invisible for the same class of reason: the noise was sampled at
`across * 12.0` over a beam 0.02 wide, a range of 0.24 -- less than one noise cell, so
the whole beam received a single flat value.

The shader also needed `precision highp float;` declared ahead of the shared prelude,
which does not declare one. Without it the compile failed on the *prelude's* uniforms
before reaching a line of ours, and the log pointed at line 2 of a file whose line 2 is
somebody else's declaration. It was caught because the fallback was there and the panel
was not blank.

### Three instruments for the knots, and one assertion removed rather than shipped

| instrument | measured | why it cannot work |
|---|---|---|
| centroid of the whole jet | 1.2px | a symmetric envelope has a fixed centre of mass, so a bulge crossing its middle cannot move it |
| centroid of the brightest tenth | 0.5-2.6px | the base is brighter than any knot crossing it, so it *is* the brightest tenth, permanently, and its position is pinned |
| frame at which each pole peaks | both frame 0 | the count is dominated by a constant base, and frame 0 wins by noise |

**"The two poles are not in step" is asserted by nothing right now.** Doing it properly
needs a per-row maximum well away from the base and about sixty samples to resolve a
fraction of the 1.6s period. Shipping the assertion with a threshold it happened to pass
would report a guarantee that is not being made, which is worse than the gap. It is
written down here instead, and the property is visible in a screenshot.

## The star: `lib/gl-star.ts`, a WORK IN PROGRESS

Transcribed from `cosmoglyph/shaders/star.glsl` v5, Luke100000, MIT. The author's words
in the itch.io comments, unprompted: *"feel free to extract the shaders, or code in
general."* No per-shader headers in the `.love`; the licence is at the repository level.

We had **no star at all** -- `SystemKind` covers planets and remnants, and a chart of a
galactic arm is mostly stars. This is the first attempt and it is not finished.

### What is transcribed and working

- **Palette-INDEXED and dithered.** Every pixel picks an index into a small palette and
  the coverage mask is a `bayer4` threshold with a `discard`. The cell-shaded look is
  not a filter applied afterwards; it is how each pixel is decided. Ours lerps RGB, which
  is why ours reads as smooth next to this.
- **Granulation**, at 150 cells around the sphere, scrolled at a rate set by `activity`.
- **Spots**, carved out of a third noise channel.
- **The corona**, with four harmonics of the screen-space direction warping its edge, and
  the band index inverted so the inner edge is the hot one.
- **Prominence loops**: 32 of them, each anchored on the sphere by a HASH rather than
  placed -- angle, depth, therefore radius, all from `hash(seed + order)` -- drawn as an
  elliptical arc with two independent wobbles and a hashed gap so none is ever a closed
  ellipse. A prominence is a loop of plasma held above the surface by a magnetic field.

### What is still wrong

1. **The granulation is too contrasty.** It reads as speckle rather than as convective
   cells. `turb`-style amplitude needs pulling back, and the dither `lift` on the surface
   is doubling the effect.
2. **The loops are not visible.** They render, but at the reference's default `flares`
   they are lost against the corona. They need to be brighter than the corona rather than
   competing with it -- which is also physically right: a prominence is denser and hotter
   than the corona it stands in.
3. **The corona edge is too clean.** The four harmonics are being applied but the ragged
   edge is not reading; `corona` needs to be higher and `width` needs more variation.
4. **Limb darkening is too strong** -- the edge goes almost black, which reads as a hole
   rather than as a cool limb.
5. **Not wired in.** There is no `star` kind in `SystemKind`, no overlay dispatcher entry
   and no chart mark. Nothing in the game can reach this yet, which is also why there are
   no assertions on it: an unwired renderer verified only by screenshot is exactly the
   failure this project keeps hitting.

### Four defects found by looking, not by reading

- **Granulation at 26 cells is continents.** The first render was a brown-and-cream
  rocky planet with a purple rim. Granulation is convective cells and there are hundreds.
- **The spot subtraction was a constant.** `map.g` is an fbm centred on 0.5, so
  `smoothstep(0.08, 0.55, map.g)` is ~0.93 almost everywhere: a near-constant offset
  that compressed the whole index distribution into the two END bands, and the two middle
  colours were never drawn at all. Hence two-tone banding. The reference's `map.g` is
  near zero almost everywhere because it was RENDERED as a spotness channel, so the
  noise here is cubed and biased down to be sparse instead.
- **`radial` substituted for the normal.** The corona's harmonics are functions of a
  *direction*, bounded in -1..1. Feeding `radial` -- which reaches ~3 at the corners --
  drove the edge term outside its range.
- **`smoothstep` with edge0 > edge1, which is UNDEFINED in GLSL.** The loop tube's
  thickness is `0.4 + (wobbleA - wobbleB)`, a sum of two sines, so it goes negative for
  part of every loop's life. The reference adds an ABSOLUTE 0.08 for the second edge, so
  a slightly negative thickness still leaves edge0 < edge1. Scaling the second edge by
  the thickness instead -- which tightening the falloff tempted -- makes both edges
  negative together. It returned 1, so `line` was 1 across the whole frame, so coverage
  was ~0.5 everywhere and the corona drew a half-tone field over the entire canvas.

  The symptom pointed at the harmonics, and at the radial substitution, and both of those
  were wrong for other reasons and worth fixing anyway. The real one was arithmetic in
  the dark.

### Backticks inside GLSL template literals

Twice in this file, and it is now the fifth time in this thread. Six of them this round,
in comments written after the first pass. A backtick inside a GLSL comment closes the
template literal and the error lands on a line of somebody else's declaration.
