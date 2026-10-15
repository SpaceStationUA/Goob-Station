// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Weapons.Ranged;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Event-driven effects for general skills. Each event performs a fixed number of direct skill
/// lookups; this system has no update loop and never enumerates players or world entities.
/// </summary>
public sealed class KnowledgeGameplaySystem : EntitySystem
{
    public static readonly EntProtoId ShootingKnowledge = "ShootingKnowledge";
    public static readonly EntProtoId FirstAidKnowledge = "FirstAidKnowledge";
    public static readonly EntProtoId ShieldKnowledge = "KnowledgeWeaponsShield";
    public static readonly EntProtoId ThrowingKnowledge = "ThrowingKnowledge";
    public static readonly EntProtoId JanitorKnowledge = "JanitorKnowledge";
    public static readonly EntProtoId CookingKnowledge = "CookingKnowledge";

    public const float MaxThrowSpeedBonus = 1f;

    public const float UnskilledThrownDamage = 0.9f;

    public const int TrainedThrowingLevel = 25;

    public const float MaxThrownDamageBonus = 0.25f;

    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private EntityQuery<AimSpeedKnowledgeComponent> _aimQuery;
    private EntityQuery<InjectTimeKnowledgeComponent> _injectQuery;
    private EntityQuery<BlockFractionKnowledgeComponent> _blockQuery;
    private EntityQuery<ThrowInsertKnowledgeComponent> _throwInsertQuery;
    private EntityQuery<ExperienceOnCookingComponent> _cookingQuery;

    public override void Initialize()
    {
        base.Initialize();

        _aimQuery = GetEntityQuery<AimSpeedKnowledgeComponent>();
        _injectQuery = GetEntityQuery<InjectTimeKnowledgeComponent>();
        _blockQuery = GetEntityQuery<BlockFractionKnowledgeComponent>();
        _throwInsertQuery = GetEntityQuery<ThrowInsertKnowledgeComponent>();
        _cookingQuery = GetEntityQuery<ExperienceOnCookingComponent>();

        SubscribeLocalEvent<KnowledgeHolderComponent, GetRecoilModifiersEvent>(OnGetRecoilModifiers);
        SubscribeLocalEvent<KnowledgeHolderComponent, AmmoShotUserEvent>(OnAmmoShot);
        // Pirate: EnergyDomeSystem owns ProjectileComponent/ProjectileHitEvent on the server.
        // Projectile hits originate from physics collisions, so use that component as the event anchor.
        SubscribeLocalEvent<PhysicsComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<KnowledgeHolderComponent, UserModifyInjectTimeEvent>(OnModifyInjectTime);
        SubscribeLocalEvent<KnowledgeHolderComponent, GetBlockFractionEvent>(OnGetBlockFraction);
        SubscribeLocalEvent<KnowledgeHolderComponent, ModifyThrownSpeedEvent>(OnModifyThrownSpeed);
        SubscribeLocalEvent<KnowledgeHolderComponent, ModifyThrowInsertChanceEvent>(OnModifyThrowInsertChance);
        SubscribeLocalEvent<KnowledgeHolderComponent, CookedFoodEvent>(OnCookedFood);
    }

    private void OnGetRecoilModifiers(Entity<KnowledgeHolderComponent> ent, ref GetRecoilModifiersEvent args)
    {
        if (!_knowledge.SkillsEnabled || args.Gun == args.User ||
            _knowledge.GetKnowledge(ent.Owner, ShootingKnowledge) is not { } skill ||
            !_aimQuery.TryComp(skill.Owner, out var effect))
            return;

        args.Modifier /= effect.Curve.GetCurve(skill.Comp.NetLevel);
    }

    private void OnAmmoShot(Entity<KnowledgeHolderComponent> ent, ref AmmoShotUserEvent args)
    {
        if (_knowledge.GetContainer(ent.Owner) is { } store)
            _knowledge.AddExperience(store, ShootingKnowledge, 1, 20);
    }

