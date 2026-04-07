// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aineias1 <dmitri.s.kiselev@gmail.com>
// SPDX-FileCopyrightText: 2025 FaDeOkno <143940725+FaDeOkno@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 McBosserson <148172569+McBosserson@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Milon <plmilonpl@gmail.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ted Lukin <66275205+pheenty@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 TheBorzoiMustConsume <197824988+TheBorzoiMustConsume@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Unlumination <144041835+Unlumy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 coderabbitai[bot] <136622811+coderabbitai[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
// SPDX-FileCopyrightText: 2025 username <113782077+whateverusername0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 whateverusername0 <whateveremail>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Pressure;
using Content.Server._Lavaland.Weapons.Ranged.Upgrades.Components;
using Content.Server.EntityEffects;
using Content.Shared._Lavaland.Weapons.Ranged.Events;
using Content.Shared._Lavaland.Weapons.Ranged.Upgrades;
using Content.Shared._Lavaland.Weapons.Ranged.Upgrades.Components;
using Content.Shared.EntityEffects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
#region DOWNSTREAM-TPirates: gun flashlights
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Light.Components;
using Content.Shared._Pirate.Weapons.Ranged.Upgrades;
using Content.Shared.Toggleable;
using Robust.Shared.GameObjects;
#endregion

namespace Content.Server._Lavaland.Weapons.Ranged.Upgrades;

public sealed class GunUpgradeSystem : SharedGunUpgradeSystem
{
    [Dependency] private readonly PressureEfficiencyChangeSystem _pressure = default!;
    [Dependency] private readonly SharedEntityEffectSystem _entityEffect = default!;
    
    #region DOWNSTREAM-TPirates: gun flashlights
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    #endregion

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunUpgradeDamageComponent, GunShotEvent>(OnDamageGunShot);
        SubscribeLocalEvent<GunUpgradeDamageComponent, ProjectileShotEvent>(OnProjectileShot);
        SubscribeLocalEvent<GunUpgradePressureComponent, EntGotInsertedIntoContainerMessage>(OnPressureUpgradeInserted);
        SubscribeLocalEvent<GunUpgradePressureComponent, EntGotRemovedFromContainerMessage>(OnPressureUpgradeRemoved);
        #region DOWNSTREAM-TPirates: gun flashlights
        SubscribeLocalEvent<GunUpgradeFlashlightComponent, EntGotInsertedIntoContainerMessage>(OnFlashlightInserted);
        SubscribeLocalEvent<GunUpgradeFlashlightComponent, EntGotRemovedFromContainerMessage>(OnFlashlightRemoved);
        SubscribeLocalEvent<GunUpgradeFlashlightComponent, ToggleActionEvent>(OnFlashlightToggled,
            after: new[] { typeof(Content.Server.Light.EntitySystems.HandheldLightSystem) });
        #endregion

