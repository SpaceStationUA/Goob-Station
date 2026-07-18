// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Nutrition;
using Content.Shared.Trigger.Systems;

namespace Content.Pirate.Shared.Ranching;

public sealed class RanchingFullyEatenTriggerSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RanchingTriggerOnFullyEatenComponent, FullyEatenEvent>(OnFullyEaten);
    }

    private void OnFullyEaten(Entity<RanchingTriggerOnFullyEatenComponent> ent, ref FullyEatenEvent args)
    {
        _trigger.Trigger(ent.Owner, args.User, TriggerSystem.DefaultTriggerKey);
    }
}
