using Content.Shared.Damage;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Whitelist;

namespace Content.Shared.Trigger.Systems;

public sealed class DamageOnTriggerSystem : XOnTriggerSystem<DamageOnTriggerComponent>
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!; // Pirate
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    protected override void OnTrigger(Entity<DamageOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (!_whitelist.CheckBoth(target, ent.Comp.Blacklist, ent.Comp.Whitelist))
            return;

        var damage = new DamageSpecifier(ent.Comp.Damage);
        var ev = new BeforeDamageOnTriggerEvent(damage, target);
        RaiseLocalEvent(ent.Owner, ref ev);

        args.Handled |= _damageableSystem.TryChangeDamage(target,
            ev.Damage,
            ent.Comp.IgnoreResistances,
            origin: ent.Owner,
            targetPart: ent.Comp.TargetPart) is not null;
    }
}

/// <summary>
/// Raised on an entity before it deals damage using DamageOnTriggerComponent.
/// Used to modify the damage that will be dealt.
/// </summary>
[ByRefEvent]
public record struct BeforeDamageOnTriggerEvent(DamageSpecifier Damage, EntityUid Tripper);
