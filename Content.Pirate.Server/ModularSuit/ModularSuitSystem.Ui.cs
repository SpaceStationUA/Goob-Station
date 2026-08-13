using System.Linq;
using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ModularSuitUiStateSystem _uiState = default!;

    private void InitializeUi()
    {
        SubscribeLocalEvent<ModularSuitComponent, BoundUIOpenedEvent>(OnUIOpened);

        SubscribeLocalEvent<ModularSuitComponent, ToggleSuitActiveMessage>(OnToggleActiveMessage);
        SubscribeLocalEvent<ModularSuitComponent, ToggleModuleMessage>(OnToggleModuleMessage);
        SubscribeLocalEvent<ModularSuitComponent, ModularSuitChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<ModularSuitComponent, ModularSuitActiveChangedEvent>(OnActiveChanged);
    }

    private void OnUIOpened(Entity<ModularSuitComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUiState(ent);
    }

    private void OnToggleActiveMessage(Entity<ModularSuitComponent> ent, ref ToggleSuitActiveMessage args)
    {
        RequestSetActive(ent, args.Actor, args.Active);
    }

    protected override void ToggleActive(Entity<ModularSuitComponent> ent, EntityUid user)
    {
        RequestSetActive(ent, user, !ent.Comp.Active);
    }

    /// <summary>
    ///     Seals every deployed part, or unseals them again. Unlike the activate action this does not
    ///     power the suit up by itself - that is left to the suit's auto-activate setting.
    /// </summary>
    private void ToggleSeal(Entity<ModularSuitComponent> ent, EntityUid wearer, EntityUid? actor = null)
    {
        var recipient = actor ?? wearer;

        if (!ent.Comp.Deployed)
        {
            Refuse(ent, recipient, "modsuit-seal-blocked-undeployed");
            return;
        }

        // Unsealing powers the suit down.
        if (ent.Comp.Assembled)
        {
            if (!TryStartSuitUnsealing(ent, wearer))
                Refuse(ent, recipient, "modsuit-seal-failed");

            UpdateUiState(ent);
            return;
        }

        if (!TryStartSuitSealing(ent, wearer, activateSuit: false))
            Refuse(ent, recipient, "modsuit-seal-failed");

        UpdateUiState(ent);
    }

    private void RequestSetActive(Entity<ModularSuitComponent> ent, EntityUid user, bool active)
    {
        if (ent.Comp.Wearer != user)
            return;

        if (active && !ent.Comp.Assembled && TryStartSuitSealing(ent, user))
        {
            UpdateUiState(ent);
            return;
        }

        if (!active && ent.Comp.Active && TryStartSuitUnsealing(ent, user))
        {
            UpdateUiState(ent);
            return;
        }

        SetActive(ent, active);
        UpdateUiState(ent);
    }

    private void OnToggleModuleMessage(Entity<ModularSuitComponent> ent, ref ToggleModuleMessage args)
    {
        if (!ent.Comp.Active)
        {
            Popup.PopupPredicted(Loc.GetString("modsuit-not-active"), ent, null);
            UpdateUiState(ent);
            return;
        }

        var moduleContainer = Container.GetContainer(ent, ModuleContainer);
        if (!TryGetEntity(args.ModuleUid, out var moduleEnt)
            || !moduleContainer.ContainedEntities.Contains(moduleEnt.Value)
            || !TryComp<ModularSuitModuleComponent>(moduleEnt.Value, out var module))
        {
            UpdateUiState(ent);
            return;
        }

        if (!module.CanBeDisabled)
        {
            UpdateUiState(ent);
            return;
        }

        if (args.Active)
        {
            var attemptEvent = new ModularSuitModuleAttemptEvent(ent.Owner);
            RaiseLocalEvent(moduleEnt.Value, ref attemptEvent);

            if (attemptEvent.Cancelled)
            {
                UpdateUiState(ent);
                return;
            }
        }

        module.IsActive = args.Active;
        Dirty(moduleEnt.Value, module);

        var ev = new ModularSuitModuleToggledEvent(ent, ent.Comp.Wearer, args.Active);
        RaiseLocalEvent(moduleEnt.Value, ref ev);

        UpdateUiState(ent);
    }

    private void OnChargeChanged(Entity<ModularSuitComponent> ent, ref ModularSuitChargeChangedEvent args)
    {
        UpdateUiState(ent);
    }

    private void OnActiveChanged(Entity<ModularSuitComponent> ent, ref ModularSuitActiveChangedEvent args)
    {
        UpdateUiState(ent);
    }

    private void UpdateUiState(Entity<ModularSuitComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, ModularSuitUiKey.Key))
            return;

        _ui.SetUiState(ent.Owner, ModularSuitUiKey.Key, _uiState.BuildUiState(ent));
    }
}
