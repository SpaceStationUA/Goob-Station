using Content.Goobstation.Shared.ModSuits;
using Content.Pirate.Shared.ModularSuit;
using Content.Server.VoiceMask;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Chat.RadioIconsEvents;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitVoiceMaskSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularSuitComponent, InventoryRelayedEvent<TransformSpeakerNameEvent>>(RelayToVoiceMasks);
        SubscribeLocalEvent<ModularSuitComponent, InventoryRelayedEvent<TransformSpeechEvent>>(RelayToVoiceMasks);
        SubscribeLocalEvent<ModularSuitComponent, InventoryRelayedEvent<TransformSpeakerJobIconEvent>>(RelayToVoiceMasks);

        SubscribeLocalEvent<VoiceMaskComponent, ModularSuitInstalledEvent>(OnInstalled);
        SubscribeLocalEvent<VoiceMaskComponent, ModularSuitRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<VoiceMaskComponent, ModSuitWearerChangedEvent>(OnWearerChanged);
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
    }

    private void OnWearerChanged(Entity<VoiceMaskComponent> ent, ref ModSuitWearerChangedEvent args)
    {
        if (args.OldWearer != null)
            _actions.RemoveAction(ent.Comp.ActionEntity);

        if (args.NewWearer is { } wearer)
            _actions.AddAction(wearer, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }
}
