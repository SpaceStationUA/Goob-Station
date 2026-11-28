// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    public static readonly CVarDef<bool> EnableBiomagneticPolarizationSymbols =
        CVarDef.Create("accessibility.enable_biomagnetic_polarization_symbols", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> HeatHaze =
        CVarDef.Create("light.heat_haze", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> PuddleReflections =
        CVarDef.Create("light.puddle_reflections", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
