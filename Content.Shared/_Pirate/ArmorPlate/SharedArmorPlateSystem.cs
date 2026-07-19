// SPDX-FileCopyrightText: 2025 Ark <189933909+ark1368@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 DevArchwave <168038123+DevArchwave@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 tonotom1 <tonotom@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.ArmorPlate;

/// <summary>
/// Handles armor plate insertion, protection, durability, and movement modifiers.
/// </summary>
public sealed partial class SharedArmorPlateSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorPlateHolderComponent, EntInsertedIntoContainerMessage>(OnPlateInserted);
        SubscribeLocalEvent<ArmorPlateHolderComponent, EntRemovedFromContainerMessage>(OnPlateRemoved);
        SubscribeLocalEvent<ArmorPlateHolderComponent, GotEquippedEvent>(OnEquippedArmor);
        SubscribeLocalEvent<ArmorPlateHolderComponent, GotUnequippedEvent>(OnUnequippedArmor);
        SubscribeLocalEvent<ArmorPlateHolderComponent, ExaminedEvent>(OnHolderExamined);
        SubscribeLocalEvent<ArmorPlateHolderComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<ArmorPlateItemComponent, GetVerbsEvent<ExamineVerb>>(OnPlateVerbExamine);
        SubscribeLocalEvent<ArmorPlateItemComponent, EntityTerminatingEvent>(OnPlateDestroyed);
        SubscribeLocalEvent<ArmorPlateItemComponent, ExaminedEvent>(OnPlateExamined);
        SubscribeLocalEvent<ArmorPlateProtectedComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    private void OnBeforeDamageChanged(Entity<ArmorPlateProtectedComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || !args.Damage.AnyPositive())
            return;

        if (!TryComp<InventoryComponent>(ent.Owner, out var inventory))
            return;

        if (!_inventory.TryGetSlots(ent, out var slots))
            return;

        if (args.Origin == null && args.OriginFlag != DamageableSystem.DamageOriginFlag.Explosion)
            return;

        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(ent, slot.Name, out var equipped, inventory))
                continue;

            if (!TryComp<ArmorPlateHolderComponent>(equipped, out var holder))
                continue;

            if (!TryGetActivePlate((equipped.Value, holder), out var plate))
                continue;

            CalcPlateDamages(args.Damage, plate.Comp, out var remainder, out var absorbed, out var plateDamage);
            AbsorbDamage(ent, plate, absorbed, plateDamage);

            if (remainder.Empty)
            {
                args.Cancelled = true;
                return;
            }

            args.Damage.DamageDict.Clear();
            foreach (var (type, amount) in remainder.DamageDict)
            {
                args.Damage.DamageDict.Add(type, amount);
            }
        }
    }

    private void AbsorbDamage(
        EntityUid wearer,
        Entity<ArmorPlateItemComponent> plate,
        FixedPoint2 absorbed,
        FixedPoint2 plateDamage)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", plateDamage);

        _damageable.TryChangeDamage(plate.Owner, damage, ignoreResistances: true);

        var staminaDamage = absorbed.Float() * plate.Comp.StaminaDamageMultiplier;
        _stamina.TakeStaminaDamage(wearer, staminaDamage);
    }

    private void OnPlateInserted(Entity<ArmorPlateHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        if (!TryComp<ArmorPlateItemComponent>(args.Entity, out var plate))
            return;

        if (ent.Comp.ActivePlate == null)
            SetActivePlate(ent, args.Entity, plate);
    }

    private void OnPlateRemoved(Entity<ArmorPlateHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != StorageComponent.ContainerId || ent.Comp.ActivePlate != args.Entity)
            return;

        ClearActivePlate(ent);

        if (!TryComp<StorageComponent>(ent, out var storage))
            return;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (!TryComp<ArmorPlateItemComponent>(item, out var plate))
                continue;

            SetActivePlate(ent, item, plate);
            break;
        }
    }

    private void OnHolderExamined(Entity<ArmorPlateHolderComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<StorageComponent>(ent, out _))
        {
            args.PushMarkup(Loc.GetString("armor-plate-examine-no-storage"));
            return;
        }

        if (ent.Comp.ActivePlate == null)
        {
            args.PushMarkup(Loc.GetString("armor-plate-examine-no-plate"));
            return;
        }

        var plateName = MetaData(ent.Comp.ActivePlate.Value).EntityName;

        if (!TryComp<ArmorPlateItemComponent>(ent.Comp.ActivePlate.Value, out var plate))
        {
            args.PushMarkup(Loc.GetString("armor-plate-examine-with-plate-simple", ("plateName", plateName)));
            return;
        }

        if (!TryComp<DamageableComponent>(ent.Comp.ActivePlate.Value, out var damageable))
        {
            args.PushMarkup(Loc.GetString("armor-plate-examine-with-plate-simple", ("plateName", plateName)));
            return;
        }

        var durabilityPercent = GetDurabilityPercent(plate, damageable);
        args.PushMarkup(Loc.GetString("armor-plate-examine-with-plate",
            ("plateName", plateName),
            ("percent", (int) durabilityPercent),
            ("durabilityColor", GetDurabilityColor(durabilityPercent))));
    }

    private void OnRefreshMoveSpeed(
        Entity<ArmorPlateHolderComponent> ent,
        ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        args.Args.ModifySpeed(ent.Comp.WalkSpeedModifier, ent.Comp.SprintSpeedModifier);
    }

    private void SetActivePlate(
        Entity<ArmorPlateHolderComponent> holder,
        EntityUid plateUid,
        ArmorPlateItemComponent plate)
    {
        holder.Comp.ActivePlate = plateUid;
        holder.Comp.WalkSpeedModifier = plate.WalkSpeedModifier;
        holder.Comp.SprintSpeedModifier = plate.SprintSpeedModifier;
        holder.Comp.StaminaDamageMultiplier = plate.StaminaDamageMultiplier;

        Dirty(holder);
        RefreshMovementSpeed(holder);
        RefreshPlateProtection(holder);
    }

    private void ClearActivePlate(Entity<ArmorPlateHolderComponent> holder)
    {
        holder.Comp.ActivePlate = null;
        holder.Comp.WalkSpeedModifier = 1.0f;
        holder.Comp.SprintSpeedModifier = 1.0f;
        holder.Comp.StaminaDamageMultiplier = 1.0f;

        Dirty(holder);
        RefreshMovementSpeed(holder);
        RefreshPlateProtection(holder);
    }

    private void RefreshMovementSpeed(EntityUid armorUid)
    {
        if (_inventory.TryGetContainingEntity(armorUid, out var wearer))
            _movementSpeed.RefreshMovementSpeedModifiers(wearer.Value);
    }

    public bool TryGetActivePlate(
        Entity<ArmorPlateHolderComponent?> holder,
        out Entity<ArmorPlateItemComponent> plate)
    {
        plate = default;

        if (!Resolve(holder, ref holder.Comp, logMissing: false) || holder.Comp.ActivePlate == null)
            return false;

        if (!TryComp<ArmorPlateItemComponent>(holder.Comp.ActivePlate.Value, out var plateComp))
            return false;

        plate = (holder.Comp.ActivePlate.Value, plateComp);
        return true;
    }

    public void CalcPlateDamages(
        DamageSpecifier incoming,
        ArmorPlateItemComponent plate,
        out DamageSpecifier remainder,
        out FixedPoint2 absorbedTotal,
        out FixedPoint2 plateDamageTotal)
    {
        remainder = new DamageSpecifier();
        absorbedTotal = FixedPoint2.Zero;
        plateDamageTotal = FixedPoint2.Zero;

        foreach (var (type, amount) in incoming.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                continue;

            var multiplier = plate.DamageMultipliers.GetValueOrDefault(type, 1.0f);
            var ratio = plate.AbsorptionRatios.GetValueOrDefault(type, 0f);
            var absorbed = FixedPoint2.Zero;
            var remainderAmount = amount;

            if (ratio > 0f)
            {
                absorbed = amount * ratio;
                remainderAmount = amount - absorbed;
            }
            else if (ratio < 0f)
            {
                remainderAmount = amount * (1f + Math.Abs(ratio));
            }

            var plateDamage = amount * Math.Abs(ratio) * multiplier;
            absorbedTotal += absorbed;
            plateDamageTotal += plateDamage;

            if (remainderAmount > FixedPoint2.Zero)
                remainder.DamageDict.Add(type, remainderAmount);
        }
    }

    private void OnPlateVerbExamine(Entity<ArmorPlateItemComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var examineMarkup = GetPlateExamine(ent.Comp);
        var armorExamine = new ArmorExamineEvent(examineMarkup);
        RaiseLocalEvent(ent, ref armorExamine);

        _examine.AddDetailedExamineVerb(args,
            ent.Comp,
            examineMarkup,
            Loc.GetString("armor-plate-examinable-verb-text"),
            "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-plate-examinable-verb-message"));
    }

    private static int CalcDirection(float ratio)
    {
        return ratio < 0 ? 1 : ratio > 0 ? -1 : 0;
    }

    private void AddSpeedDisplay(FormattedMessage message, string gaitType, float speedChange)
    {
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("armor-plate-speed-display",
            ("gait", gaitType),
            ("deltasign", CalcDirection(speedChange)),
            ("speedPercent", Math.Abs(speedChange))));
    }

    private FormattedMessage GetPlateExamine(ArmorPlateItemComponent plate)
    {
        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("armor-plate-attributes-examine"));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("armor-plate-initial-durability",
            ("durability", plate.MaxDurability)));

        var walkModifier = MathF.Round((plate.WalkSpeedModifier - 1.0f) * 100f, 1);
        var sprintModifier = MathF.Round((plate.SprintSpeedModifier - 1.0f) * 100f, 1);

        if (walkModifier != 0.0f || sprintModifier != 0.0f)
        {
            if (MathHelper.CloseTo(walkModifier, sprintModifier, 0.5f))
            {
                AddSpeedDisplay(message, Loc.GetString("armor-plate-gait-speed"), walkModifier);
            }
            else
            {
                AddSpeedDisplay(message, Loc.GetString("armor-plate-gait-sprint"), sprintModifier);
                AddSpeedDisplay(message, Loc.GetString("armor-plate-gait-walk"), walkModifier);
            }
        }

        foreach (var (damageType, ratio) in plate.AbsorptionRatios)
        {
            message.PushNewline();

            var localizedDamageType = Loc.GetString("armor-damage-type-" + damageType.ToLower());
            var ratioPercent = MathF.Round(ratio * 100, 1);
            var multiplier = plate.DamageMultipliers.GetValueOrDefault(damageType, 1.0f);

            message.AddMarkupOrThrow(Loc.GetString("armor-plate-ratios-display",
                ("deltasign", CalcDirection(ratio)),
                ("dmgType", localizedDamageType),
                ("ratioPercent", Math.Abs(ratioPercent)),
                ("multiplier", multiplier.ToString("0.##"))));
        }

        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("armor-plate-stamina-value",
            ("multiplier", MathF.Round(plate.StaminaDamageMultiplier * 100f, 1))));

        return message;
    }

    private void OnPlateDestroyed(Entity<ArmorPlateItemComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return;

        if (!TryComp<ArmorPlateHolderComponent>(container.Owner, out var holder) || holder.ActivePlate != ent.Owner)
            return;

        if (!holder.ShowBreakPopup || !_inventory.TryGetContainingEntity(container.Owner, out var wearer))
            return;

        _popup.PopupEntity(
            Loc.GetString("armor-plate-break", ("plateName", MetaData(ent).EntityName)),
            wearer.Value,
            wearer.Value,
            PopupType.MediumCaution);
    }

    private void OnEquippedArmor(Entity<ArmorPlateHolderComponent> armor, ref GotEquippedEvent args)
    {
        if (TryGetActivePlate((armor.Owner, armor.Comp), out _))
            EnsureComp<ArmorPlateProtectedComponent>(args.Equipee);
    }

    private void OnUnequippedArmor(Entity<ArmorPlateHolderComponent> armor, ref GotUnequippedEvent args)
    {
        if (TryGetActivePlate((armor.Owner, armor.Comp), out _))
            RemComp<ArmorPlateProtectedComponent>(args.Equipee);
    }

    private void RefreshPlateProtection(EntityUid armorUid)
    {
        if (!_inventory.TryGetContainingEntity(armorUid, out var wearer))
            return;

        if (!TryComp<ArmorPlateHolderComponent>(armorUid, out var holder))
            return;

        if (TryGetActivePlate((armorUid, holder), out _))
            EnsureComp<ArmorPlateProtectedComponent>(wearer.Value);
        else
            RemComp<ArmorPlateProtectedComponent>(wearer.Value);
    }

    private void OnPlateExamined(Entity<ArmorPlateItemComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var durabilityPercent = GetDurabilityPercent(ent.Comp, damageable);
        args.PushMarkup(Loc.GetString("armor-plate-item-durability",
            ("percent", (int) durabilityPercent),
            ("durabilityColor", GetDurabilityColor(durabilityPercent))));
    }

    private static float GetDurabilityPercent(ArmorPlateItemComponent plate, DamageableComponent damageable)
    {
        var durabilityPercent = (plate.MaxDurability - damageable.TotalDamage.Int()) / (float) plate.MaxDurability * 100f;
        return Math.Clamp(durabilityPercent, 0f, 100f);
    }

    private static string GetDurabilityColor(float durabilityPercent)
    {
        return durabilityPercent switch
        {
            > 66f => "green",
            >= 33f => "yellow",
            _ => "red",
        };
    }
}
