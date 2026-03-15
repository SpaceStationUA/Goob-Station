using System;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Pirate;
using Content.Shared._Pirate.Movement.Pulling.Events;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Armor;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Item;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Movement.Pulling.Systems;

public sealed partial class PullingSystem
{
    /// <summary>
    /// prevent infinite loop after attacking the body part
    /// </summary>
    private readonly Dictionary<EntityUid, GrabFollowupSuppression> _followupSuppressions = new();
    private readonly List<EntityUid> _expiredFollowups = new();
    private static readonly TimeSpan suppressionDuration = TimeSpan.FromSeconds(1);
    private TimeSpan _nextFollowupSuppressionPrune;

    private readonly record struct GrabFollowupSuppression(EntityUid Target, TimeSpan ExpiresAt);

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_followupSuppressions.Count == 0 || _timing.CurTime < _nextFollowupSuppressionPrune)
            return;

        _nextFollowupSuppressionPrune = _timing.CurTime + suppressionDuration;
        PruneExpiredFollowupSuppressions();
    }

    private void InitializePirateGrabActions()
    {
        SubscribeLocalEvent<PullableComponent, BeforeHarmfulActionEvent>(OnPullableBeforeHarmfulAction);
        SubscribeLocalEvent<PullerComponent, BoneCrushDoAfterEvent>(OnGrabBoneBreakDoAfter);
        SubscribeLocalEvent<PullerComponent, ThroatSliceDoAfterEvent>(OnGrabThroatSliceDoAfter);
    }

    private void OnPullableBeforeHarmfulAction(Entity<PullableComponent> ent, ref BeforeHarmfulActionEvent args)
    {
        if (args.Type != HarmfulActionType.Harm)
            return;

        if (TryConsumeGrabFollowupSuppression(args.User, ent.Owner))
            return;

        if (!TryComp<PullerComponent>(args.User, out var pullerComp))
            return;

        if (!_handsSystem.TryGetActiveItem(args.User, out var used)
            || used is not { } usedItem)
            return;

        if (CanStartThroatSliceDoAfter((args.User, pullerComp), ent, usedItem, out var throatSlicePart, out var throatBlockingItem))
        {
            var sliceDoAfter = new DoAfterArgs(
                EntityManager,
                args.User,
                pullerComp.ThroatSliceDelay,
                new ThroatSliceDoAfterEvent(throatSlicePart),
                args.User,
                target: ent.Owner,
                used: usedItem)
            {
                NeedHand = true,
                BreakOnMove = true,
                BreakOnDamage = true,
                MultiplyDelay = false,
                ShowTo = ent.Owner,
            };

            if (!_doAfter.TryStartDoAfter(sliceDoAfter))
                return;

            if (_netManager.IsServer)
                _audio.PlayPvs(new SoundPathSpecifier(PirateAudio.ButcherEffect), ent.Owner);

            PopupGrabAction(args.User,
                ent.Owner,
                "popup-grab-throat-slice-start",
                PopupType.MediumCaution,
                ("tool", usedItem));

            args.Cancel();
            return;
        }

        if (throatBlockingItem is { } blockingItem)
        {
            PopupGrabActionCancelled(args.User, "popup-grab-throat-slice-cancel-covered", ("item", blockingItem));
            return;
        }

        if (!CanStartBoneBreakDoAfter((args.User, pullerComp), ent, usedItem, out var targetPart))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            pullerComp.BoneBreakDelay,
            new BoneCrushDoAfterEvent(targetPart),
            args.User,
            target: ent.Owner,
            used: usedItem)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            MultiplyDelay = false,
            ShowTo = ent.Owner,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var limb = GetGrabLimbName(targetPart);
        PopupGrabAction(args.User,
            ent.Owner,
            "popup-grab-bone-break-start",
            PopupType.MediumCaution,
            ("tool", usedItem),
            ("limb", limb));

        args.Cancel();
    }

    private void OnGrabBoneBreakDoAfter(Entity<PullerComponent> ent, ref BoneCrushDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled || args.Target is not { } target || args.Used is not { } used)
        {
            PopupGrabActionCancelled(ent.Owner, "popup-grab-bone-break-cancel-interrupted");
            return;
        }

        if (!TryComp<PullableComponent>(target, out var pullableComp))
        {
            PopupGrabActionCancelled(ent.Owner, "popup-grab-bone-break-cancel-target-lost");
            return;
        }

        if (!CanStartBoneBreakDoAfter(ent, (target, pullableComp), used, out var targetPart)
            || targetPart != args.TargetPart)
        {
            PopupGrabActionCancelled(ent.Owner, "popup-grab-bone-break-cancel-conditions");
            return;
        }

        if (!TryGetLegBone(target, args.TargetPart, out var bone, out var broken))
        {
            if (broken)
                PopupGrabActionCancelled(ent.Owner, "popup-grab-bone-break-cancel-already-broken");
            else
                PopupGrabActionCancelled(ent.Owner, "popup-grab-bone-break-cancel-no-bone");
            return;
        }

        var bluntDamage = GetBluntDamage(used);
        if (bluntDamage <= 0f)
        {
            PopupGrabActionCancelled(ent.Owner, "popup-grab-bone-break-cancel-invalid-weapon");
            return;
        }

        var chance = Math.Clamp(
            ent.Comp.BoneBreakBaseChance + bluntDamage * ent.Comp.BoneBreakChancePerBluntDamage,
            ent.Comp.BoneBreakBaseChance,
            ent.Comp.BoneBreakMaxChance);
        var limb = GetGrabLimbName(args.TargetPart);

        if (!_random.Prob(chance))
        {
            ApplyEmpoweredMeleeHit(ent.Owner, target, used, args.TargetPart, ent.Comp.BoneBreakStrikeMultiplier);

            PopupGrabAction(ent.Owner,
                target,
                "popup-grab-bone-break-fail",
                PopupType.MediumCaution,
                ("limb", limb));

            return;
        }

        ApplyEmpoweredMeleeHit(ent.Owner, target, used, args.TargetPart, ent.Comp.BoneBreakStrikeMultiplier);
        ApplyFinisherDamage(target,
            ent.Owner,
            args.TargetPart,
            "Blunt",
            ent.Comp.BoneBreakBaseDamage + bluntDamage * ent.Comp.BoneBreakDamagePerBluntDamage,
            ent.Comp.BoneBreakTraumaWoundSeverity);

        PopupGrabAction(ent.Owner,
            target,
            "popup-grab-bone-break-success",
            PopupType.LargeCaution,
            ("limb", limb));
    }

    private void OnGrabThroatSliceDoAfter(Entity<PullerComponent> ent, ref ThroatSliceDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled || args.Target is not { } target || args.Used is not { } used)
        {
            PopupGrabActionCancelled(ent.Owner, "popup-grab-throat-slice-cancel-interrupted");
            return;
        }

        if (!TryComp<PullableComponent>(target, out var pullableComp))
        {
            PopupGrabActionCancelled(ent.Owner, "popup-grab-throat-slice-cancel-target-lost");
            return;
        }

        if (!CanStartThroatSliceDoAfter(ent, (target, pullableComp), used, out var targetPart, out var blockingItem)
            || targetPart != args.TargetPart)
        {
            if (blockingItem is { } coveredBy)
                PopupGrabActionCancelled(ent.Owner, "popup-grab-throat-slice-cancel-covered", ("item", coveredBy));
            else
                PopupGrabActionCancelled(ent.Owner, "popup-grab-throat-slice-cancel-conditions");
            return;
        }

        if (!TryGetHeadWoundable(target, out var headWoundable))
        {
            PopupGrabActionCancelled(ent.Owner, "popup-grab-throat-slice-cancel-no-throat");
            return;
        }

        var sharpDamage = GetSharpDamage(used);
        if (sharpDamage <= 0f)
        {
            PopupGrabActionCancelled(ent.Owner, "popup-grab-throat-slice-cancel-invalid-weapon");
            return;
        }

        ApplyEmpoweredMeleeHit(ent.Owner, target, used, args.TargetPart, ent.Comp.ThroatSliceStrikeMultiplier);
        ApplyFinisherDamage(target,
            ent.Owner,
            args.TargetPart,
            "Slash",
            ent.Comp.ThroatSliceBaseSeverity + sharpDamage * ent.Comp.ThroatSliceSeverityPerSharpDamage,
            ent.Comp.ThroatSliceWoundSeverityMultiplier);

        PopupGrabAction(ent.Owner,
            target,
            "popup-grab-throat-slice-success",
            PopupType.LargeCaution,
            ("tool", used));
    }

    private bool CanStartBoneBreakDoAfter(
        Entity<PullerComponent> puller,
        Entity<PullableComponent> pullable,
        EntityUid used,
        out TargetBodyPart targetPart)
    {
        targetPart = TargetBodyPart.Chest;

        if (puller.Comp.Pulling != pullable.Owner
            || pullable.Comp.Puller != puller.Owner
            || puller.Comp.GrabStage != GrabStage.Hard
            || !_combatMode.IsInCombatMode(puller.Owner)
            || !_blocker.CanAttack(puller.Owner, pullable.Owner)
            || !TryComp<MobStateComponent>(pullable.Owner, out _)
            || !TryComp<TargetingComponent>(puller.Owner, out var targeting))
            return false;

        if (TryComp<PullableComponent>(puller.Owner, out var pullerAsPullable) && pullerAsPullable.Puller != null)
            return false;

        if (targeting.Target is not (TargetBodyPart.LeftLeg or TargetBodyPart.RightLeg))
            return false;

        targetPart = targeting.Target;
        return GetBluntDamage(used) > 0f;
    }

    private bool CanStartThroatSliceDoAfter(
        Entity<PullerComponent> puller,
        Entity<PullableComponent> pullable,
        EntityUid used,
        out TargetBodyPart targetPart,
        out EntityUid? blockingItem)
    {
        targetPart = TargetBodyPart.Chest;
        blockingItem = null;

        if (puller.Comp.Pulling != pullable.Owner
            || pullable.Comp.Puller != puller.Owner
            || puller.Comp.GrabStage != GrabStage.Hard
            || !_combatMode.IsInCombatMode(puller.Owner)
            || !_blocker.CanAttack(puller.Owner, pullable.Owner)
            || !TryComp<MobStateComponent>(pullable.Owner, out _)
            || !TryComp<TargetingComponent>(puller.Owner, out var targeting))
            return false;

        if (TryComp<PullableComponent>(puller.Owner, out var pullerAsPullable) && pullerAsPullable.Puller != null)
            return false;

        if (targeting.Target != TargetBodyPart.Head)
            return false;

        if (!TryGetHeadWoundable(pullable.Owner, out _))
            return false;

        if (TryGetThroatCoveringItem(pullable.Owner, out var coveringItem))
        {
            blockingItem = coveringItem;
            return false;
        }

        targetPart = targeting.Target;
        return GetSharpDamage(used) > 0f;
    }

    private bool TryGetThroatCoveringItem(EntityUid target, out EntityUid coveringItem)
    {
        coveringItem = default;
        const SlotFlags throatCoveringSlots = SlotFlags.MASK | SlotFlags.NECK;
        if (!_inventory.TryGetContainerSlotEnumerator(target, out var containerSlotEnumerator, throatCoveringSlots))
            return false;

        while (containerSlotEnumerator.MoveNext(out var containerSlot))
        {
            if (!containerSlot.ContainedEntity.HasValue)
                continue;

            var item = containerSlot.ContainedEntity.Value;
            if (!TryComp<ArmorComponent>(item, out var armor)
                || !armor.ArmorCoverage.Contains(BodyPartType.Head))
                continue;

            coveringItem = item;
            return true;
        }

        return false;
    }

    private float GetBluntDamage(EntityUid used)
    {
        if (!TryComp<ItemComponent>(used, out _)
            || !TryComp<MeleeWeaponComponent>(used, out var melee))
            return 0f;

        return melee.Damage.DamageDict.TryGetValue("Blunt", out var blunt)
            ? blunt.Float()
            : 0f;
    }

    private float GetSharpDamage(EntityUid used)
    {
        if (!TryComp<ItemComponent>(used, out _)
            || !TryComp<MeleeWeaponComponent>(used, out var melee))
            return 0f;

        var slash = melee.Damage.DamageDict.TryGetValue("Slash", out var slashDamage)
            ? slashDamage.Float()
            : 0f;

        var piercing = melee.Damage.DamageDict.TryGetValue("Piercing", out var piercingDamage)
            ? piercingDamage.Float()
            : 0f;

        return slash + piercing;
    }

    private bool TryGetLegBone(
        EntityUid target,
        TargetBodyPart targetPart,
        out (EntityUid Bone, BoneComponent BoneComp) bone,
        out bool alreadyBroken)
    {
        bone = default;
        alreadyBroken = false;

        if (!TryComp<BodyComponent>(target, out var body))
            return false;

        var (partType, symmetry) = _body.ConvertTargetBodyPart(targetPart);
        if (partType != BodyPartType.Leg)
            return false;

        var bodyPart = _body.GetBodyChildrenOfType(target, partType, body, symmetry).FirstOrNull();
        if (bodyPart == null
            || !TryComp<WoundableComponent>(bodyPart.Value.Id, out var woundable))
            return false;

        var boneEnt = woundable.Bone.ContainedEntities.FirstOrNull();
        if (boneEnt == null
            || !TryComp<BoneComponent>(boneEnt.Value, out var boneComp))
            return false;

        if (boneComp.BoneSeverity == BoneSeverity.Broken)
        {
            alreadyBroken = true;
            return false;
        }

        bone = (boneEnt.Value, boneComp);
        return true;
    }

    private bool TryGetHeadWoundable(EntityUid target, out EntityUid headWoundable)
    {
        headWoundable = default;

        if (!TryComp<BodyComponent>(target, out var body))
            return false;

        var headPart = _body.GetBodyChildrenOfType(target, BodyPartType.Head, body, BodyPartSymmetry.None).FirstOrNull();
        if (headPart == null
            || !TryComp<WoundableComponent>(headPart.Value.Id, out _))
            return false;

        headWoundable = headPart.Value.Id;
        return true;
    }

    private void ApplyEmpoweredMeleeHit(
        EntityUid attacker,
        EntityUid target,
        EntityUid used,
        TargetBodyPart targetPart,
        float multiplier)
    {
        if (multiplier <= 0f
            || !TryComp<MeleeWeaponComponent>(used, out var melee))
            return;

        if (!TryComp<TargetingComponent>(attacker, out var targeting))
            return;

        var originalTarget = targeting.Target;
        var originalDamage = new DamageSpecifier(melee.Damage);
        targeting.Target = targetPart;
        melee.Damage = originalDamage * multiplier;
        Dirty(attacker, targeting);
        Dirty(used, melee);

        try
        {
            TryPerformFollowupAttack(attacker, target, used, melee);
        }
        finally
        {
            melee.Damage = originalDamage;
            targeting.Target = originalTarget;
            Dirty(used, melee);
            Dirty(attacker, targeting);
        }
    }

    /// <summary>
    /// Three-tier follow-up for grab finishers:
    /// 1) Try <see cref="SharedMeleeWeaponSystem.AttemptLightAttack"/> normally.
    /// 2) If that fails due to cooldown, temporarily clamp <c>melee.NextAttack</c> to now and try
    ///    <see cref="SharedMeleeWeaponSystem.AttemptLightAttack"/> again.
    /// 3) Last resort: call the public virtual <see cref="SharedMeleeWeaponSystem.DoLightAttack"/>
    ///    entry point directly.
    ///
    /// We intentionally use the same public API path other systems use for forced melee interactions.
    /// The temporary <c>melee.NextAttack</c> mutation is an established pattern in this codebase
    /// (see <c>SharedHereticBladeSystem</c>) and is safe here because we restore timing when needed.
    /// This preserves standard melee checks/effects (range/LOS, damageability, harmful-action hooks,
    /// sounds, lunge and damage effects) while minimizing random no-hit outcomes.
    /// TODO: If API clarity becomes important, consider a dedicated ForceAttack helper in melee.
    /// </summary>
    private void TryPerformFollowupAttack(
        EntityUid attacker,
        EntityUid target,
        EntityUid used,
        MeleeWeaponComponent melee)
    {
        ArmGrabFollowupSuppression(attacker, target);
        if (_melee.AttemptLightAttack(attacker, used, melee, target))
            return;
        ClearGrabFollowupSuppression(attacker);

        var originalNextAttack = melee.NextAttack;
        var touchedCooldown = false;

        if (melee.NextAttack > _timing.CurTime)
        {
            melee.NextAttack = _timing.CurTime;
            Dirty(used, melee);
            touchedCooldown = true;
        }

        ArmGrabFollowupSuppression(attacker, target);
        if (_melee.AttemptLightAttack(attacker, used, melee, target))
            return;
        ClearGrabFollowupSuppression(attacker);

        // Last resort: run the light attack path directly. This still does range/LOS,
        // harm event checks, sounds, lunge and damage effects.
        var coords = Transform(target).Coordinates;
        ArmGrabFollowupSuppression(attacker, target);
        var forcedLight = new LightAttackEvent(
            GetNetEntity(target),
            GetNetEntity(used),
            GetNetCoordinates(coords));
        _melee.DoLightAttack(attacker, forcedLight, used, melee, null);
        ClearGrabFollowupSuppression(attacker);

        if (!touchedCooldown)
            return;

        // If both attempt paths failed and direct path didn't consume cooldown,
        // preserve the old timing window.
        if (melee.NextAttack <= _timing.CurTime)
        {
            melee.NextAttack = originalNextAttack;
            Dirty(used, melee);
        }
    }

    private void ArmGrabFollowupSuppression(EntityUid attacker, EntityUid target)
    {
        _followupSuppressions[attacker] = new GrabFollowupSuppression(
            target,
            _timing.CurTime + suppressionDuration);
    }

    private void ClearGrabFollowupSuppression(EntityUid attacker)
    {
        _followupSuppressions.Remove(attacker);
    }

    private bool TryConsumeGrabFollowupSuppression(EntityUid attacker, EntityUid target)
    {
        if (!_followupSuppressions.TryGetValue(attacker, out var suppression))
            return false;

        if (suppression.ExpiresAt < _timing.CurTime)
        {
            _followupSuppressions.Remove(attacker);
            return false;
        }

        if (suppression.Target != target)
            return false;

        _followupSuppressions.Remove(attacker);
        return true;
    }

    private void PruneExpiredFollowupSuppressions()
    {
        var now = _timing.CurTime;
        _expiredFollowups.Clear();

        foreach (var (uid, suppression) in _followupSuppressions)
        {
            if (suppression.ExpiresAt < now)
                _expiredFollowups.Add(uid);
        }

        foreach (var uid in _expiredFollowups)
        {
            _followupSuppressions.Remove(uid);
        }
    }

    private void ApplyFinisherDamage(
        EntityUid target,
        EntityUid attacker,
        TargetBodyPart part,
        string damageType,
        float damageAmount,
        float woundSeverityMultiplier)
    {
        if (damageAmount <= 0f)
            return;

        var spec = new DamageSpecifier();
        spec.DamageDict[damageType] = FixedPoint2.New(damageAmount);
        spec.WoundSeverityMultipliers[damageType] = woundSeverityMultiplier;

        _damageable.TryChangeDamage(target,
            spec,
            origin: attacker,
            targetPart: part,
            canMiss: false);
    }

    private void PopupGrabActionCancelled(
        EntityUid user,
        string message,
        params (string Name, object Value)[] extraArgs)
    {
        if (!_netManager.IsServer)
            return;

        var normalizedArgs = new List<(string, object)>(extraArgs.Length);
        foreach (var (name, value) in extraArgs)
        {
            normalizedArgs.Add((name, value is EntityUid uid ? Identity.Entity(uid, EntityManager) : value));
        }

        _popup.PopupEntity(
            Loc.GetString(message, normalizedArgs.ToArray()),
            user,
            user,
            PopupType.MediumCaution);
    }

    private void PopupGrabAction(
        EntityUid user,
        EntityUid target,
        string keyPrefix,
        PopupType type,
        params (string Name, object Value)[] extraArgs)
    {
        if (!_netManager.IsServer)
            return;

        var targetIdentity = Identity.Entity(target, EntityManager);
        var userIdentity = Identity.Entity(user, EntityManager);

        var selfArgs = new List<(string, object)> { ("target", targetIdentity) };
        var targetArgs = new List<(string, object)> { ("puller", userIdentity) };
        var othersArgs = new List<(string, object)>
        {
            ("puller", userIdentity),
            ("target", targetIdentity),
        };

        foreach (var (name, value) in extraArgs)
        {
            var normalized = value is EntityUid uid
                ? Identity.Entity(uid, EntityManager)
                : value;

            selfArgs.Add((name, normalized));
            targetArgs.Add((name, normalized));
            othersArgs.Add((name, normalized));
        }

        _popup.PopupEntity(
            Loc.GetString($"{keyPrefix}-self", selfArgs.ToArray()),
            user,
            user,
            type);

        _popup.PopupEntity(
            Loc.GetString($"{keyPrefix}-target", targetArgs.ToArray()),
            target,
            target,
            type);

        _popup.PopupEntity(
            Loc.GetString($"{keyPrefix}-others", othersArgs.ToArray()),
            target,
            Filter.Pvs(target, entityManager: EntityManager)
                .RemovePlayerByAttachedEntity(user)
                .RemovePlayerByAttachedEntity(target),
            true,
            type);
    }

    private string GetGrabLimbName(TargetBodyPart targetPart)
    {
        return targetPart switch
        {
            TargetBodyPart.LeftLeg => Loc.GetString("popup-grab-limb-left-leg"),
            TargetBodyPart.RightLeg => Loc.GetString("popup-grab-limb-right-leg"),
            _ => Loc.GetString("popup-grab-limb-leg"),
        };
    }
}
