// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from space-wizards/space-station-14#44601 ("Mesons (XRayVision)").
// This engine uses explicit event subscriptions.

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared._Pirate.Clothing.MesonGoggles;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Pirate.Xray;

public abstract class SharedXRayVisionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XRayVisionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<XRayVisionComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<XRayVisionComponent, GotEquippedEvent>(OnCompEquip);
        SubscribeLocalEvent<XRayVisionComponent, GotUnequippedEvent>(OnCompUnequip);
        SubscribeLocalEvent<XRayVisionComponent, InventoryRelayedEvent<RefreshXRayVisionEvent>>(OnRefreshEquipmentHud);
        SubscribeLocalEvent<XRayVisionComponent, RefreshXRayVisionEvent>(OnRefreshComponentHud);
        SubscribeLocalEvent<XRayVisionComponent, ToggleXRayVisionEvent>(OnToggleXRayVision);
    }

    private void OnStartup(Entity<XRayVisionComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(ent);

        // Pirate: meson vision - skip actionless preview entities.
        if (ent.Comp.Action is { } action && HasComp<ActionsComponent>(ent))
            _actions.AddAction(ent, ref ent.Comp.ActionEntity, action);
    }

    private void OnRemove(Entity<XRayVisionComponent> ent, ref ComponentRemove args)
    {
        // Pirate: meson vision - refresh the wearer when a worn item is removed.
        if (ent.Comp.RelayOverlay)
        {
            RefreshOverlay(Transform(ent.Owner).ParentUid);
            return;
        }

        RefreshOverlay(ent);
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnCompEquip(Entity<XRayVisionComponent> ent, ref GotEquippedEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(args.Equipee);

        // Pirate: meson vision - retain the action entity for re-equipping.
        if (ent.Comp.Action is { } action && HasComp<ActionsComponent>(args.Equipee))
            _actions.AddAction(args.Equipee, ref ent.Comp.ActionEntity, action, ent);
    }

    private void OnCompUnequip(Entity<XRayVisionComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(args.Equipee);
    }

    private void OnRefreshEquipmentHud(Entity<XRayVisionComponent> ent, ref InventoryRelayedEvent<RefreshXRayVisionEvent> args)
    {
        OnRefreshComponentHud(ent, ref args.Args);
    }

    private void OnRefreshComponentHud(Entity<XRayVisionComponent> ent, ref RefreshXRayVisionEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        args.Entities.Add(ent);
    }

    // Pirate: meson vision - item-action events target their container.
    private void OnToggleXRayVision(Entity<XRayVisionComponent> ent, ref ToggleXRayVisionEvent args)
    {
        if (args.Handled)
            return;

        SetEnabled((ent.Owner, ent.Comp), !ent.Comp.Enabled, args.Performer);
        args.Handled = true;
    }

    public void SetEnabled(Entity<XRayVisionComponent?> ent, bool enabled, EntityUid? viewer = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);

        // Pirate: meson vision - keep the shader in sync.
        if (TryComp(ent.Owner, out GoggleShaderComponent? goggleShader))
        {
            goggleShader.Enabled = enabled;
            Dirty(ent.Owner, goggleShader);

            var ev = new GoggleShaderToggledEvent(enabled);
            RaiseLocalEvent(ent.Owner, ref ev);
        }

        RefreshOverlay(viewer ?? ent);
    }

    protected virtual void RefreshOverlay(EntityUid entity) { }
}

[ByRefEvent]
public record struct RefreshXRayVisionEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
    public List<Entity<XRayVisionComponent>> Entities = new();
}
