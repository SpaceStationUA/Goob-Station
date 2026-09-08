// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.NightVision;

/// <summary>
/// Shows/hides the <see cref="MonolithNightVisionOverlay"/> based on whether the observed
/// entity has a <see cref="MonolithNightVisionComponent"/> equipped.
/// </summary>
public abstract partial class SharedMonolithNightVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MonolithNightVisionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MonolithNightVisionComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<MonolithNightVisionComponent, GotEquippedEvent>(OnCompEquip);
        SubscribeLocalEvent<MonolithNightVisionComponent, GotUnequippedEvent>(OnCompUnequip);
        SubscribeLocalEvent<MonolithNightVisionComponent, InventoryRelayedEvent<RefreshMonolithNightVisionEvent>>(OnRefreshEquipmentHud);
        SubscribeLocalEvent<MonolithNightVisionComponent, RefreshMonolithNightVisionEvent>(OnRefreshComponentHud);
        SubscribeLocalEvent<MonolithToggleNightVisionEvent>(OnToggleNightVisionEvent);
    }

    private void OnStartup(Entity<MonolithNightVisionComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(ent);
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnRemove(Entity<MonolithNightVisionComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(ent);
        // Use the detached-safe overload: the action may already be gone (e.g. deleted
        // alongside its owner), in which case RemoveAction would log an error.
        _actions.RemoveAction(ent.Comp.ActionEntity);
    }

    private void OnCompEquip(Entity<MonolithNightVisionComponent> ent, ref GotEquippedEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        RefreshOverlay(args.Equipee);
        _actions.AddAction(args.Equipee, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }
    private void OnCompUnequip(Entity<MonolithNightVisionComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        ent.Comp.Enabled = false; // mono
        Dirty(ent);
        // Use the detached-safe overload: when the wearer is being deleted the action
        // entity may already be gone, and RemoveAction would log an error (fails tests).
        _actions.RemoveAction(ent.Comp.ActionEntity);
        RefreshOverlay(args.Equipee);
    }
    protected virtual void OnRefreshEquipmentHud(Entity<MonolithNightVisionComponent> ent, ref InventoryRelayedEvent<RefreshMonolithNightVisionEvent> args)
    {
        OnRefreshComponentHud(ent, ref args.Args);
    }
    protected virtual void OnRefreshComponentHud(Entity<MonolithNightVisionComponent> ent, ref RefreshMonolithNightVisionEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        args.Entities.Add(ent);
    }

    private void OnToggleNightVisionEvent(MonolithToggleNightVisionEvent args)
    {
        var ent = args.Action.Comp.Container;

        if (!TryComp<MonolithNightVisionComponent>(ent, out var nightVisionComp))
            return;

        SetEnabled(ent.Value, !nightVisionComp.Enabled, args.Performer);
        args.Handled = true;
    }

    /// <param name="ent">The night vision to toggle.</param>
    /// <param name="enabled">Whether to enable or disable.</param>
    /// <param name="viewer">Viewer of the night vision, used to refresh their overlay. If null, assumes the night vision entity is the viewer.</param>
    public void SetEnabled(Entity<MonolithNightVisionComponent?> ent, bool enabled, EntityUid? viewer = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);

        if (_net.IsClient && _timing.IsFirstTimePredicted)
        {
            _audio.PlayEntity(enabled ? ent.Comp.ActivateSound : ent.Comp.DeactivateSound,
                Filter.Local(),
                ent,
                false);
        }

        RefreshOverlay(viewer ?? ent);
    }

    protected virtual void RefreshOverlay(EntityUid entity) { }
}

[ByRefEvent]
public record struct RefreshMonolithNightVisionEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
    public List<Entity<MonolithNightVisionComponent>> Entities = new();
}
