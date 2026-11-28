// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Throwing;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Suspicion;

[RegisterComponent, NetworkedComponent]
public sealed partial class SuspicionThrowingKnifeComponent : Component
{
    public DamageSpecifier? BaseDamage;
}

public sealed class SuspicionThrowingKnifeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SuspicionThrowingKnifeComponent, ThrowDoHitEvent>(OnHit, before: [typeof(SharedDamageOtherOnHitSystem)]);
    }

    private void OnHit(Entity<SuspicionThrowingKnifeComponent> ent, ref ThrowDoHitEvent args)
    {
        if (args.Component.ThrownTime is not {} thrown || !TryComp<DamageOtherOnHitComponent>(ent, out var damage))
            return;
        ent.Comp.BaseDamage ??= damage.Damage;
        // Original Suspicion knife grows from 5 to 100 slash damage during the first 475 ms of flight.
        var progress = Math.Clamp((float)(_timing.CurTime - thrown).TotalSeconds / 0.475f, 0f, 1f);
        damage.Damage = ent.Comp.BaseDamage * (1f + 19f * progress);
    }
}
