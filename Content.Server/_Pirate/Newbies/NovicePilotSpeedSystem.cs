// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Players.PlayTimeTracking;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Newbies;

/// <summary>Limits active pilots, including ghost roles, using total playtime.</summary>
public sealed class NovicePilotSpeedSystem : EntitySystem
{
    public const float SpeedLimit = 4f;
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(4);
    [Dependency] private readonly NovicePlaytimeSystem _playtime = default!;
    [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly Dictionary<ICommonSession, (TimeSpan NextCheck, bool Restricted)> _cache = new();

    public override void Initialize()
    {
        base.Initialize();
        _tracking.SessionPlayTimeUpdated += Invalidate;
        SubscribeLocalEvent<PlayerDetachedEvent>(OnDetached);
    }

    public override void Shutdown()
    {
        _tracking.SessionPlayTimeUpdated -= Invalidate;
        _cache.Clear();
        base.Shutdown();
    }

    private void OnDetached(PlayerDetachedEvent ev) => Invalidate(ev.Player);
    private void Invalidate(ICommonSession session) => _cache.Remove(session);

    public float GetSpeedLimit(EntityUid pilot, float normalLimit)
    {
        if (!TryComp<ActorComponent>(pilot, out var actor))
            return normalLimit;

        var session = actor.PlayerSession;
        if (!_cache.TryGetValue(session, out var cached) || _timing.CurTime >= cached.NextCheck)
        {
            cached = (_timing.CurTime + TimeSpan.FromSeconds(5), _playtime.IsRestricted(session, RequiredPlaytime));
            _cache[session] = cached;
        }

        return cached.Restricted ? Math.Min(normalLimit, SpeedLimit) : normalLimit;
    }
}
