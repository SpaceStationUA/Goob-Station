// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.Pacification.Managers;
using Content.Server._Pirate.Newbies;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Pirate.CCVars;
using Content.Shared.CombatMode.Pacification;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Pacification.Systems;

public sealed class PacifiedNewbiesSystem : EntitySystem
{
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(5);

    [Dependency] private readonly NovicePlaytimeSystem _playtime = default!;
    [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
    [Dependency] private readonly PacifyManager _pacifyManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;

    private readonly HashSet<ICommonSession> _pending = new();
    private readonly List<(EntityUid Entity, ICommonSession Session)> _rechecks = new();
    private TimeSpan _nextCheck;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_configuration, NoviceProtectionCVars.PacificationEnabled, value => _enabled = value, true);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        _tracking.SessionPlayTimeUpdated += OnPlaytimeUpdated;
    }

    public override void Shutdown()
    {
        _tracking.SessionPlayTimeUpdated -= OnPlaytimeUpdated;
        _pending.Clear();
        base.Shutdown();
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        Refresh(ev.Entity, ev.Player);
    }

    private void OnPlaytimeUpdated(ICommonSession session)
    {
        // The manager sends this outside simulation; change components on the next simulation update.
        _pending.Add(session);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var session in _pending)
        {
            if (session.AttachedEntity is { } entity && !TerminatingOrDeleted(entity))
                Refresh(entity, session);
        }
        _pending.Clear();

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + RecheckInterval;
        _rechecks.Clear();
        var query = EntityQueryEnumerator<NovicePacificationComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
            _rechecks.Add((uid, actor.PlayerSession));

        // Only already-restricted players need the fallback and the one-hour expiry check.
        foreach (var (uid, session) in _rechecks)
            Refresh(uid, session);
    }

    private void Refresh(EntityUid uid, ICommonSession session)
    {
        var restricted = _enabled && _playtime.IsRestricted(session, RequiredPlaytime);
        restricted |= _pacifyManager.IsPacified(session.Name);

        if (restricted)
        {
            var guard = EnsureComp<NovicePacificationComponent>(uid);
            if (!TryComp<PacifiedComponent>(uid, out var pacified))
                guard.AppliedPacification = AddComp<PacifiedComponent>(uid);
            else if (guard.AppliedPacification != pacified)
                guard.AppliedPacification = null;
            return;
        }

        if (!TryComp<NovicePacificationComponent>(uid, out var previous))
            return;

        if (previous.AppliedPacification != null &&
            TryComp<PacifiedComponent>(uid, out var current) && current == previous.AppliedPacification)
            RemComp<PacifiedComponent>(uid);

        RemComp<NovicePacificationComponent>(uid);
    }
}
