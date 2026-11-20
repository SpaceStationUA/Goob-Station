using Content.Goobstation.Shared.ModSuits;
using Content.Pirate.Shared.ModularSuit;
using Content.Server.VoiceMask;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Chat.RadioIconsEvents;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.VoiceMask;
using Robust.Shared.Containers;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitVoiceMaskSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularSuitComponent, InventoryRelayedEvent<TransformSpeakerNameEvent>>(RelayToVoiceMasks);
        SubscribeLocalEvent<ModularSuitComponent, InventoryRelayedEvent<TransformSpeechEvent>>(RelayToVoiceMasks);
        SubscribeLocalEvent<ModularSuitComponent, InventoryRelayedEvent<TransformSpeakerJobIconEvent>>(RelayToVoiceMasks);

        SubscribeLocalEvent<VoiceMaskComponent, ModularSuitInstalledEvent>(OnInstalled);
        SubscribeLocalEvent<VoiceMaskComponent, ModularSuitRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<VoiceMaskComponent, ModSuitWearerChangedEvent>(OnWearerChanged);
        SubscribeLocalEvent<VoiceMaskComponent, AccessibleOverrideEvent>(OnVoiceMaskAccess);
        SubscribeLocalEvent<VoiceMaskComponent, BoundUserInterfaceMessageAttempt>(OnVoiceMaskUiAttempt);
    }

    private void RelayToVoiceMasks<T>(Entity<ModularSuitComponent> ent, ref InventoryRelayedEvent<T> args)
    {
        if (!_container.TryGetContainer(ent, SharedModularSuitSystem.ModuleContainer, out var modules))
            return;

        foreach (var module in modules.ContainedEntities)
        {
            if (!HasComp<VoiceMaskComponent>(module)
                || !TryComp<ModularSuitModuleComponent>(module, out var moduleComp)
                || !moduleComp.IsActive)
                continue;

            RaiseLocalEvent(module, args);
        }
    }

    private void OnInstalled(Entity<VoiceMaskComponent> ent, ref ModularSuitInstalledEvent args)
    {
        if (TryComp<ModularSuitComponent>(args.Suit, out var suit) && suit.Wearer is { } wearer)
            _actions.AddAction(wearer, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    private void OnRemoved(Entity<VoiceMaskComponent> ent, ref ModularSuitRemovedEvent args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
        _ui.CloseUi(ent.Owner, VoiceMaskUIKey.Key);
    }

    private void OnWearerChanged(Entity<VoiceMaskComponent> ent, ref ModSuitWearerChangedEvent args)
    {
        if (args.OldWearer != null)
        {
            _actions.RemoveAction(ent.Comp.ActionEntity);
            _ui.CloseUi(ent.Owner, VoiceMaskUIKey.Key, args.OldWearer);
        }

        if (args.NewWearer is { } wearer)
            _actions.AddAction(wearer, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    private void OnVoiceMaskAccess(Entity<VoiceMaskComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.Target != ent.Owner
            || !HasComp<ModularSuitModuleComponent>(ent) || GetInstalledWearer(ent) != args.User)
            return;

        args.Handled = true;
        args.Accessible = true;
    }

    private void OnVoiceMaskUiAttempt(Entity<VoiceMaskComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.UiKey is VoiceMaskUIKey && HasComp<ModularSuitModuleComponent>(ent)
            && GetInstalledWearer(ent) != args.Actor)
            args.Cancel();
    }

    private EntityUid? GetInstalledWearer(EntityUid module)
    {
        if (_container.TryGetContainingContainer(module, out var container)
            && container.ID == SharedModularSuitSystem.ModuleContainer
            && TryComp<ModularSuitComponent>(container.Owner, out var suit))
            return suit.Wearer;

        return null;
    }
}
