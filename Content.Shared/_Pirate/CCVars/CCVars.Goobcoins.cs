// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

/// <summary>Pirate CVar definitions for mid-round goobcoin payouts.</summary>
[CVarDefs]
public sealed class PirateGoobcoinCVars
{
    /// <summary>Set to 0 to disable the round-start bonus.</summary>
    public static readonly CVarDef<int> RoundStartBonus =
        CVarDef.Create("pirate.goobcoin.round_start_bonus", 50, CVar.SERVERONLY);

    /// <summary>Set to 0 to disable the early-cryo penalty.</summary>
    public static readonly CVarDef<int> EarlyCryoPenalty =
        CVarDef.Create("pirate.goobcoin.early_cryo_penalty", 100, CVar.SERVERONLY);

    /// <summary>Minutes after round start when entering cryo still counts as early.</summary>
    public static readonly CVarDef<float> EarlyCryoWindowMinutes =
        CVarDef.Create("pirate.goobcoin.early_cryo_window_minutes", 10f, CVar.SERVERONLY);
}
