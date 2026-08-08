// SPDX-FileCopyrightText: 2025 Coenx-flex
// SPDX-FileCopyrightText: 2025 Cojoke
// SPDX-FileCopyrightText: 2025 Ilya246
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Interaction.Events;
using Content.Shared.MedicalScanner;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._Pirate.CorticalBorer;

public abstract partial class SharedCorticalBorerSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly SharedUserInterfaceSystem Ui = default!;
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CorticalBorerComponent, AttackAttemptEvent>(OnBorerAttackAttempt);
        SubscribeLocalEvent<SurgeryCorticalBorerConditionComponent, SurgeryValidEvent>(OnCorticalBorerValid);
        SubscribeLocalEvent<SurgeryStepRemoveCorticalBorerComponent, SurgeryStepEvent>(OnCorticalBorerRemovalStep);
        SubscribeLocalEvent<SurgeryStepRemoveCorticalBorerComponent, SurgeryStepCompleteCheckEvent>(OnCorticalBorerRemovalCheck);
    }

    public bool CanUseAbility(Entity<CorticalBorerComponent> ent, EntityUid target)
    {
        if (!_statusEffects.HasStatusEffect(target, "CorticalBorerProtection"))
            return true;

        Popup.PopupEntity(Loc.GetString("cortical-borer-sugar-block"), ent.Owner, ent.Owner, PopupType.Medium);
        return false;
    }

    private void OnBorerAttackAttempt(Entity<CorticalBorerComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.Host is not { } host || args.Target != host)
            return;

        if (!CanUseAbility(ent, host))
            args.Cancel();
    }

    public void InfestTarget(Entity<CorticalBorerComponent> ent, EntityUid target)
    {
        var (uid, comp) = ent;
        var infested = EnsureComp<CorticalBorerInfestedComponent>(target);

        // Pirate: infestations can begin after map initialization, so initialize both containers here too.
        infested.InfestationContainer ??= Container.EnsureContainer<Container>(target, "InfestationContainer");
        infested.ControlContainer ??= Container.EnsureContainer<Container>(target, "ControlContainer");

        if (!Container.Insert(uid, infested.InfestationContainer))
        {
            RemCompDeferred<CorticalBorerInfestedComponent>(target);
            return;
        }

        infested.Borer = ent;
        comp.Host = target;

        if (comp.AddOnInfest is not null)
        {
            foreach (var component in comp.AddOnInfest.Values)
            {
                var componentType = component.Component.GetType();
                if (HasComp(uid, componentType))
                    continue;

                var copy = (Component) _serialization.CreateCopy(component.Component, notNullableOverride: true);
                EntityManager.AddComponent(uid, copy, true);
            }
        }

        if (comp.RemoveOnInfest is not null)
        {
            foreach (var component in comp.RemoveOnInfest.Values)
                RemCompDeferred(uid, component.Component.GetType());
        }

        if (TryComp<DamageableComponent>(uid, out var damageable))
            _damage.SetAllDamage(uid, damageable, 0);
    }

    public bool TryEjectBorer(Entity<CorticalBorerComponent> ent)
    {
        var (uid, comp) = ent;

        if (comp.Host is not { } host)
            return false;

        var ejecting = new CorticalBorerEjectingEvent();
        RaiseLocalEvent(uid, ref ejecting);

        if (comp.ControlingHost || !Container.TryRemoveFromContainer(uid))
            return false;

        var ejected = new CorticalBorerEjectedEvent(host);
        RaiseLocalEvent(uid, ref ejected);

        if (TryComp<UserInterfaceComponent>(uid, out var userInterface))
        {
            Ui.CloseUi((uid, userInterface), HealthAnalyzerUiKey.Key);
            Ui.CloseUi((uid, userInterface), CorticalBorerDispenserUiKey.Key);
        }

        RemCompDeferred<CorticalBorerInfestedComponent>(host);
        comp.Host = null;

        if (comp.RemoveOnInfest is not null)
        {
            foreach (var component in comp.RemoveOnInfest.Values)
            {
                var componentType = component.Component.GetType();
                if (HasComp(uid, componentType))
                    continue;

                var copy = (Component) _serialization.CreateCopy(component.Component, notNullableOverride: true);
                EntityManager.AddComponent(uid, copy, true);
            }
        }

        if (comp.AddOnInfest is not null)
        {
            foreach (var component in comp.AddOnInfest.Values)
                RemCompDeferred(uid, component.Component.GetType());
        }

        return true;
    }

    public void LayEgg(Entity<CorticalBorerComponent> ent)
    {
        if (ent.Comp.Host is not { } host)
            return;

        var coordinates = _transform.ToMapCoordinates(host.ToCoordinates());
        Spawn(ent.Comp.EggProto, coordinates);
    }

    private void OnCorticalBorerValid(Entity<SurgeryCorticalBorerConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!HasComp<CorticalBorerInfestedComponent>(args.Body) ||
            !HasComp<IncisionOpenComponent>(args.Part))
        {
            args.Cancelled = true;
        }
    }

    private void OnCorticalBorerRemovalStep(Entity<SurgeryStepRemoveCorticalBorerComponent> ent, ref SurgeryStepEvent args)
    {
        if (TryComp<CorticalBorerInfestedComponent>(args.Body, out var infested) &&
            infested.InfestationContainer.ContainedEntities.Count != 0)
        {
            TryEjectBorer(infested.Borer);
        }
    }

    private void OnCorticalBorerRemovalCheck(Entity<SurgeryStepRemoveCorticalBorerComponent> ent,
        ref SurgeryStepCompleteCheckEvent args)
    {
        if (HasComp<CorticalBorerInfestedComponent>(args.Body))
            args.Cancelled = true;
    }
}

public sealed class InfestHostAttempt : CancellableEntityEventArgs
{
    public EntityUid? Blocker;
}

[ByRefEvent]
public record struct CorticalBorerEjectingEvent;

[ByRefEvent]
public readonly record struct CorticalBorerEjectedEvent(EntityUid Host);

[Serializable, NetSerializable]
public enum CorticalBorerDispenserUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CorticalBorerDispenserSetInjectAmountMessage(int amount) : BoundUserInterfaceMessage
{
    public readonly int CorticalBorerDispenserDispenseAmount = amount;
}

[Serializable, NetSerializable]
public sealed class CorticalBorerDispenserInjectMessage(string prototype) : BoundUserInterfaceMessage
{
    public readonly string ChemProtoId = prototype;
}

[Serializable, NetSerializable]
public sealed class CorticalBorerDispenserBoundUserInterfaceState(
    List<CorticalBorerDispenserItem> dispenserList,
    int dispenseAmount) : BoundUserInterfaceState
{
    public readonly List<CorticalBorerDispenserItem> DisList = dispenserList;
    public readonly int SelectedDispenseAmount = dispenseAmount;
}

[Serializable, NetSerializable]
public sealed class CorticalBorerDispenserItem(
    string reagentName,
    string reagentId,
    int cost,
    int amount,
    int chemicals,
    Color reagentColor)
{
    public readonly string ReagentName = reagentName;
    public readonly string ReagentId = reagentId;
    public readonly int Cost = cost;
    public readonly int Amount = amount;
    public readonly int Chems = chemicals;
    public readonly Color ReagentColor = reagentColor;
}
