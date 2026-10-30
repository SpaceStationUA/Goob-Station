// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Minutes a round must reach before it enters voting penalty history.
    /// </summary>
    public static readonly CVarDef<float> VoteMinimumPlayedMinutes =
        CVarDef.Create("vote.minimum_played_minutes", 10f, CVar.SERVERONLY | CVar.ARCHIVE);
}
