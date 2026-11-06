// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    // Server-only replicated settings prevent players from changing starlight.
    public static readonly CVarDef<bool> Starlight =
        CVarDef.Create("pirate.starlight", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> StarlightStrength =
        CVarDef.Create("pirate.starlight_strength", 0.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> StarlightColor =
        CVarDef.Create("pirate.starlight_color", "#8589FA", CVar.SERVER | CVar.REPLICATED);
}
