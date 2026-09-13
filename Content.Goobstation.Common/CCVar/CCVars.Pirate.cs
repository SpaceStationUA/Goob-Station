// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Goobstation.Common.CCVar;

[CVarDefs]
public sealed partial class PirateCVars
{
    /// <summary>
    ///     RoundLoadProbeSystem: logs round-join timeline (attach + frame stability).
    ///     Client-only; used to tune the wipe overlay parameters.
    /// </summary>
    public static readonly CVarDef<bool> PirateRoundProbe =
        CVarDef.Create("pirate.roundprobe", false, CVar.CLIENT);

    /// <summary>
    ///     Whether the round-start wipe (splash art dissolving into the live game) is enabled.
    /// </summary>
    public static readonly CVarDef<bool> PirateRoundWipe =
        CVarDef.Create("pirate.roundwipe", true, CVar.CLIENT);

    /// <summary>
    ///     Texture path of the "before" frame for the round-start wipe.
    /// </summary>
    public static readonly CVarDef<string> PirateRoundWipeArt =
        CVarDef.Create("pirate.roundwipe_art", "/Textures/LobbyScreens/toppirates1.webp", CVar.CLIENT);

    /// <summary>
    ///     Mask variant for the wipe: -1 = random, 0 = dither cells, 1 = staggered
    ///     bands, 2 = centered circle, 3 = sweep, 4 = precise stripes, 5 = art stripes.
    /// </summary>
    public static readonly CVarDef<int> PirateRoundWipeMask =
        CVarDef.Create("pirate.roundwipe_mask", -1, CVar.CLIENT);

    /// <summary>
    ///     Seconds the art holds ("breathing") before the dissolve starts.
    /// </summary>
    public static readonly CVarDef<float> PirateRoundWipeHold =
        CVarDef.Create("pirate.roundwipe_hold", 1f, CVar.CLIENT);

    /// <summary>
    ///     Dev: force a cover at attach (isolated shader timing test).
    /// </summary>
    public static readonly CVarDef<bool> PirateRoundWipeTest =
        CVarDef.Create("pirate.roundwipe_test", false, CVar.CLIENT);
}
