// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Armor;
using Content.Shared._Pirate.Durability;
using Content.Shared.Blocking;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Knowledge.Quality;

/// <summary>
/// Rolls and applies item quality in response to construction and transfer events.
/// This system deliberately has no update loop or entity-wide queries.
/// </summary>
public sealed class QualitySystem : EntitySystem
{
    private static readonly EntProtoId FabricationKnowledge = "FabricationKnowledge";
    private static readonly ProtoId<KnowledgeCategoryPrototype> CraftingCategory = "Crafting";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DurabilitySystem _durability = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    private EntityQuery<QualityComponent> _qualityQuery;

    public override void Initialize()
    {
        base.Initialize();

        _qualityQuery = GetEntityQuery<QualityComponent>();

        SubscribeLocalEvent<QualityComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
        SubscribeLocalEvent<QualityComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<QualityComponent, QualityTransferEvent>(OnQualityTransfer);
        SubscribeLocalEvent<QualityComponent, StackSplitEvent>(OnStackSplit);
        SubscribeLocalEvent<QualityComponent, AttemptMergeStackEvent>(OnAttemptMergeStack);

        SubscribeLocalEvent<ArmorComponent, ApplyQualityEvent>(OnArmorApplyQuality);
        SubscribeLocalEvent<ClothingComponent, ApplyQualityEvent>(OnClothingApplyQuality);
        SubscribeLocalEvent<ExplosionResistanceComponent, ApplyQualityEvent>(OnExplosionResistanceApplyQuality);
        SubscribeLocalEvent<StaminaResistanceComponent, ApplyQualityEvent>(OnStaminaResistanceApplyQuality);
        SubscribeLocalEvent<DamageOtherOnHitComponent, ApplyQualityEvent>(OnThrownDamageApplyQuality);
        SubscribeLocalEvent<DurabilityComponent, ApplyQualityEvent>(OnDurabilityApplyQuality);
        SubscribeLocalEvent<MeleeWeaponComponent, ApplyQualityEvent>(OnMeleeDamageApplyQuality);
        SubscribeLocalEvent<GunComponent, ApplyQualityEvent>(OnGunApplyQuality);
        SubscribeLocalEvent<ProjectileComponent, ApplyQualityEvent>(OnProjectileApplyQuality);
        SubscribeLocalEvent<BlockingComponent, ApplyQualityEvent>(OnShieldApplyQuality);
    }

    private void OnRefreshNameModifiers(Entity<QualityComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (!ent.Comp.Applied)
            return;

        args.AddModifier($"quality-name-{Math.Clamp(ent.Comp.Quality, -5, 5)}");
    }

    private void OnGunRefreshModifiers(Entity<QualityComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!ent.Comp.Applied || !_prototypes.Resolve(ent.Comp.QualityFactors, out var factors))
            return;

