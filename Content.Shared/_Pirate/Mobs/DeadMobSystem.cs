// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Mobs;

/// <summary>Event-driven dead-mob marker; intentionally does not scan entities.</summary>
public sealed class DeadMobSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateComponent, MapInitEvent>(OnMobStateMapInit);
        // Use the global event form so this marker does not collide with other
        // MobStateComponent-specific subscriptions in the event dispatcher.
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateMapInit(Entity<MobStateComponent> ent, ref MapInitEvent args)
    {
        UpdateMarker(ent.Owner, ent.Comp.CurrentState);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        // State application already synchronizes the marker from the network.
        // Mutating it here would create duplicate component churn on clients.
        if (_timing.ApplyingState)
            return;

        UpdateMarker(args.Target, args.NewMobState);
    }

    private void UpdateMarker(EntityUid uid, MobState state)
    {
        if (state == MobState.Dead)
            EnsureComp<DeadMobComponent>(uid);
        else
            RemComp<DeadMobComponent>(uid);
    }
}
