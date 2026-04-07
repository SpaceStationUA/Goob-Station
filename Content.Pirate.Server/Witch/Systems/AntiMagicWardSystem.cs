using Content.Pirate.Shared.Witch;
using Content.Pirate.Shared.Witch.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Magic.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class AntiMagicWardSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private const float MagicAttackDamageMultiplier = 0.6f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntiMagicWardComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AntiMagicWardComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AntiMagicWardComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatusEffectAdded);
        SubscribeLocalEvent<AntiMagicWardComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnInit(Entity<AntiMagicWardComponent> ent, ref ComponentInit args)
    {
        if (!_prototype.TryIndex(ent.Comp.Modifier, out var modifier))
            return;

        var buff = EnsureComp<DamageProtectionBuffComponent>(ent.Owner);
        if (!buff.Modifiers.ContainsKey(ent.Comp.Modifier))
            buff.Modifiers.Add(ent.Comp.Modifier, modifier);
    }

    private void OnShutdown(Entity<AntiMagicWardComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<DamageProtectionBuffComponent>(ent.Owner, out var buff))
            return;

        buff.Modifiers.Remove(ent.Comp.Modifier);
        if (buff.Modifiers.Count == 0)
            RemComp<DamageProtectionBuffComponent>(ent.Owner);
    }

    private void OnBeforeStatusEffectAdded(Entity<AntiMagicWardComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        foreach (var effect in WitchStatusEffectIds.WitchEffects)
        {
            if (args.Effect != effect)
                continue;

            args.Cancelled = true;
            return;
        }
    }

    private void OnDamageModify(Entity<AntiMagicWardComponent> ent, ref DamageModifyEvent args)
    {
        if (HasHolyDamage(args.Damage))
            return;

        if (!IsMagicOrigin(args.Origin))
            return;

        args.Damage *= MagicAttackDamageMultiplier;
    }

    private bool HasHolyDamage(DamageSpecifier damage)
    {
        return damage.DamageDict.TryGetValue("Holy", out var holy) && holy > 0;
    }

    private bool IsMagicOrigin(EntityUid? origin)
    {
        if (origin == null || !Exists(origin.Value))
            return false;

        if (HasComp<MagicDamageSourceComponent>(origin.Value))
            return true;

        if (TryComp<ReflectiveComponent>(origin.Value, out var reflective) &&
            (reflective.Reflective & ReflectType.Magic) != 0)
            return true;

        return HasComp<MagicComponent>(origin.Value);
    }
}
