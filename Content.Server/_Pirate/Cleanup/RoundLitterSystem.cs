// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Cleanup;
using Content.Shared.Fluids.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Cleanup;

public sealed class RoundLitterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private static readonly TimeSpan ResidueLifetime = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan CasingLifetime = TimeSpan.FromMinutes(10);
    private TimeSpan _nextSweep;

    public override void Initialize()
    {
        SubscribeLocalEvent<DisposableLitterComponent, LitterProducedEvent>(OnLitterProduced);
        SubscribeLocalEvent<PuddleComponent, LitterProducedEvent>(OnPuddleProduced);
        SubscribeLocalEvent<CartridgeAmmoComponent, CartridgeSpentChangedEvent>(OnSpentChanged);
        SubscribeLocalEvent<RoundLitterComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<RoundLitterComponent, EntGotRemovedFromContainerMessage>(OnRemovedFromContainer);
    }

    private void OnLitterProduced(Entity<DisposableLitterComponent> ent, ref LitterProducedEvent args)
    {
        Track(ent, ResidueLifetime, true);
    }

    private void OnSpentChanged(Entity<CartridgeAmmoComponent> ent, ref CartridgeSpentChangedEvent args)
    {
        if (ent.Comp.Spent && !ent.Comp.DeleteOnSpawn)
            Track(ent, CasingLifetime, true);
        else
            RemComp<RoundLitterComponent>(ent);
    }

    private void OnPuddleProduced(Entity<PuddleComponent> ent, ref LitterProducedEvent args)
        => TrackResidue(ent.Owner);

    /// <summary>Only the producer of a new puddle or stain can start its timer.</summary>
    public void TrackResidue(EntityUid uid) => Track(uid, ResidueLifetime, false);

    public void RefreshResidue(EntityUid uid)
    {
        if (TryComp<RoundLitterComponent>(uid, out var litter))
            litter.ExpiresAt = _timing.CurTime + litter.Lifetime;
    }

    private void Track(EntityUid uid, TimeSpan lifetime, bool floorOnly)
    {
        var litter = EnsureComp<RoundLitterComponent>(uid);
        litter.Lifetime = lifetime;
        litter.FloorOnly = floorOnly;
        litter.ExpiresAt = _timing.CurTime + lifetime;
    }

    private void OnParentChanged(Entity<RoundLitterComponent> ent, ref EntParentChangedMessage args)
    {
        if (ent.Comp.FloorOnly)
            ent.Comp.ExpiresAt = _timing.CurTime + ent.Comp.Lifetime;
    }

    private void OnRemovedFromContainer(Entity<RoundLitterComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (ent.Comp.FloorOnly)
            ent.Comp.ExpiresAt = _timing.CurTime + ent.Comp.Lifetime;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;

        _nextSweep = now + TimeSpan.FromSeconds(1);
        var budget = 64;
        var query = EntityQueryEnumerator<RoundLitterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var litter, out var xform))
        {
            if (litter.ExpiresAt > now)
                continue;

            if (litter.FloorOnly && (xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid
                || _containers.IsEntityInContainer(uid)))
            {
                litter.ExpiresAt = now + litter.Lifetime;
                continue;
            }

            QueueDel(uid);
            if (--budget == 0)
                break;
        }
    }
}
