using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Light.Components;
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Systems;

namespace Content.Pirate.Shared.Genetics.Damage;

public sealed class FragileSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FragileComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<FragileComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Target != ent.Owner || !args.Damage.AnyPositive())
            return;

        args.Damage *= ent.Comp.Modifier; // die
    }
}
