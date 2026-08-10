// SPDX-License-Identifier: MIT

using Content.Shared.Bed.Sleep;
using Content.Shared.CCVar;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.SSDIndicator;

/// <summary>
///     Handle changing player SSD indicator status
/// </summary>
public sealed class SSDIndicatorSystem : EntitySystem
{
    public static readonly EntProtoId StatusEffectSSDSleeping = "StatusEffectSSDSleeping";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private bool _icSsdSleep;
    private float _icSsdSleepTime;

    public override void Initialize()
    {
        base.Initialize();

        // Pirate: SSD state and sleep are server-authoritative.
        if (!_net.IsServer)
            return;

        SubscribeLocalEvent<SSDIndicatorComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<SSDIndicatorComponent, MapInitEvent>(OnMapInit);

        _cfg.OnValueChanged(CCVars.ICSSDSleep, obj => _icSsdSleep = obj, true);
        _cfg.OnValueChanged(CCVars.ICSSDSleepTime, obj => _icSsdSleepTime = obj, true);
    }

    private void OnPlayerAttached(EntityUid uid, SSDIndicatorComponent component, PlayerAttachedEvent args)
    {
        ClearSSDState(uid, component);
    }

    private void ClearSSDState(EntityUid uid, SSDIndicatorComponent component)
    {
        var stateChanged = component.IsSSD || component.FallAsleepTime != TimeSpan.Zero;

        component.IsSSD = false;
        component.FallAsleepTime = TimeSpan.Zero;
        _statusEffects.TryRemoveStatusEffect(uid, StatusEffectSSDSleeping);

        if (stateChanged)
            Dirty(uid, component);
    }

    private void OnPlayerDetached(EntityUid uid, SSDIndicatorComponent component, PlayerDetachedEvent args)
    {
        component.IsSSD = true;

        // _Pirate: Don't force NPCs to sleep via SSD
        if (!HasComp<NOSSDSleepComponent>(uid))
        {
            // Sets the time when the entity should fall asleep
            if (_icSsdSleep)
            {
                component.FallAsleepTime = _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime);
            }
        }

        Dirty(uid, component);
    }

    // Prevents mapped mobs to go to sleep immediately
    private void OnMapInit(EntityUid uid, SSDIndicatorComponent component, MapInitEvent args)
    {
        if (!_icSsdSleep || !component.IsSSD)
            return;

        // _Pirate: Don't force NPCs to sleep via SSD
        if (HasComp<NOSSDSleepComponent>(uid))
            return;

        component.FallAsleepTime = _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime);
        component.NextUpdate = _timing.CurTime + component.UpdateInterval;
        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SSDIndicatorComponent>();

        while (query.MoveNext(out var uid, out var ssd))
        {
            // Pirate: ActorComponent is authoritative when an attach event leaves stale SSD state behind.
            if (HasComp<ActorComponent>(uid))
            {
                ClearSSDState(uid, ssd);
                continue;
            }

            if (!_icSsdSleep)
                continue;

            // _Pirate: Don't force NPCs to sleep via SSD
            if (HasComp<NOSSDSleepComponent>(uid))
                continue;

            // Forces the entity to sleep when the time has come
            if (!ssd.IsSSD
                || ssd.NextUpdate > curTime
                || ssd.FallAsleepTime > curTime
                || TerminatingOrDeleted(uid))
                continue;

            _statusEffects.TryUpdateStatusEffectDuration(uid, StatusEffectSSDSleeping);
            ssd.NextUpdate += ssd.UpdateInterval;
            Dirty(uid, ssd);
        }
    }
}
