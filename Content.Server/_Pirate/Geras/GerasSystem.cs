// SPDX-FileCopyrightText: 2024 Just-a-Unity-Dev <67359748+Just-a-Unity-Dev@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server.Actions;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Shared._Pirate.Geras;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Zombies;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Geras;

public sealed class GerasSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private const string StorageContainerId = "geras_storage";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GerasComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GerasComponent, MorphIntoGerasEvent>(OnMorphIntoGeras);
        SubscribeLocalEvent<GerasComponent, EntityZombifiedEvent>(OnZombified);
        SubscribeLocalEvent<GerasComponent, PolymorphedEvent>(OnPolymorphed);
    }

    private void OnMapInit(Entity<GerasComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<ZombieComponent>(ent.Owner))
            return;

        AddAction(ent);
    }

    private void OnZombified(Entity<GerasComponent> ent, ref EntityZombifiedEvent args)
    {
        if (ent.Comp.GerasActionEntity is { } action)
            _actions.RemoveAction(action);
    }

    private void AddAction(Entity<GerasComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.GerasActionEntity, ent.Comp.GerasAction);
    }

    private void OnMorphIntoGeras(Entity<GerasComponent> ent, ref MorphIntoGerasEvent args)
    {
        if (args.Handled || HasComp<ZombieComponent>(ent.Owner))
            return;

        var color = TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid)
            ? humanoid.SkinColor
            : Color.White;

        StoreWornItems(ent);

        if (_polymorph.PolymorphEntity(ent.Owner, ent.Comp.GerasPolymorphId) is not { } geras)
        {
            // Morph failed - hand everything straight back instead of leaving it stuck in storage.
            RestoreWornItems(ent);
            return;
        }

        _appearance.SetData(geras, GerasVisuals.Color, color);

        _popup.PopupEntity(
            Loc.GetString("geras-popup-morph-message-others", ("entity", geras)),
            geras,
            Filter.PvsExcept(geras),
            true);
        _popup.PopupEntity(Loc.GetString("geras-popup-morph-message-user"), geras, geras);

        args.Handled = true;
    }

    private void OnPolymorphed(Entity<GerasComponent> ent, ref PolymorphedEvent args)
    {
        // Only care about the geras reverting back into its owner - not the initial morph into geras.
        if (!args.IsRevert || args.NewEntity != ent.Owner)
            return;

        // If the geras died before reverting, its equipment spills out instead of re-equipping.
        if (_mobState.IsDead(ent.Owner))
            DropWornItems(ent);
        else
            RestoreWornItems(ent);
    }

    private void StoreWornItems(Entity<GerasComponent> ent)
    {
        var storage = _container.EnsureContainer<Container>(ent.Owner, StorageContainerId);

        if (TryComp<InventoryComponent>(ent.Owner, out var inventory))
        {
            var enumerator = new InventorySystem.InventorySlotEnumerator(inventory);
            while (enumerator.NextItem(out var item, out var slot))
            {
                ent.Comp.StoredClothing[slot.Name] = item;
                _container.Insert(item, storage, force: true);
            }
        }

        foreach (var held in _hands.EnumerateHeld(ent.Owner).ToList())
        {
            ent.Comp.StoredHeldItems.Add(held);
            _container.Insert(held, storage, force: true);
        }
    }

    private void RestoreWornItems(Entity<GerasComponent> ent)
    {
        foreach (var (slot, item) in ent.Comp.StoredClothing)
        {
            if (!Deleted(item))
                _inventory.TryEquip(ent.Owner, item, slot, force: true);
        }

        foreach (var item in ent.Comp.StoredHeldItems)
        {
            if (!Deleted(item))
                _hands.TryPickupAnyHand(ent.Owner, item, checkActionBlocker: false);
        }

        ent.Comp.StoredClothing.Clear();
        ent.Comp.StoredHeldItems.Clear();
    }

    private void DropWornItems(Entity<GerasComponent> ent)
    {
        if (_container.TryGetContainer(ent.Owner, StorageContainerId, out var storage))
            _container.EmptyContainer(storage, force: true, destination: Transform(ent.Owner).Coordinates);

        ent.Comp.StoredClothing.Clear();
        ent.Comp.StoredHeldItems.Clear();
    }
}
