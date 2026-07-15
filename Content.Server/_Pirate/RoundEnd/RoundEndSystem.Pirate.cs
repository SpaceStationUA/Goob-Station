// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate port: DeltaV OOC automatic shuttle vote.
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._Pirate.CCVars;

namespace Content.Server.RoundEnd;

public sealed partial class RoundEndSystem
{
    [Dependency] private readonly IVoteManager _vote = default!;

    public void CallEvacuationVote()
    {
        var options = new VoteOptions
        {
            Title = Loc.GetString("round-end-system-vote-title"),
            Duration = _cfg.GetCVar(PirateVars.EmergencyShuttleVoteTime),
            InitiatorText = Loc.GetString("vote-options-server-initiator-text"),
        };

        options.Options.Add((Loc.GetString("round-end-system-vote-end"), true));
        options.Options.Add((Loc.GetString("round-end-system-vote-continue"), false));

        var vote = _vote.CreateVote(options);

        vote.OnFinished += (_, args) =>
        {
            if (args.Winner == null || (bool)args.Winner)
                RequestRoundEnd(checkCooldown: false, text: "round-end-system-vote-shuttle-called-announcement");
        };
    }
}
