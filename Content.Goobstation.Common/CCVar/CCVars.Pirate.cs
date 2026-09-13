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
}
