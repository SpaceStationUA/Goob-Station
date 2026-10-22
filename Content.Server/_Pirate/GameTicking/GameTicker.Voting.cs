// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Voting.Managers;
using Content.Shared.GameTicking;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    private void UpdatePirateSelectionVoteCountdown()
    {
        if (RunLevel != GameRunLevel.PreRoundLobby || Paused || _roundStartTime == TimeSpan.Zero)
            return;

        if (IoCManager.Resolve<IVoteManager>() is not VoteManager manager)
            return;

        var remaining = manager.PirateSelectionTimeLeft;
        if (remaining <= TimeSpan.Zero)
            return;

        // Do not preload a map or start a round before both ballots are complete.
        var earliestStart = _gameTiming.CurTime + remaining + RoundPreloadTime;
        if (_roundStartTime > earliestStart)
            return;

        _roundStartTime = earliestStart + TimeSpan.FromSeconds(1);
        RaiseNetworkEvent(new TickerLobbyCountdownEvent(_roundStartTime, Paused));
    }
}
