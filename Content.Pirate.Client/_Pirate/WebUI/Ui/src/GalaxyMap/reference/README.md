# Reference

Deep-Fold's PixelPlanets, MIT licensed. Fetched from
<https://github.com/Deep-Fold/PixelPlanets>, `Planets/BlackHole/`.

Kept here because the shaders declare DEFAULTS and the scene OVERRIDES most of
them, and reading the wrong one of those two is what made the black hole render as
a completely different object for a very long time:

| uniform | declared | scene sets |
|---|---|---|
| `ring_perspective` | 4.0 | **14.0** |
| `disk_width` | 0.1 | **0.065** |
| `size` | 50.0 | **6.598** |
| `OCTAVES` | — | **3** |
| `rotation` | 0.0 | **0.766** |
| `radius` | 0.5 | **0.247** |
| `light_width` | 0.05 | **0.028** |

`ring_perspective` is the one that matters: at 4.0 the disc is foreshortened four
to one and reads as a fat ellipse; at 14.0 it is a thin sweep.

Two more things only the scene says:

- **Node order is the depth cue.** `BlackHole` is index 0 and `Disk` is index 1,
  and Godot draws later siblings on top, so the disc draws OVER the horizon. That
  is what puts a near side in front of the singularity and breaks the photon ring
  where the band crosses it.
- **Sizes.** `BlackHole` spans 100x100 and `Disk` spans 300x300, concentric, so the
  horizon is a third of the canvas and its radius is `0.247 / 3` of it.

The disc's palette runs BRIGHT to DARK (`#ffffeb` → `#bd4035`) and `posterized`
indexes straight into it, so a low value is bright. The reverse reads as obvious
and is wrong.

## Gas planet rings

`Ring.gdshader`, with the values `GasPlanetLayers.tscn` gives it. Three things here
are only knowable from the source, and all three were wrong in the SVG ring that
this replaced:

| uniform | declared | scene sets |
|---|---|---|
| `ring_perspective` | 4.0 | **6.0** |
| `rotation` | 0.0 | **0.7** |
| `ring_width` | 0.1 | **0.127** |
| `size` | 50.0 | **15.0** |
| `OCTAVES` | — | **4** |

- **The ring is mostly noise, and the noise carves it.** `ring *= fbm(...)` with
  four octaves, then `step(0.28, ring)` for the alpha. So the divisions in the ring
  are where the noise fell below the cut. Three flat ribbons have no divisions,
  which is why they read as a wire hoop laid across the planet.
- **The planet's hole is the ring's own.** `if (uv.y < 0.5) ring *=
  step(1.0 / scale_rel_to_planet, distance(uv, vec2(0.5)))` — the ring canvas is
  300x300 and the planet 100x100, so `1/6` is exactly the planet's radius in it. The
  occlusion is geometry, not the sprite's transparency.
- **The ring is lit from off-canvas.** `light_origin` is `(-0.1, 0.3)`, left of the
  edge, and `posterized` adds `pow(light_d, 2) * 2`. There is a lit-to-shadowed
  gradient across it, and the shader switches to `dark_colors` outright past 1.0
  rather than continuing through `colors`. Six tones in two ramps.

`GasLayers.gdshader` is here because it is the other half of the same planet, and
because the ring's colours are chosen to sit against it.