        var modifier = QualityModifier(ent.Comp.Quality, factors.Gun);
        args.MinAngle *= modifier;
        args.MaxAngle *= modifier;
    }

    private void OnArmorApplyQuality(Entity<ArmorComponent> ent, ref ApplyQualityEvent args)
    {
        var modifier = args.Modifier(args.Prototype.Armor);
        foreach (var damageType in ent.Comp.Modifiers.Coefficients.Keys.ToArray())
            ent.Comp.Modifiers.Coefficients[damageType] *= modifier;

        Dirty(ent);
    }

    private void OnClothingApplyQuality(Entity<ClothingComponent> ent, ref ApplyQualityEvent args)
    {
        ent.Comp.EquipDelay *= args.Modifier(args.Prototype.ClothingDelay);
        Dirty(ent);
    }

    private void OnExplosionResistanceApplyQuality(Entity<ExplosionResistanceComponent> ent, ref ApplyQualityEvent args)
    {
        ent.Comp.DamageCoefficient *= args.Modifier(args.Prototype.ExplosionResist);
        Dirty(ent);
    }

    private void OnStaminaResistanceApplyQuality(Entity<StaminaResistanceComponent> ent, ref ApplyQualityEvent args)
    {
        ent.Comp.DamageCoefficient *= args.Modifier(args.Prototype.StaminaResist);
        Dirty(ent);
    }

    private void OnThrownDamageApplyQuality(Entity<DamageOtherOnHitComponent> ent, ref ApplyQualityEvent args)
    {
        ent.Comp.Damage *= args.Modifier(args.Prototype.Damage);
    }

    private void OnDurabilityApplyQuality(Entity<DurabilityComponent> ent, ref ApplyQualityEvent args)
    {
        _durability.ScaleDamageProbability(ent, args.Modifier(args.Prototype.Durability));
    }

    private void OnMeleeDamageApplyQuality(Entity<MeleeWeaponComponent> ent, ref ApplyQualityEvent args)
    {
        ent.Comp.Damage *= args.Modifier(args.Prototype.MeleeDamage);
        Dirty(ent);
    }

    private void OnGunApplyQuality(Entity<GunComponent> ent, ref ApplyQualityEvent args)
    {
        _gun.RefreshModifiers(ent.AsNullable());
    }

    private void OnProjectileApplyQuality(Entity<ProjectileComponent> ent, ref ApplyQualityEvent args)
    {
        ent.Comp.Damage *= args.Modifier(args.Prototype.Projectile);
        Dirty(ent);
    }

    private void OnShieldApplyQuality(Entity<BlockingComponent> ent, ref ApplyQualityEvent args)
    {
        var coefficientModifier = args.Modifier(args.Prototype.Shield);
        var flatModifier = args.Modifier(args.Prototype.ShieldFlat);

        ent.Comp.PassiveBlockFraction *= flatModifier;
        ent.Comp.ActiveBlockFraction *= flatModifier;
        ApplyShieldModifier(ent.Comp.PassiveBlockDamageModifer, coefficientModifier, flatModifier);
        ApplyShieldModifier(ent.Comp.ActiveBlockDamageModifier, coefficientModifier, flatModifier);
        Dirty(ent);
    }

    private static void ApplyShieldModifier(
        Content.Shared.Damage.DamageModifierSet modifiers,
        float coefficientModifier,
        float flatModifier)
    {
        foreach (var damageType in modifiers.Coefficients.Keys.ToArray())
            modifiers.Coefficients[damageType] *= coefficientModifier;

        foreach (var damageType in modifiers.FlatReduction.Keys.ToArray())
            modifiers.FlatReduction[damageType] *= flatModifier;
    }

    private void OnQualityTransfer(Entity<QualityComponent> ent, ref QualityTransferEvent args)
    {
        CopyQuality(ent, args.Created);
    }

    private void OnStackSplit(Entity<QualityComponent> ent, ref StackSplitEvent args)
    {
        CopyQuality(ent, args.NewId);
    }

    private void OnAttemptMergeStack(Entity<QualityComponent> ent, ref AttemptMergeStackEvent args)
    {
        if (!_qualityQuery.TryComp(args.OtherStack, out var other) ||
            other.Quality != ent.Comp.Quality ||
            other.QualityModifiers != ent.Comp.QualityModifiers ||
            other.QualityFactors != ent.Comp.QualityFactors ||
            !LevelDeltasMatch(other.LevelDeltas, ent.Comp.LevelDeltas))
        {
            args.Cancelled = true;
        }
    }

    public void CopyQuality(Entity<QualityComponent> original, EntityUid created)
    {
        if (EnsureComp<QualityComponent>(created, out var qualityAlreadyPresent))
        {
            qualityAlreadyPresent.QualityModifiers += original.Comp.Quality * 5;
            Dirty(created, qualityAlreadyPresent);
            return;
        }

        qualityAlreadyPresent.LevelDeltas = new Dictionary<EntProtoId, int>(original.Comp.LevelDeltas);
        qualityAlreadyPresent.Quality = original.Comp.Quality;
        qualityAlreadyPresent.QualityModifiers = original.Comp.QualityModifiers;
        qualityAlreadyPresent.QualityFactors = original.Comp.QualityFactors;
        Dirty(created, qualityAlreadyPresent);
        ApplyQuality((created, qualityAlreadyPresent));
    }

    /// <summary>
    /// Applies immutable quality effects once. Calling this again is a no-op.
    /// </summary>
    public bool ApplyQuality(Entity<QualityComponent> ent)
    {
        if (ent.Comp.Applied || !_prototypes.Resolve(ent.Comp.QualityFactors, out var prototype))
            return false;

        ent.Comp.Applied = true;
        Dirty(ent);
        _nameModifier.RefreshNameModifiers(ent.Owner);

        var ev = new ApplyQualityEvent(ent.Comp.Quality, prototype);
        RaiseLocalEvent(ent.Owner, ref ev);
        return true;
    }

    /// <summary>
    /// Rolls quality once. Supplying a roll is intended for deterministic integration tests.
    /// </summary>
    public int RollQuality(Entity<QualityComponent> ent, EntityUid user, int? roll = null)
    {
        if (ent.Comp.Applied)
            return ent.Comp.Quality;

        var rolled = Math.Clamp(roll ?? PredictedRoll(ent.Owner), 1, 99);
        var modifier = 100 - rolled * 2;

        if (_knowledge.GetContainer(user) is { } store)
        {
            if (_knowledge.SkillsEnabled)
            {
                var (primary, _, lowestDelta, _) = FindLowestDelta(store, ent.Comp.LevelDeltas);
                var primaryLevel = _knowledge.GetKnowledge(store, primary)?.Comp.NetLevel ?? -1;
                modifier = primaryLevel + lowestDelta * 15 + ent.Comp.Quality + ent.Comp.QualityModifiers - rolled;
            }
        }
        else
        {
            ApplyQuality(ent);
            return ent.Comp.Quality;
        }

        ent.Comp.Quality = QualityFromModifier(modifier);
        Dirty(ent);
        ApplyQuality(ent);
        // Pirate: Trauma intentionally removed passive crafting XP; progression uses explicit knowledge grants.
        return ent.Comp.Quality;
    }

    public (EntProtoId Primary, EntProtoId? Lowest, int LowestDelta, int RequiredMastery) FindLowestDelta(
        Entity<KnowledgeContainerComponent> store,
        IReadOnlyDictionary<EntProtoId, int> requirements)
    {
        var primary = requirements.Keys
            .Where(id => _knowledge.AllKnowledges.TryGetValue(id, out var prototype) &&
                         prototype.Category == CraftingCategory)
            .OrderBy(id => id.Id)
            .FirstOrDefault(FabricationKnowledge);

        EntProtoId? lowest = null;
        var lowestDelta = 0;
        var requiredMastery = 0;

        foreach (var (id, required) in requirements.OrderBy(pair => pair.Key.Id))
        {
            if (id == primary)
                continue;

            var actual = _knowledge.GetKnowledge(store, id) is { } knowledge
                ? SharedKnowledgeSystem.GetMastery(knowledge.Comp.NetLevel)
                : 0;
            var delta = actual - required;
            if (lowest is not null && delta >= lowestDelta)
                continue;

            lowest = id;
            lowestDelta = delta;
            requiredMastery = required;
        }

        return (primary, lowest, lowestDelta, requiredMastery);
    }

    public static int QualityFromModifier(int modifier)
        => modifier switch
        {
            >= 88 => 5,
            >= 44 => 4,
            >= 20 => 3,
            >= 10 => 2,
            >= 5 => 1,
            >= 0 => 0,
            >= -5 => -1,
            >= -10 => -2,
            >= -20 => -3,
            >= -44 => -4,
            _ => -5,
        };

    public static float QualityModifier(float quality, float power = 1.1f)
        => MathF.Pow(power, quality);

    private int PredictedRoll(EntityUid entity)
    {
        var seed = Content.Shared.Random.Helpers.SharedRandomExtensions.HashCodeCombine(
            (int) _timing.CurTick.Value,
            GetNetEntity(entity).Id,
            0x5155414C);
        return new System.Random(seed).Next(1, 100);
    }

    private static bool LevelDeltasMatch(
        IReadOnlyDictionary<EntProtoId, int> first,
        IReadOnlyDictionary<EntProtoId, int> second)
    {
        if (first.Count != second.Count)
            return false;

        foreach (var (id, delta) in first)
        {
            if (!second.TryGetValue(id, out var otherDelta) || otherDelta != delta)
                return false;
        }

        return true;
    }
}
