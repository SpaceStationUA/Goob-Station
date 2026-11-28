// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.GunGame.Components;
using Content.Server.GameTicking;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.GunGame;

public sealed partial class GunGameRuleSystem
{
    private void QueueRespawn(Entity<GunGameTrackerComponent> victim, Entity<GunGameRuleComponent> rule)
    {
        if (victim.Comp.Player is not {} user || !rule.Comp.PlayerInfo.TryGetValue(user, out var player))
            return;

        player.PendingRespawn = (victim.Owner, Timing.CurTime + rule.Comp.RespawnDelay);
        if (_player.TryGetSessionById(user, out var session))
            _chat.DispatchServerMessage(session, Loc.GetString("rule-respawn-in-seconds", ("second", rule.Comp.RespawnDelay.TotalSeconds)));

        ScheduleOnce(rule.Comp.RespawnDelay, () => TryRespawn(rule, player));
    }

    private void OnSessionStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame || ActiveRule is not {} rule ||
            !rule.Comp.PlayerInfo.TryGetValue(args.Session.UserId, out var player) ||
            player.PendingRespawn is not {} pending || pending.Due > Timing.CurTime)
            return;

        // Allow the ticker to finish attaching the reconnecting player's old body or observer.
        ScheduleOnce(TimeSpan.Zero, () => TryRespawn(rule, player));
    }

    private void TryRespawn(Entity<GunGameRuleComponent> rule, GunGamePlayerTrackingInfo player)
    {
        if (ActiveRule?.Owner != rule.Owner || GameTicker.RunLevel != GameRunLevel.InRound || rule.Comp.Victor != null ||
            player.PendingRespawn == null || player.Profile is not {} profile || !_player.TryGetSessionById(player.UserId, out var session) ||
            session.Status != SessionStatus.InGame || _stations.GetStations().FirstOrNull() is not {} station)
            return;

        SpawnFighter(rule, session, profile, station);
    }
}