    private void OnProjectileHit(Entity<PhysicsComponent> _, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } shooter || !_mobState.IsAlive(args.Target) ||
            _knowledge.GetContainer(shooter) is not { } store)
            return;

        _knowledge.AddExperience(store, ShootingKnowledge, 1, 10);
    }

    private void OnModifyInjectTime(Entity<KnowledgeHolderComponent> ent, ref UserModifyInjectTimeEvent args)
    {
        if (!_knowledge.SkillsEnabled || args.Delay <= TimeSpan.Zero ||
            _knowledge.GetKnowledge(ent.Owner, FirstAidKnowledge) is not { } skill ||
            !_injectQuery.TryComp(skill.Owner, out var effect))
            return;

        args.Delay *= effect.Curve.GetCurve(skill.Comp.NetLevel);
    }

    private void OnGetBlockFraction(Entity<KnowledgeHolderComponent> ent, ref GetBlockFractionEvent args)
    {
        if (!_knowledge.SkillsEnabled ||
            _knowledge.GetKnowledge(ent.Owner, ShieldKnowledge) is not { } skill ||
            !_blockQuery.TryComp(skill.Owner, out var effect))
            return;

        args.Fraction *= effect.Curve.GetCurve(skill.Comp.NetLevel);
    }

    public static float ThrowSpeedMultiplier(int level)
        => 1f + MaxThrowSpeedBonus * Math.Clamp(level, 0, 100) / 100f;

    public static float ThrownDamageMultiplier(int level)
    {
        level = Math.Clamp(level, 0, 100);
        if (level < TrainedThrowingLevel)
            return UnskilledThrownDamage + (1f - UnskilledThrownDamage) * level / TrainedThrowingLevel;

        return 1f + MaxThrownDamageBonus * (level - TrainedThrowingLevel) / (100f - TrainedThrowingLevel);
    }

    public float GetThrownDamageMultiplier(EntityUid? thrower)
    {
        if (!_knowledge.SkillsEnabled || thrower is not { } user ||
            _knowledge.GetKnowledge(user, ThrowingKnowledge) is not { } skill)
            return 1f;

        return ThrownDamageMultiplier(skill.Comp.NetLevel);
    }

    private void OnModifyThrownSpeed(Entity<KnowledgeHolderComponent> ent, ref ModifyThrownSpeedEvent args)
    {
        if (!_knowledge.SkillsEnabled || _knowledge.GetContainer(ent.Owner) is not { } store)
            return;

        if (_knowledge.GetKnowledge(store, ThrowingKnowledge) is { } skill)
            args.BaseThrowSpeed *= ThrowSpeedMultiplier(skill.Comp.NetLevel);

        _knowledge.AddExperience(store, ThrowingKnowledge, 1, Math.Clamp((int) args.Distance * 5, 0, 100));
    }

    private void OnModifyThrowInsertChance(Entity<KnowledgeHolderComponent> ent, ref ModifyThrowInsertChanceEvent args)
    {
        if (!_knowledge.SkillsEnabled)
            return;

        ApplyThrowInsertSkill(ent.Owner, ThrowingKnowledge, ref args);
        ApplyThrowInsertSkill(ent.Owner, JanitorKnowledge, ref args);
    }

    private void ApplyThrowInsertSkill(EntityUid holder, EntProtoId id, ref ModifyThrowInsertChanceEvent args)
    {
        if (_knowledge.GetKnowledge(holder, id) is not { } skill ||
            !_throwInsertQuery.TryComp(skill.Owner, out var effect))
            return;

        args.Chance += effect.Curve.GetCurve(skill.Comp.NetLevel);
    }

    private void OnCookedFood(Entity<KnowledgeHolderComponent> ent, ref CookedFoodEvent args)
    {
        if (_knowledge.GetKnowledge(ent.Owner, CookingKnowledge) is not { } skill ||
            !_cookingQuery.TryComp(skill.Owner, out var effect))
            return;

        if (effect.Cooked.Add(args.Result))
        {
            effect.Limit = effect.Cooked.Count;
            Dirty(skill.Owner, effect);
        }

        var experience = Math.Max(args.Count, 0) * effect.Scale;
        _knowledge.AddExperience(skill, args.User, experience, Math.Min(100, effect.Limit));
    }
}
