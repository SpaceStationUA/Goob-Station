using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Common.Cloning;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared._Pirate.Ghost;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Goobstation.Shared.MisandryBox.Thunderdome;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Ghost;

public sealed class PirateGhostRespawnSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Dictionary<NetUserId, GhostRespawnState> _states = new();
    private readonly Dictionary<NetUserId, PendingGhostTransition> _pendingTransitions = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<MindContainerComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<MindContainerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<MindContainerComponent, TransferredToCloneEvent>(OnTransferredToClone);

        SubscribeNetworkEvent<GhostRespawnLobbyRequest>(OnGhostRespawnLobbyRequest);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        EnsureCrewCycle(ev.Player.UserId, ev.Mob);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        ClearState(ev.PlayerSession.UserId);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _states.Clear();
        _pendingTransitions.Clear();
    }

    private void OnMindRemoved(Entity<MindContainerComponent> ent, ref MindRemovedMessage args)
    {
        if (args.Mind.Comp.UserId is not { } userId)
            return;

        var wasInCryo = HasComp<CryostorageContainedComponent>(ent.Owner);

        if (!TryGetState(userId, out var state) || !state.HasCrewCycle)
            return;

        if (wasInCryo)
        {
            _pendingTransitions[userId] = new PendingGhostTransition
            {
                Immediate = true
            };
            return;
        }

        if (state.TimerArmed)
            return;

        _pendingTransitions[userId] = new PendingGhostTransition
        {
            Immediate = false
        };
    }

    private void OnMindAdded(Entity<MindContainerComponent> ent, ref MindAddedMessage args)
    {
        if (args.Mind.Comp.UserId is not { } userId)
            return;

        if (HasComp<GhostComponent>(ent.Owner))
        {
            ArmTimerIfNeeded(userId);
            SendStatusToUser(userId);
            return;
        }

        if (TryTrackTransferredCrewLife(userId, ent.Owner))
        {
            _pendingTransitions.Remove(userId);
            return;
        }

        EnsureInitialCrewCycle(userId, ent.Owner, args.Mind.Comp);
        _pendingTransitions.Remove(userId);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!HasComp<GhostComponent>(args.Entity))
            return;

        if (!IsTemporaryGhostProjection(args.Player.UserId, args.Entity))
            ArmTimerIfNeeded(args.Player.UserId);

        SendStatus(args.Player);
    }

    private void OnMobStateChanged(Entity<MindContainerComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.Mind is not { } mindId ||
            !TryComp<MindComponent>(mindId, out var mind) ||
            mind.UserId is not { } userId)
        {
            return;
        }

        if (!TryGetState(userId, out var state) ||
            !state.HasCrewCycle ||
            state.CrewLifeEntity != ent.Owner)
        {
            return;
        }

        if (args.OldMobState == MobState.Dead && args.NewMobState is MobState.Alive or MobState.Critical)
        {
            ResetRespawnTimer(userId, state);
            return;
        }

        if (args.NewMobState != MobState.Dead || !IsVisitingGhost(mind))
            return;

        RearmRespawnTimer(userId, state);
    }

    private void OnTransferredToClone(Entity<MindContainerComponent> ent, ref TransferredToCloneEvent args)
    {
        if (!TryComp<MindContainerComponent>(args.Cloned, out var clonedContainer) ||
            clonedContainer.Mind is not { } mindId ||
            !TryComp<MindComponent>(mindId, out var mind) ||
            mind.UserId is not { } userId)
        {
            return;
        }

        if (!TryGetState(userId, out var state) ||
            !state.HasCrewCycle ||
            state.CrewLifeEntity != ent.Owner ||
            !ShouldSeedCrewCycle(args.Cloned))
        {
            return;
        }

        state.CrewLifeEntity = args.Cloned;
        ResetRespawnTimer(userId, state);
    }

    private void OnGhostRespawnLobbyRequest(GhostRespawnLobbyRequest ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session.AttachedEntity is not { Valid: true } attached || !HasComp<GhostComponent>(attached))
        {
            Log.Warning($"User {session.Name} sent an invalid {nameof(GhostRespawnLobbyRequest)}");
            return;
        }

        if (!HasRespawnStatus(session, attached))
        {
            SendStatus(session);
            return;
        }

        ArmTimerIfNeeded(session.UserId);
        var status = GetStatus(session.UserId);
        if (!status.CanRespawn)
        {
            SendStatus(session);
            return;
        }

        ClearState(session.UserId);
        _gameTicker.Respawn(session);
    }

    public PirateGhostRespawnDebugState GetDebugState(NetUserId userId)
    {
        if (!TryGetState(userId, out var state))
            return default;

        return new PirateGhostRespawnDebugState(state.HasCrewCycle, state.TimerArmed, state.RespawnAvailableAt);
    }

    public PirateGhostRespawnAvailability GetDebugAvailability(NetUserId userId)
    {
        var status = GetStatus(userId);
        return new PirateGhostRespawnAvailability(status.CanRespawn, status.RemainingTime);
    }

    private bool TryGetState(NetUserId userId, [NotNullWhen(true)] out GhostRespawnState state)
    {
        if (_states.TryGetValue(userId, out var found))
        {
            state = found;
            return true;
        }

        state = null!;
        return false;
    }

    private void EnsureCrewCycle(NetUserId userId, EntityUid entity)
    {
        if (!ShouldSeedCrewCycle(entity))
        {
            _pendingTransitions.Remove(userId);
            return;
        }

        if (_states.ContainsKey(userId))
        {
            _pendingTransitions.Remove(userId);
            return;
        }

        _states[userId] = new GhostRespawnState
        {
            CrewLifeEntity = entity,
            HasCrewCycle = true,
            TimerArmed = false,
            RespawnAvailableAt = TimeSpan.Zero
        };

        _pendingTransitions.Remove(userId);
    }

    private void EnsureInitialCrewCycle(NetUserId userId, EntityUid entity, MindComponent mind)
    {
        if (_states.ContainsKey(userId))
            return;

        // Pirate: only treat the first owned body of a mind as a fresh life.
        if (mind.OriginalOwnedEntity != GetNetEntity(entity))
            return;

        EnsureCrewCycle(userId, entity);
    }

    private bool TryTrackTransferredCrewLife(NetUserId userId, EntityUid entity)
    {
        if (!TryGetState(userId, out var state) ||
            !state.HasCrewCycle ||
            state.CrewLifeEntity == entity ||
            !ShouldSeedCrewCycle(entity))
        {
            return false;
        }

        state.CrewLifeEntity = entity;
        ResetRespawnTimer(userId, state);
        return true;
    }

    private bool ShouldSeedCrewCycle(EntityUid entity)
    {
        if (HasComp<GhostComponent>(entity))
            return false;

        if (HasComp<GhostRoleComponent>(entity))
            return false;

        if (HasComp<ThunderdomePlayerComponent>(entity))
            return false;

        return true;
    }

    private void ArmTimerIfNeeded(NetUserId userId)
    {
        if (!TryGetState(userId, out var state) || !state.HasCrewCycle)
        {
            _pendingTransitions.Remove(userId);
            return;
        }

        if (_pendingTransitions.Remove(userId, out var pending) && pending.Immediate)
        {
            state.TimerArmed = true;
            state.RespawnAvailableAt = _timing.CurTime;
            return;
        }

        if (state.TimerArmed)
        {
            return;
        }

        state.TimerArmed = true;
        state.RespawnAvailableAt = _timing.CurTime + GetRespawnDelay();
    }

    private GhostRespawnStatus GetStatus(NetUserId userId)
    {
        if (!TryGetState(userId, out var state) || !state.HasCrewCycle)
            return new GhostRespawnStatus(true, TimeSpan.Zero);

        if (!state.TimerArmed)
            return new GhostRespawnStatus(false, GetRespawnDelay());

        var remainingTime = state.RespawnAvailableAt - _timing.CurTime;
        if (remainingTime <= TimeSpan.Zero)
            return new GhostRespawnStatus(true, TimeSpan.Zero);

        return new GhostRespawnStatus(false, remainingTime);
    }

    private TimeSpan GetRespawnDelay()
    {
        var delaySeconds = _cfg.GetCVar(CCVars.GhostRespawnDelay);
        return delaySeconds > 0 ? TimeSpan.FromSeconds(delaySeconds) : TimeSpan.Zero;
    }

    private void SendStatusToUser(NetUserId userId)
    {
        if (!_player.TryGetSessionById(userId, out var session))
            return;

        SendStatus(session);
    }

    private void SendStatus(ICommonSession session)
    {
        if (!HasRespawnStatus(session))
        {
            RaiseNetworkEvent(new GhostRespawnStatusEvent(false, false, TimeSpan.Zero), session.Channel);
            return;
        }

        var status = GetStatus(session.UserId);
        RaiseNetworkEvent(new GhostRespawnStatusEvent(true, status.CanRespawn, status.RemainingTime), session.Channel);
    }

    private bool HasRespawnStatus(ICommonSession session, EntityUid? attached = null)
    {
        if (!_gameTicker.LobbyEnabled)
            return false;

        var entity = attached ?? session.AttachedEntity;
        if (entity is not { Valid: true } attachedEntity)
            return true;

        return !IsTemporaryGhostProjection(session.UserId, attachedEntity);
    }

    private bool IsTemporaryGhostProjection(NetUserId userId, EntityUid attached)
    {
        if (!HasComp<VisitingMindComponent>(attached))
            return false;

        if (!_mind.TryGetMind(userId, out _, out var mind))
            return true;

        return !_mind.IsCharacterDeadPhysically(mind);
    }

    private void ClearState(NetUserId userId)
    {
        _states.Remove(userId);
        _pendingTransitions.Remove(userId);
    }

    private void ResetRespawnTimer(NetUserId userId, GhostRespawnState? state = null)
    {
        if (state == null && !TryGetState(userId, out state))
            return;

        if (!state.HasCrewCycle)
            return;

        state.TimerArmed = false;
        state.RespawnAvailableAt = TimeSpan.Zero;
        _pendingTransitions.Remove(userId);
        SendStatusToUser(userId);
    }

    private void RearmRespawnTimer(NetUserId userId, GhostRespawnState state)
    {
        if (!state.HasCrewCycle)
            return;

        state.TimerArmed = true;
        state.RespawnAvailableAt = _timing.CurTime + GetRespawnDelay();
        _pendingTransitions.Remove(userId);
        SendStatusToUser(userId);
    }

    private bool IsVisitingGhost(MindComponent mind)
    {
        return mind.VisitingEntity is { } visiting && HasComp<GhostComponent>(visiting);
    }

    private sealed class GhostRespawnState
    {
        public EntityUid CrewLifeEntity;
        public bool HasCrewCycle;
        public bool TimerArmed;
        public TimeSpan RespawnAvailableAt;
    }

    private sealed class PendingGhostTransition
    {
        public bool Immediate;
    }

    private readonly record struct GhostRespawnStatus(bool CanRespawn, TimeSpan RemainingTime);
}

public readonly record struct PirateGhostRespawnDebugState(bool HasCrewCycle, bool TimerArmed, TimeSpan RespawnAvailableAt);

public readonly record struct PirateGhostRespawnAvailability(bool CanRespawn, TimeSpan RemainingTime);