        SubscribeLocalEvent<WeaponUpgradeEffectsComponent, MeleeHitEvent>(OnEffectsUpgradeHit);
    }

    private void OnDamageGunShot(Entity<GunUpgradeDamageComponent> ent, ref GunShotEvent args)
    {
        foreach (var (ammo, _) in args.Ammo)
        {
            if (!TryComp<ProjectileComponent>(ammo, out var projectile))
                continue;

            var multiplier = 1f;

            if (TryComp<PressureDamageChangeComponent>(Transform(ent).ParentUid, out var pressure)
                && _pressure.ApplyModifier((Transform(ent).ParentUid, pressure))
                && pressure.ApplyToProjectiles)
                multiplier = pressure.AppliedModifier;

            if (ent.Comp.BonusDamage != null)
                projectile.Damage += ent.Comp.BonusDamage * multiplier;
            projectile.Damage *= ent.Comp.Modifier;
        }
    }

    private void OnProjectileShot(Entity<GunUpgradeDamageComponent> ent, ref ProjectileShotEvent args)
    {
        if (!TryComp<ProjectileComponent>(args.FiredProjectile, out var projectile))
            return;

        var multiplier = 1f;

        if (TryComp<PressureDamageChangeComponent>(Transform(ent).ParentUid, out var pressure)
            && _pressure.ApplyModifier((Transform(ent).ParentUid, pressure))
            && pressure.ApplyToProjectiles)
            multiplier = pressure.AppliedModifier;

        if (ent.Comp.BonusDamage != null)
            projectile.Damage += ent.Comp.BonusDamage * multiplier;
        projectile.Damage *= ent.Comp.Modifier;
    }

    private void OnPressureUpgradeInserted(Entity<GunUpgradePressureComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var comp = ent.Comp;
        if (!TryComp<PressureDamageChangeComponent>(args.Container.Owner, out var pdc))
            return;

        comp.SavedAppliedModifier = pdc.AppliedModifier;
        comp.SavedApplyWhenInRange = pdc.ApplyWhenInRange;
        comp.SavedLowerBound = pdc.LowerBound;
        comp.SavedUpperBound = pdc.UpperBound;

        if (comp.NewAppliedModifier != null)
            pdc.AppliedModifier = comp.NewAppliedModifier.Value;
        if (comp.NewApplyWhenInRange != null)
            pdc.ApplyWhenInRange = comp.NewApplyWhenInRange.Value;
        if (comp.NewLowerBound != null)
            pdc.LowerBound = comp.NewLowerBound.Value;
        if (comp.NewUpperBound != null)
            pdc.UpperBound = comp.NewUpperBound.Value;
    }

    private void OnPressureUpgradeRemoved(Entity<GunUpgradePressureComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        var comp = ent.Comp;
        if (!TryComp<PressureDamageChangeComponent>(args.Container.Owner, out var pdc))
            return;

        pdc.AppliedModifier = comp.SavedAppliedModifier;
        pdc.ApplyWhenInRange = comp.SavedApplyWhenInRange;
        pdc.LowerBound = comp.SavedLowerBound;
        pdc.UpperBound = comp.SavedUpperBound;
    }

    private void OnEffectsUpgradeHit(Entity<WeaponUpgradeEffectsComponent> ent, ref MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            foreach (var effect in ent.Comp.Effects)
            {
                _entityEffect.Effect(effect, new EntityEffectBaseArgs(hit, EntityManager));
            }
        }
    }

    #region DOWNSTREAM-TPirates: gun flashlights
    private void OnFlashlightInserted(Entity<GunUpgradeFlashlightComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != "flashlight")
            return;

        if (!TryComp<UpgradeableWeaponComponent>(args.Container.Owner, out _))
            return;

        if (!TryComp<HandheldLightComponent>(ent, out var handheld)
            || handheld.ToggleActionEntity == null)
            return;

        if (!TryComp<ActionComponent>(handheld.ToggleActionEntity.Value, out var action))
            return;

        ent.Comp.OriginalItemIconStyle = action.ItemIconStyle;
        ent.Comp.OriginalUseDelay = action.UseDelay;
        ent.Comp.HasSavedActionDefaults = true;

        _actions.SetEntityIcon((handheld.ToggleActionEntity.Value, action), args.Container.Owner);
        _actions.SetItemIconStyle((handheld.ToggleActionEntity.Value, action), ItemActionIconStyle.BigItem);
        _actions.SetUseDelay((handheld.ToggleActionEntity.Value, action), null);
        SetGunFlashlightVisuals(args.Container.Owner, attached: true, on: handheld.Activated);
        GrantUpgradeActionsIfHeld(args.Container.Owner);
    }

    private void OnFlashlightRemoved(Entity<GunUpgradeFlashlightComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != "flashlight")
            return;

        if (TryComp<HandheldLightComponent>(ent, out var handheld)
            && handheld.ToggleActionEntity is { } toggleAction
            && TryComp<ActionComponent>(toggleAction, out var action))
        {
            if (ent.Comp.HasSavedActionDefaults)
            {
                _actions.SetItemIconStyle((toggleAction, action), ent.Comp.OriginalItemIconStyle);
                _actions.SetUseDelay((toggleAction, action), ent.Comp.OriginalUseDelay);
                ent.Comp.HasSavedActionDefaults = false;
            }

            if (action.EntIcon == args.Container.Owner)
                _actions.SetEntityIcon((toggleAction, action), null);

            if (_containers.TryGetContainingContainer((args.Container.Owner, Transform(args.Container.Owner), MetaData(args.Container.Owner)), out var gunContainer)
                && TryComp<HandsComponent>(gunContainer.Owner, out _)
                && action.AttachedEntity == gunContainer.Owner)
            {
                _actions.RemoveAction((gunContainer.Owner, CompOrNull<ActionsComponent>(gunContainer.Owner)), (toggleAction, action));
            }
        }

        SetGunFlashlightVisuals(args.Container.Owner, attached: false, on: false);
    }

    private void OnFlashlightToggled(Entity<GunUpgradeFlashlightComponent> ent, ref ToggleActionEvent args)
    {
        if (!TryComp<HandheldLightComponent>(ent, out var handheld))
            return;

        if (!TryGetContainingGun(ent.Owner, out var gun))
            return;

        SetGunFlashlightVisuals(gun, attached: true, on: handheld.Activated);
    }

    private bool TryGetContainingGun(EntityUid flashlight, out EntityUid gun)
    {
        gun = default;

        if (!_containers.TryGetContainingContainer((flashlight, Transform(flashlight), MetaData(flashlight)), out var container))
            return false;

        if (container.ID != "flashlight")
            return false;

        if (!TryComp<UpgradeableWeaponComponent>(container.Owner, out _))
            return false;

        gun = container.Owner;
        return true;
    }

    private void SetGunFlashlightVisuals(EntityUid gun, bool attached, bool on)
    {
        if (!TryComp<AppearanceComponent>(gun, out var appearance))
            return;

        _appearance.SetData(gun, GunFlashlightVisuals.Attached, attached, appearance);
        _appearance.SetData(gun, GunFlashlightVisuals.LightOn, on, appearance);
    }

    private void GrantUpgradeActionsIfHeld(EntityUid gun)
    {
        if (!_containers.TryGetContainingContainer((gun, Transform(gun), MetaData(gun)), out var container))
            return;

        var holder = container.Owner;
        if (!TryComp<HandsComponent>(holder, out _))
            return;

        var ev = new GetItemActionsEvent(_actionContainer, holder, gun);
        RaiseLocalEvent(gun, ev);
        if (ev.Actions.Count == 0)
            return;

        EnsureComp<ActionsContainerComponent>(gun);
        _actions.GrantActions((holder, CompOrNull<ActionsComponent>(holder)), ev.Actions, gun);
        _actions.LoadActions(holder);
    }
    #endregion
}
