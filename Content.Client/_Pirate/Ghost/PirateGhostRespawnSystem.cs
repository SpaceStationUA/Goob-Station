using System;
using Content.Shared._Pirate.Ghost;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Ghost;

public sealed class PirateGhostRespawnSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _hasStatus;
    private bool _canRespawn;
    private bool _pendingRespawnRequest;
    private TimeSpan _respawnAvailableAt;

    public event Action? StatusChanged;

    public override void Initialize()
    {
        SubscribeNetworkEvent<GhostRespawnStatusEvent>(OnGhostRespawnStatus);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public void Reset()
    {
        _hasStatus = false;
        _canRespawn = false;
        _pendingRespawnRequest = false;
        _respawnAvailableAt = TimeSpan.Zero;
        StatusChanged?.Invoke();
    }

    public PirateGhostRespawnDisplayState GetDisplayState()
    {
        if (!_hasStatus)
            return new PirateGhostRespawnDisplayState(false, false, TimeSpan.Zero);

        if (_canRespawn)
            return new PirateGhostRespawnDisplayState(true, true, TimeSpan.Zero);

        var remainingTime = _respawnAvailableAt - _timing.CurTime;
        if (remainingTime <= TimeSpan.Zero)
            return new PirateGhostRespawnDisplayState(true, true, TimeSpan.Zero);

        return new PirateGhostRespawnDisplayState(true, false, remainingTime);
    }

    public void RequestRespawnToLobby()
    {
        var state = GetDisplayState();
        if (_pendingRespawnRequest || !state.HasStatus || !state.CanRespawn)
            return;

        _pendingRespawnRequest = true;
        RaiseNetworkEvent(new GhostRespawnLobbyRequest());
    }

    private void OnGhostRespawnStatus(GhostRespawnStatusEvent ev)
    {
        _hasStatus = ev.HasStatus;
        _canRespawn = ev.HasStatus && ev.CanRespawn;
        _pendingRespawnRequest = false;
        _respawnAvailableAt = ev.HasStatus
            ? _timing.CurTime + ev.RemainingTime
            : TimeSpan.Zero;
        StatusChanged?.Invoke();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        Reset();
    }
}

public readonly record struct PirateGhostRespawnDisplayState(bool HasStatus, bool CanRespawn, TimeSpan RemainingTime);
