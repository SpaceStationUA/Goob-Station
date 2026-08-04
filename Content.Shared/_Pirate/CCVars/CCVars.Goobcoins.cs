// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

/// <summary>Pirate CVar definitions for mid-round goobcoin payouts.</summary>
[CVarDefs]
public sealed class PirateGoobcoinCVars
{
    /// <summary>Sign-on bonus paid at round end. Set to 0 to disable.</summary>
    public static readonly CVarDef<int> RoundStartBonus =
        CVarDef.Create("pirate.goobcoin.round_start_bonus", 50, CVar.SERVERONLY);

    /// <summary>Maximum early-cryo penalty. Set to 0 to disable.</summary>
    public static readonly CVarDef<int> EarlyCryoPenalty =
        CVarDef.Create("pirate.goobcoin.early_cryo_penalty", 100, CVar.SERVERONLY);

    /// <summary>Minutes after pod entry over which the penalty tapers to zero.</summary>
    public static readonly CVarDef<float> EarlyCryoWindowMinutes =
        CVarDef.Create("pirate.goobcoin.early_cryo_window_minutes", 10f, CVar.SERVERONLY);
}
