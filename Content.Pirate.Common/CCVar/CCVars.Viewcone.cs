// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using Robust.Shared.Configuration;

namespace Content.Pirate.Common.CCVar;

public sealed partial class PirateCVars
{
    /// <summary>
    /// Disable vision effect spawning like footsteps, used for integration tests.
    /// </summary>
    public static readonly CVarDef<bool> DisableVisionEffects =
        CVarDef.Create("pirate.disable_vision_effects", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether to disable vision cone overlays.
    /// </summary>
    public static readonly CVarDef<bool> DisableVisionCones =
        CVarDef.Create("pirate.disable_vision_cones", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Scale for how strong out-of-vision graininess is, 0 is just pure greyscale.
    /// </summary>
    public static readonly CVarDef<float> VisionGrainScale =
        CVarDef.Create("pirate.vision_grain_scale", 0.75f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
