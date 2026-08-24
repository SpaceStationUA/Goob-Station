// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Random.Helpers;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Trigger;

public sealed partial class TriggerOnDamageSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TriggerOnDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<TriggerOnDamageComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } damage || damage.GetTotal() <= ent.Comp.Threshold)
            return;

        var seed = SharedRandomExtensions.HashCodeCombine((int) _timing.CurTick.Value, GetNetEntity(ent).Id);
        var random = new System.Random(seed);
        if (!random.Prob(ent.Comp.Probability))
            return;

        _trigger.Trigger(ent.Owner, args.Origin, ent.Comp.KeyOut);
    }
}
