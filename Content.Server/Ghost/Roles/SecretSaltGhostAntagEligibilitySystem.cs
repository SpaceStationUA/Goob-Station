// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Robust.Shared.Network;

namespace Content.Server.Ghost.Roles;

/// <summary>
/// Records whether an account has ever participated as spawned crew this round,
/// and whether the same account has completed cryostorage.
/// Scoped strictly to the SecretSalt preset; not a general ghost-role restriction.
/// </summary>
public sealed class SecretSaltGhostAntagEligibilityTracker
{
    private readonly HashSet<NetUserId> _crew = new();
    private readonly HashSet<NetUserId> _completedCryo = new();
    private int _roundId;

    public void BeginRound(int roundId)
    {
        if (_roundId == roundId)
            return;

        _roundId = roundId;
        _crew.Clear();
        _completedCryo.Clear();
    }

    public void RecordCrew(int roundId, NetUserId player)
    {
        if (roundId != _roundId || roundId <= 0)
            return;
        _crew.Add(player);
    }

    public void RecordCompletedCryo(int roundId, NetUserId player)
    {
        if (roundId != _roundId || roundId <= 0)
            return;
        _completedCryo.Add(player);
    }

    public bool CanReceiveAntagRole(int roundId, NetUserId player)
    {
        return roundId == _roundId
            && _crew.Contains(player)
            && !_completedCryo.Contains(player);
    }
}

/// <summary>
/// Server-only eligibility guard, used by both ghost-role request and takeover.
/// Do not clear state on player reconnect or when their mind is wiped.
/// </summary>
public sealed class SecretSaltGhostAntagEligibilitySystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    private readonly SecretSaltGhostAntagEligibilityTracker _history = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnRestart(RoundRestartCleanupEvent ev)
    {
        // The next round has a different ID; no player should inherit access.
        _history.BeginRound(0);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_ticker.CurrentPreset?.ID != "SecretSalt")
            return;

        // The crew spawn event may occur before InRound is entered. It is still
        // an actual spawned body, not just a lobby selection.
        if (HasComp<GhostComponent>(ev.Mob) || HasComp<GhostRoleComponent>(ev.Mob)
            || HasComp<CryostorageContainedComponent>(ev.Mob))
            return;

        _history.BeginRound(_ticker.RoundId);
        _history.RecordCrew(_ticker.RoundId, ev.Player.UserId);
    }

    /// <summary>Called only after cryostorage completes successfully.</summary>
    public void RecordCompletedCryo(NetUserId userId)
    {
        if (_ticker.CurrentPreset?.ID != "SecretSalt")
            return;

        _history.RecordCompletedCryo(_ticker.RoundId, userId);
    }

    public bool CanReceiveAntagGhostRole(NetUserId player)
    {
        if (_ticker.CurrentPreset?.ID != "SecretSalt")
            return true;

        return _history.CanReceiveAntagRole(_ticker.RoundId, player);
    }
}
