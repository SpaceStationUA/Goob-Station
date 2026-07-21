// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Pirate.CCVars;

public sealed partial class PirateVars
{
    /// <summary>
    /// How long players have to vote on the automatic evacuation shuttle call.
    /// </summary>
    public static readonly CVarDef<TimeSpan> EmergencyShuttleVoteTime =
        CVarDef.Create("pirate.shuttle_vote_time", TimeSpan.FromMinutes(1), CVar.SERVERONLY);

    /// <summary>
    /// Whether the automatic round-end shuttle call starts an OOC vote first.
    /// </summary>
    public static readonly CVarDef<bool> RoundEndIsOOCVote =
        CVarDef.Create("pirate.round_end_is_ooc_vote", true, CVar.SERVERONLY);
}
