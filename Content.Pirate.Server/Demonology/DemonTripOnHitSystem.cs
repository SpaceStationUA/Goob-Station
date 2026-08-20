// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Demonology;
using Content.Server.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Demonology;

public sealed class DemonTripOnHitSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DemonTripOnHitComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<DemonTripOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (_random.Prob(ent.Comp.Chance))
                _stun.TryKnockdown(target, ent.Comp.Duration, true);
        }
    }
}
