// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Durability;

/// <summary>
/// Event-driven wear, repair, and stat modifiers for durable items.
/// </summary>
public sealed partial class DurabilitySystem : EntitySystem
{
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private static readonly Dictionary<DurabilityState, Color> AssociatedColors = new()
    {
        [DurabilityState.Reinforced] = new Color(98, 217, 195),
        [DurabilityState.Pristine] = new Color(117, 217, 98),
        [DurabilityState.Worn] = new Color(217, 191, 98),
        [DurabilityState.Damaged] = new Color(217, 140, 98),
        [DurabilityState.Broken] = new Color(217, 98, 98),
        [DurabilityState.Destroyed] = Color.Red,
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DurabilityComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DurabilityComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<DurabilityComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<DurabilityComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<DurabilityComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<DurabilityComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<DurabilityComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<DurabilityComponent, DurabilityDamageChangedEvent>(OnDurabilityDamageChanged);
        SubscribeLocalEvent<GunComponent, DurabilityStateChangedEvent>(OnStateChangeGun);
        SubscribeLocalEvent<DurabilityComponent, DurabilityStateChangedEvent>(OnDurabilityStateChanged);
        SubscribeLocalEvent<DurabilityComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DurabilityComponent, RepairItemDoAfterEvent>(OnRepairItemDoAfter);
        SubscribeLocalEvent<DurabilityComponent, RepairToolDoAfterEvent>(OnRepairToolDoAfter);
        SubscribeLocalEvent<CustomDurabilityModifierComponent, DurabilityStateChangedByEvent>(OnStateChangedBy);
    }

    public bool DamageEntity(
        EntityUid uid,
        FixedPoint2 amount,
        DurabilityComponent? comp = null,
        EntityUid? attacker = null,
        HashSet<EntityUid>? targets = null,
        EntityUid? used = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        if (amount > 0 && !RollDamageChance(uid, comp))
            return false;

        var before = new DurabilityChangeAttemptEvent(uid, amount);
        RaiseLocalEvent(uid, ref before);
        amount = before.Damage;

        var oldDamage = comp.Damage;
        comp.Damage = FixedPoint2.Max(comp.Damage + amount, -comp.MaxRepairBonus);
        DirtyField(uid, comp, nameof(DurabilityComponent.Damage));

        var oldState = comp.DurabilityState;
        comp.DurabilityState = GetState(comp);
        DirtyField(uid, comp, nameof(DurabilityComponent.DurabilityState));

        if (comp.DurabilityState != oldState)
        {
            var stateChanged = new DurabilityStateChangedEvent(
                oldState,
                comp.DurabilityState,
                uid,
                attacker,
                targets,
                used);
            RaiseLocalEvent(uid, ref stateChanged);
        }

        if (used is { } item)
        {
            var changedBy = new DurabilityStateChangedByEvent(
                oldState,
                comp.DurabilityState,
                uid,
                attacker,
                targets,
                item);
            RaiseLocalEvent(item, ref changedBy);
        }

        var changed = new DurabilityDamageChangedEvent(uid, comp.Damage, oldDamage);
        RaiseLocalEvent(uid, ref changed);
        return comp.Damage != oldDamage;
    }

    public DurabilityState GetState(DurabilityComponent comp)
    {
        foreach (var (threshold, state) in comp.DurabilityThresholds.Reverse())
        {
            if (state == DurabilityState.Pristine &&
                !comp.DurabilityThresholds.ContainsValue(DurabilityState.Reinforced) &&
                comp.Damage < 0)
            {
                return DurabilityState.Reinforced;
            }

            if (comp.Damage >= threshold * comp.DurabilityScale)
                return state;
        }

        return DurabilityState.Pristine;
    }

    public float GetModifier(DurabilityComponent comp)
    {
        if (comp.CustomDurabilityModifiers.TryGetValue(comp.DurabilityState, out var custom))
            return custom;

        if (comp.DurabilityModifiers.TryGetValue(comp.DurabilityState, out var configured))
            return configured;

        return comp.DurabilityState == DurabilityState.Destroyed ? 0f : 1f;
    }

