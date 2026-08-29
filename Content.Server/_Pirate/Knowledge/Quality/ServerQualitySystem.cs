// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Cargo.Components;
using Content.Server.Damage.Components;
using Content.Server.Destructible;
using Content.Shared._Pirate.Knowledge.Quality;
using Content.Shared.Destructible.Thresholds.Triggers;

namespace Content.Server._Pirate.Knowledge.Quality;

/// <summary>
/// Applies quality to components that only exist in the server assembly.
/// </summary>
public sealed class ServerQualitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaticPriceComponent, ApplyQualityEvent>(OnStaticPriceApplyQuality);
        SubscribeLocalEvent<DestructibleComponent, ApplyQualityEvent>(OnDestructibleApplyQuality);
        SubscribeLocalEvent<DamageOnHitComponent, ApplyQualityEvent>(OnSelfDamageApplyQuality);
    }

    private void OnStaticPriceApplyQuality(Entity<StaticPriceComponent> ent, ref ApplyQualityEvent args)
    {
        ent.Comp.Price *= args.Modifier(args.Prototype.Price);
    }

    private void OnDestructibleApplyQuality(Entity<DestructibleComponent> ent, ref ApplyQualityEvent args)
    {
        var modifier = args.Modifier(args.Prototype.Health);
        foreach (var threshold in ent.Comp.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger damage)
                damage.Damage *= modifier;
        }
    }

    private void OnSelfDamageApplyQuality(Entity<DamageOnHitComponent> ent, ref ApplyQualityEvent args)
    {
        ent.Comp.Damage *= args.Modifier(args.Prototype.SelfDamage);
    }
}
