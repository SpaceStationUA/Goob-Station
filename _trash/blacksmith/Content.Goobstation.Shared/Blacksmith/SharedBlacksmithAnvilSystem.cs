using System.Text;
using Content.Shared._Shitmed.OnHit;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Hands;
using Content.Shared.Movement.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Goobstation.Shared.Blacksmith;

/// <summary>
/// Applies stacked forge modifiers to melee damage, attack rate, held move speed, and item name.
/// </summary>
public abstract class SharedBlacksmithAnvilSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, GetMeleeAttackRateEvent>(OnGetAttackRate);
        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, RefreshNameModifiersEvent>(OnRefreshName);
        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshHeldSpeed);
        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, GotEquippedHandEvent>(OnHandEquip);
        SubscribeLocalEvent<BlacksmithForgedWeaponComponent, GotUnequippedHandEvent>(OnHandUnequip);
    }

    private void OnStartup(Entity<BlacksmithForgedWeaponComponent> ent, ref ComponentStartup args)
    {
        Recalculate(ent);
        ApplyInjectOnHit(ent);
        _nameModifier.RefreshNameModifiers(ent.Owner);
    }

    private void OnHandleState(Entity<BlacksmithForgedWeaponComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _nameModifier.RefreshNameModifiers(ent.Owner);
    }

    private void OnGetMeleeDamage(Entity<BlacksmithForgedWeaponComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.DamageMultiplier != 1f)
            args.Damage *= ent.Comp.DamageMultiplier;
    }

    private void OnGetAttackRate(Entity<BlacksmithForgedWeaponComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (ent.Comp.AttackRateMultiplier != 1f)
            args.Multipliers *= ent.Comp.AttackRateMultiplier;
    }

    private void OnMeleeHit(Entity<BlacksmithForgedWeaponComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (!ent.Comp.BonusDamage.Empty)
            args.BonusDamage += ent.Comp.BonusDamage;

        // Crit rolls only on the server to avoid double-dipping / desync.
        if (_net.IsClient || ent.Comp.CritChance <= 0f || ent.Comp.CritDamage.Empty)
            return;

        if (_random.Prob(ent.Comp.CritChance))
            args.BonusDamage += ent.Comp.CritDamage;
    }

    private void OnRefreshName(Entity<BlacksmithForgedWeaponComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (ent.Comp.Modifiers.Count == 0)
            return;

        var sb = new StringBuilder();
        for (var i = 0; i < ent.Comp.Modifiers.Count; i++)
        {
            if (!_prototypes.TryIndex(ent.Comp.Modifiers[i], out var proto))
                continue;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(Loc.GetString(proto.Name));
        }

        if (sb.Length == 0)
            return;

        args.AddModifier("blacksmith-weapon-name-modified", priority: 50, ("qualities", sb.ToString()));
    }

    private void OnRefreshHeldSpeed(Entity<BlacksmithForgedWeaponComponent> ent, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (ent.Comp.HeldSpeedMultiplier == 1f)
            return;

        args.Args.ModifySpeed(ent.Comp.HeldSpeedMultiplier, ent.Comp.HeldSpeedMultiplier);
    }

    private void OnHandEquip(Entity<BlacksmithForgedWeaponComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.HeldSpeedMultiplier != 1f)
            _movementSpeed.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnHandUnequip(Entity<BlacksmithForgedWeaponComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.HeldSpeedMultiplier != 1f)
            _movementSpeed.RefreshMovementSpeedModifiers(args.User);
    }

    /// <summary>
    /// Sets applied modifiers, recalculates caches, and updates the item name.
    /// </summary>
    public void SetModifiers(EntityUid uid, List<ProtoId<BlacksmithWeaponModifierPrototype>> modifiers, BlacksmithForgedWeaponComponent? comp = null)
    {
        comp ??= EnsureComp<BlacksmithForgedWeaponComponent>(uid);
        comp.Modifiers = modifiers;
        Recalculate((uid, comp));
        ApplyInjectOnHit((uid, comp));
        Dirty(uid, comp);
        _nameModifier.RefreshNameModifiers(uid);
    }

    private void Recalculate(Entity<BlacksmithForgedWeaponComponent> ent)
    {
        var damage = 1f;
        var attack = 1f;
        var speed = 1f;
        var bonus = new DamageSpecifier();
        var critChance = 0f;
        var critDamage = new DamageSpecifier();

        foreach (var id in ent.Comp.Modifiers)
        {
            if (!_prototypes.TryIndex(id, out var proto))
                continue;

            damage *= proto.DamageMultiplier;
            attack *= proto.AttackRateMultiplier;
            speed *= proto.HeldSpeedMultiplier;

            if (proto.BonusDamage != null)
                bonus += proto.BonusDamage;

            // Independent crits: keep the highest chance and sum crit damage if multiple somehow apply.
            if (proto.CritChance > critChance)
                critChance = proto.CritChance;

            if (proto.CritDamage != null)
                critDamage += proto.CritDamage;
        }

        ent.Comp.DamageMultiplier = damage;
        ent.Comp.AttackRateMultiplier = attack;
        ent.Comp.HeldSpeedMultiplier = speed;
        ent.Comp.BonusDamage = bonus;
        ent.Comp.CritChance = critChance;
        ent.Comp.CritDamage = critDamage;
    }

    private void ApplyInjectOnHit(Entity<BlacksmithForgedWeaponComponent> ent)
    {
        var reagents = new List<ReagentQuantity>();
        float? limit = null;

        foreach (var id in ent.Comp.Modifiers)
        {
            if (!_prototypes.TryIndex(id, out var proto) || proto.InjectReagents == null)
                continue;

            reagents.AddRange(proto.InjectReagents);
            if (proto.InjectReagentLimit != null)
                limit = Math.Max(limit ?? 0f, proto.InjectReagentLimit.Value);
        }

        if (reagents.Count == 0)
        {
            RemCompDeferred<InjectOnHitComponent>(ent);
            return;
        }

        var inject = EnsureComp<InjectOnHitComponent>(ent);
        inject.Reagents = reagents;
        inject.ReagentLimit = limit;
        inject.NeedsRestrain = false;
        inject.InjectionDelay = 0;
        Dirty(ent.Owner, inject);
    }
}
