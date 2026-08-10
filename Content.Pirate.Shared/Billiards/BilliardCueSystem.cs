// SPDX-License-Identifier: MIT

using Content.Shared.Weapons.Melee.Components;

namespace Content.Pirate.Shared.Billiards;

public sealed class BilliardCueSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BilliardCueComponent, AttemptMeleeThrowOnHitEvent>(OnAttemptMeleeThrowOnHit);
    }

    private void OnAttemptMeleeThrowOnHit(Entity<BilliardCueComponent> ent, ref AttemptMeleeThrowOnHitEvent args)
    {
        if (!HasComp<BilliardBallComponent>(args.Target))
        {
            args.Cancelled = true;
            args.Handled = true;
        }
    }
}