    public void SetScale(Entity<DurabilityComponent?> ent, FixedPoint2 scale)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.DurabilityScale == scale)
            return;

        ent.Comp.DurabilityScale = scale;
        DirtyField(ent, ent.Comp, nameof(DurabilityComponent.DurabilityScale));
    }

    public void ScaleDamageProbability(Entity<DurabilityComponent> ent, float divisor)
    {
        if (divisor == 0f)
            return;

        ent.Comp.DamageProbability /= divisor;
        DirtyField(ent, ent.Comp, nameof(DurabilityComponent.DamageProbability));
    }

    public void SetDamageProbability(Entity<DurabilityComponent> ent, float probability)
    {
        probability = Math.Clamp(probability, 0f, 1f);
        if (ent.Comp.DamageProbability == probability)
            return;

        ent.Comp.DamageProbability = probability;
        DirtyField(ent, ent.Comp, nameof(DurabilityComponent.DamageProbability));
    }

    private bool RollDamageChance(EntityUid uid, DurabilityComponent comp)
    {
        var probability = Math.Clamp(comp.DamageProbability, 0f, 1f);
        return probability >= 1f || probability > 0f && PredictedRandom(uid, 0x44555241).Prob(probability);
    }

    private System.Random PredictedRandom(EntityUid uid, int salt)
    {
        var seed = SharedRandomExtensions.HashCodeCombine(
            (int) _timing.CurTick.Value,
            GetNetEntity(uid).Id,
            salt);
        return new System.Random(seed);
    }

    private void OnStateChangedBy(Entity<CustomDurabilityModifierComponent> ent, ref DurabilityStateChangedByEvent args)
    {
        if (!TryComp(args.Weapon, out DurabilityComponent? durability) ||
            !TryComp(args.Weapon, out MeleeWeaponComponent? melee) ||
            !ent.Comp.MaxDurabilityStateModifiers.TryGetValue(args.NewState, out var limits))
        {
            return;
        }

        var damage = melee.Damage.GetTotal().Float();
        if (damage == 0f)
            return;

        var modified = limits switch
        {
            { X: > 0f, Y: > 1f } => MathF.Min(damage + limits.X, damage * limits.Y),
            { X: < 0f, Y: < 1f } => MathF.Max(damage + limits.X, damage * limits.Y),
            _ => damage,
        };

        if (modified == damage)
            return;

        durability.CustomDurabilityModifiers[args.NewState] = modified / damage;
        DirtyField(args.Weapon, durability, nameof(DurabilityComponent.CustomDurabilityModifiers));
    }

    private void OnAttemptMelee(Entity<DurabilityComponent> ent, ref AttemptMeleeEvent args)
    {
        if (ent.Comp.DurabilityState != DurabilityState.Destroyed)
            return;

        args.Cancelled = true;
        if (ent.Comp.DestroyedSwingAttemptPopup is { } popup)
            args.Message = Loc.GetString(popup, ("weapon", Name(ent)));
    }

    private void OnMeleeHit(Entity<DurabilityComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !args.HitEntities.Any(HasComp<DamageableComponent>))
            return;

        var damage = PredictedRandom(ent, 0x4D454C45)
            .NextFloat(ent.Comp.MinDamageRoll.Float(), ent.Comp.MaxDamageRoll.Float());
        DamageEntity(ent, damage, ent.Comp, args.User, args.HitEntities.ToHashSet());
    }

    private void OnGetMeleeDamage(Entity<DurabilityComponent> ent, ref GetMeleeDamageEvent args)
    {
        args.Damage *= GetModifier(ent.Comp);
    }

    private void OnAttemptShoot(Entity<DurabilityComponent> ent, ref AttemptShootEvent args)
    {
        if (ent.Comp.DurabilityState != DurabilityState.Destroyed)
            return;

        args.Cancelled = true;
        if (ent.Comp.DestroyedSwingAttemptPopup is { } popup)
            args.Message = Loc.GetString(popup, ("weapon", Name(ent)));
    }

    private void OnGunShot(Entity<DurabilityComponent> ent, ref GunShotEvent args)
    {
        var damage = PredictedRandom(ent, 0x47554E53)
            .NextFloat(ent.Comp.MinDamageRoll.Float(), ent.Comp.MaxDamageRoll.Float());
        DamageEntity(ent, damage, ent.Comp, args.User);
    }

    private void OnGunRefreshModifiers(Entity<DurabilityComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var modifier = GetModifier(ent.Comp);
        args.FireRate *= modifier;
        args.BurstFireRate *= modifier;
        args.AngleDecay *= modifier;
        args.AngleIncrease *= modifier;

        if (modifier <= 0f)
            return;

        args.MaxAngle /= modifier;
        args.MinAngle /= modifier;
        args.BurstCooldown /= modifier;
    }

    private void OnDurabilityDamageChanged(Entity<DurabilityComponent> ent, ref DurabilityDamageChangedEvent args)
    {
        var difference = args.Damage - args.OldDamage;
        if (difference < 0)
        {
            var loc = args.OldDamage <= 0 && args.Damage <= 0
                ? "durability-reinforce-popup"
                : "durability-repair-popup";
            var amount = args.OldDamage - FixedPoint2.Max(args.Damage, -ent.Comp.MaxRepairBonus);
            _popup.PopupCoordinates(
                Loc.GetString(loc, ("weapon", Name(ent)), ("amount", amount)),
                Transform(ent).Coordinates);
            return;
        }

        if (difference > 0 && ent.Comp.DamagePopups.TryGetValue(ent.Comp.DurabilityState, out var messages) && messages.Count > 0)
        {
            var random = PredictedRandom(ent, 0x504F5055);
            var message = messages.ElementAt(random.Next(messages.Count));
            _popup.PopupCoordinates(Loc.GetString(message), Transform(ent).Coordinates, PopupType.SmallCaution);
            return;
        }

        if (difference == 0 && ent.Comp.Damage <= -ent.Comp.MaxRepairBonus)
        {
            _popup.PopupCoordinates(
                Loc.GetString("durability-repair-max", ("weapon", Name(ent))),
                Transform(ent).Coordinates);
        }
    }

    private void OnDurabilityStateChanged(Entity<DurabilityComponent> ent, ref DurabilityStateChangedEvent args)
    {
        if (ent.Comp.CustomDurabilityModifiers.Count > 0 && args.NewState != args.OldState)
        {
            foreach (var state in ent.Comp.CustomDurabilityModifiers.Keys.ToArray())
            {
                if (args.NewState < args.OldState && state > args.NewState ||
                    args.NewState > args.OldState && state < args.NewState)
                {
                    ent.Comp.CustomDurabilityModifiers.Remove(state);
                }
            }

            DirtyField(ent, ent.Comp, nameof(DurabilityComponent.CustomDurabilityModifiers));
        }

        if (args.NewState != DurabilityState.Destroyed)
            return;

        if (ent.Comp.OnBreakEffects is { } effects)
            _effects.ApplyEffects(ent, effects, user: args.Attacker);

        if (ent.Comp.DeleteOnDestroyed)
            PredictedQueueDel(ent);

        if (args.Attacker is { } attacker && TryComp(attacker, out MeleeWeaponComponent? userMelee))
        {
            userMelee.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(1 / userMelee.AttackRate);
            DirtyField(attacker, userMelee, nameof(MeleeWeaponComponent.NextAttack));
        }
    }

    private void OnStateChangeGun(Entity<GunComponent> ent, ref DurabilityStateChangedEvent args)
    {
        _gun.RefreshModifiers(ent.AsNullable(), args.Attacker);
    }
}
