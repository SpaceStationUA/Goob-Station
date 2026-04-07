// Pirate port: Delta-V traits system CCVars
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

[CVarDefs]
public sealed class TraitCCVars
{
    /// <summary>
    /// Maximum number of traits a player can select.
    /// </summary>
    public static readonly CVarDef<int> MaxTraitCount =
        CVarDef.Create("traits.max_count", 10, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Maximum total trait points a player can spend.
    /// </summary>
    public static readonly CVarDef<int> MaxTraitPoints =
        CVarDef.Create("traits.max_points", 3, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether to skip the disabled traits popup on spawn.
    /// </summary>
    public static readonly CVarDef<bool> SkipDisabledTraitsPopup =
        CVarDef.Create("traits.skip_disabled_traits_popup", false, CVar.CLIENT | CVar.ARCHIVE);
}

