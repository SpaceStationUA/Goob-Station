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
