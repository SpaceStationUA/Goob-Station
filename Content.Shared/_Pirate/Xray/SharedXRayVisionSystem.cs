// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from space-wizards/space-station-14#44601 ("Mesons (XRayVision)").
// Upstream declares its handlers with [SubscribeLocalEvent]; our engine (270.1.0) predates attribute-based
// subscriptions, so they are wired up explicitly in Initialize instead.

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared._Pirate.Clothing.MesonGoggles;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Pirate.Xray;

/// <summary>
/// Shows/hides the x-ray overlay based on whether the observed entity has a
/// <see cref="XRayVisionComponent"/> equipped.
/// </summary>
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

        // Pirate: guarded - clothing gets equipped onto action-less dummies (the lobby character preview),
        // and AddAction errors out on those. Same guard the T-ray port uses.
        if (ent.Comp.Action is { } action && HasComp<ActionsComponent>(ent))
            _actions.AddAction(ent, ref ent.Comp.ActionEntity, action);
    }

    private void OnRemove(Entity<XRayVisionComponent> ent, ref ComponentRemove args)
    {
        // Pirate: upstream bails outright on RelayOverlay here, which leaves a worn-and-then-deleted item's
        // effects up (admin-deleting equipped goggles, gibbing). Harmless upstream - just a stale overlay - but
        // our client also holds the lighting buffer off while active, and a permanently unlit client is nasty.
        // So refresh the wearer instead of returning; the item is parented to them while equipped.
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

        // Pirate: see the guard note in OnStartup. Unequipping needs no counterpart - SharedActionsSystem's
        // RemoveProvidedActions already revokes item-provided actions, and keeping ActionEntity set lets
        // re-equipping reuse the same action entity.
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

    // Pirate: upstream reads the toggled item out of the action's Container field. That field is
    // [Access]-restricted to the actions systems here, but SharedActionsSystem already raises item-action
    // events directed at the container itself, so subscribing on the component gives us the same entity.
    private void OnToggleXRayVision(Entity<XRayVisionComponent> ent, ref ToggleXRayVisionEvent args)
    {
        if (args.Handled)
            return;

        SetEnabled((ent.Owner, ent.Comp), !ent.Comp.Enabled, args.Performer);
        args.Handled = true;
    }

    /// <summary>
    /// Enables or disables the component.
    /// </summary>
    /// <param name="ent">The x-ray to toggle.</param>
    /// <param name="enabled">Whether to enable or disable.</param>
    /// <param name="viewer">Viewer of the x-ray, used to refresh their overlay. If null, assumes the x-ray entity is the viewer.</param>
    public void SetEnabled(Entity<XRayVisionComponent?> ent, bool enabled, EntityUid? viewer = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);

        // Pirate: reuses the T-ray goggles' full-screen tint/scanline shader for the x-ray's own toggle, kept
        // in lockstep the same way SharedTrayScannerSystem does it for TrayScannerComponent.
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
