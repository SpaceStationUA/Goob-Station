// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Parry;

public sealed class ParrySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly WeaponClassSystem _weaponClass = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ReflectiveComponent> _reflectiveQuery;
    private EntityQuery<WeaponClassComponent> _classQuery;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _reflectiveQuery = GetEntityQuery<ReflectiveComponent>();
        _classQuery = GetEntityQuery<WeaponClassComponent>();

        SubscribeLocalEvent<HandsComponent, ParryAttemptEvent>(OnParry);
        SubscribeLocalEvent<ParryComponent, HeldRelayedEvent<ProjectileReflectAttemptEvent>>(OnReflectProjectile);
        SubscribeLocalEvent<ParryComponent, HeldRelayedEvent<HitScanReflectAttemptEvent>>(OnReflectHitscan);
        SubscribeLocalEvent<ParryComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ParryExhaustionComponent, ComponentStartup>(OnExhaustionStartup);
    }

    private void OnExhaustionStartup(Entity<ParryExhaustionComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.LastUpdate = _timing.CurTime;
        ent.Comp.Exhaustion = Math.Clamp(ent.Comp.Exhaustion, 0f, 1f);
        Dirty(ent);
    }

    private void OnParry(Entity<HandsComponent> ent, ref ParryAttemptEvent args)
    {
        if (args.Cancelled || args.Attacker == ent.Owner)
            return;

        foreach (var held in _hands.EnumerateHeld(ent.AsNullable()))
        {
            if (!TryComp<ParryComponent>(held, out var parry) ||
                !TryParry((held, parry), ent.Owner, args.Attacker))
            {
                continue;
            }

            args.Cancel();
            return;
        }
    }

    private void OnReflectProjectile(
        Entity<ParryComponent> ent,
        ref HeldRelayedEvent<ProjectileReflectAttemptEvent> args)
    {
        if (args.Args.Cancelled || !TryReflectProjectile(ent, args.Source, args.Args.ProjUid))
            return;

        args.Args.Cancelled = true;
    }

    private void OnReflectHitscan(Entity<ParryComponent> ent, ref HeldRelayedEvent<HitScanReflectAttemptEvent> args)
    {
        if (args.Args.Reflected ||
            !TryReflectHitscan(
                ent,
                args.Source,
                args.Args.Shooter,
                args.Args.SourceItem,
                args.Args.Direction,
                args.Args.Reflective,
                out var direction))
        {
            return;
        }

        args.Args.Direction = direction.Value;
        args.Args.Reflected = true;
    }

    public bool TryParry(Entity<ParryComponent> weapon, EntityUid user, EntityUid attacker)
    {
        if (user == attacker ||
            !_toggle.IsActivated(weapon.Owner) ||
            GetSkillLevel(user, weapon) < weapon.Comp.ParryMinSkill ||
            !TrySpendExhaustion(user, weapon.Comp.ParryExhaustionCost, reflect: false))
        {
            return false;
        }

        PlayAudioAndPopup(weapon.Comp.SoundOnParry, user, attacker);
        _adminLogger.Add(LogType.MeleeHit, LogImpact.Medium, $"{ToPrettyString(user):user} parried a melee strike from {ToPrettyString(attacker):attacker}");
        return true;
    }

    public bool TryReflectProjectile(
        Entity<ParryComponent> weapon,
        EntityUid user,
        EntityUid projectile)
    {
        if (!_reflectiveQuery.TryComp(projectile, out var reflective) ||
            !_physicsQuery.TryComp(projectile, out var physics) ||
            (weapon.Comp.Reflects & reflective.Reflective) == ReflectType.None ||
            !_toggle.IsActivated(weapon.Owner) ||
            GetSkillLevel(user, weapon) < weapon.Comp.ReflectMinSkill ||
            !TrySpendExhaustion(user, weapon.Comp.ReflectExhaustionCost, reflect: true))
        {
            return false;
        }

        var random = PredictedRandom(weapon.Owner, 0x50524F4A);
        var rotation = random.NextAngle(-weapon.Comp.ReflectSpread / 2, weapon.Comp.ReflectSpread / 2).Opposite();
        var existingVelocity = _physics.GetMapLinearVelocity(projectile, component: physics);
        var relativeVelocity = existingVelocity - _physics.GetMapLinearVelocity(user);
        var newVelocity = rotation.RotateVec(relativeVelocity);
        _physics.SetLinearVelocity(projectile, physics.LinearVelocity + newVelocity - existingVelocity, body: physics);

        var localRotation = Transform(projectile).LocalRotation;
        _transform.SetLocalRotation(projectile, rotation.RotateVec(localRotation.ToVec()).ToAngle());
        RemCompDeferred<HomingProjectileComponent>(projectile);

        EntityUid? shooter = null;
        if (TryComp<ProjectileComponent>(projectile, out var projectileComp))
        {
            shooter = projectileComp.Shooter;
            projectileComp.Shooter = user;
            projectileComp.Weapon = user;
            Dirty(projectile, projectileComp);
        }

        PlayAudioAndPopup(weapon.Comp.SoundOnReflect, user, shooter);
        _adminLogger.Add(LogType.BulletHit, LogImpact.Medium, $"{ToPrettyString(user):user} reflected {ToPrettyString(projectile):projectile}");
        return true;
    }

    public bool TryReflectHitscan(
        Entity<ParryComponent> weapon,
        EntityUid user,
        EntityUid? shooter,
        EntityUid shotSource,
        Vector2 direction,
        ReflectType reflectType,
        [NotNullWhen(true)] out Vector2? newDirection)
    {
        if ((weapon.Comp.Reflects & reflectType) == ReflectType.None ||
            !_toggle.IsActivated(weapon.Owner) ||
            GetSkillLevel(user, weapon) < weapon.Comp.ReflectMinSkill ||
            !TrySpendExhaustion(user, weapon.Comp.ReflectExhaustionCost, reflect: true))
        {
            newDirection = null;
            return false;
        }

        var random = PredictedRandom(weapon.Owner, 0x48495453);
        var spread = random.NextAngle(-weapon.Comp.ReflectSpread / 2, weapon.Comp.ReflectSpread / 2);
        newDirection = -spread.RotateVec(direction);
        PlayAudioAndPopup(weapon.Comp.SoundOnReflect, user, shooter);
        _adminLogger.Add(LogType.HitScanHit, LogImpact.Medium, $"{ToPrettyString(user):user} reflected hitscan from {ToPrettyString(shotSource):source}");
        return true;
    }

    public bool TrySpendExhaustion(EntityUid user, float cost, bool reflect)
    {
        if (cost < 0f || cost > 1f)
            return false;

        var component = EnsureComp<ParryExhaustionComponent>(user);
        var exhaustion = new Entity<ParryExhaustionComponent>(user, component);
        RefreshExhaustion(exhaustion);

        var maximum = reflect ? component.MaxReflectExhaustion : component.MaxParryExhaustion;
        if (component.Exhaustion >= maximum || component.Exhaustion + cost > 1f)
            return false;

        component.Exhaustion += cost;
        component.ExhaustionRegenTimer = _timing.CurTime + component.ExhaustionRegenDelay;
        component.LastUpdate = _timing.CurTime;
        Dirty(exhaustion);
        return true;
    }

    public float RefreshExhaustion(Entity<ParryExhaustionComponent> ent)
    {
        var now = _timing.CurTime;
        var regenStart = ent.Comp.LastUpdate > ent.Comp.ExhaustionRegenTimer
            ? ent.Comp.LastUpdate
            : ent.Comp.ExhaustionRegenTimer;

        var previous = ent.Comp.Exhaustion;
        if (now > regenStart && ent.Comp.ExhaustionRegenRate > 0f)
        {
            var elapsed = (float) (now - regenStart).TotalSeconds;
            ent.Comp.Exhaustion = Math.Clamp(previous - ent.Comp.ExhaustionRegenRate * elapsed, 0f, 1f);
        }

        ent.Comp.LastUpdate = now;
        if (!MathHelper.CloseTo(previous, ent.Comp.Exhaustion))
            Dirty(ent);

        return ent.Comp.Exhaustion;
    }

    public int GetSkillLevel(EntityUid user, Entity<ParryComponent> weapon)
    {
        if (!_knowledge.SkillsEnabled || !_classQuery.TryComp(weapon.Owner, out var weaponClass))
            return 100;

        return _weaponClass.GetSkillLevel((weapon.Owner, weaponClass), user);
    }

    private System.Random PredictedRandom(EntityUid uid, int salt)
    {
        var seed = SharedRandomExtensions.HashCodeCombine(
            (int) _timing.CurTick.Value,
            GetNetEntity(uid).Id,
            salt);
        return new System.Random(seed);
    }

    private void OnExamine(Entity<ParryComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.ParryExhaustionCost > 1f)
            return;

        if (GetSkillLevel(args.Examiner, ent) < ent.Comp.ParryMinSkill)
        {
            args.PushMarkup(Loc.GetString("parry-component-examine-lowskill"));
            return;
        }

        var exhaustion = EnsureComp<ParryExhaustionComponent>(args.Examiner);
        RefreshExhaustion((args.Examiner, exhaustion));
        var uses = ent.Comp.ParryExhaustionCost <= 0f
            ? int.MaxValue
            : (int) MathF.Ceiling(Math.Clamp(exhaustion.MaxParryExhaustion, 0f, 1f) / ent.Comp.ParryExhaustionCost);
        args.PushMarkup(Loc.GetString(
            uses == int.MaxValue ? "parry-component-examine-unlimited" : "parry-component-examine",
            ("value", uses)));
    }

    private void PlayAudioAndPopup(Robust.Shared.Audio.SoundSpecifier? sound, EntityUid user, EntityUid? source)
    {
        _popup.PopupPredicted(Loc.GetString("reflect-shot"), user, source);
        _audio.PlayPredicted(sound, user, source);
    }
}

/// <summary>
/// Relays one melee defense attempt through the existing hands-level harmful-action subscription.
/// </summary>
public sealed class ParryAttemptEvent(EntityUid attacker) : CancellableEntityEventArgs
{
    public EntityUid Attacker { get; } = attacker;
}
