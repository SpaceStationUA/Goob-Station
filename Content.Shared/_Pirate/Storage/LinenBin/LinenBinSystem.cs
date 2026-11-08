// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration.Logs;
using Content.Shared.Atmos;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Shared._Pirate.Storage.LinenBin;
public sealed class LinenBinSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly BinSystem _bin = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LinenBinComponent, ComponentStartup>(OnStartup);
        // Run before the bin, so that the hidden item falls out.
        SubscribeLocalEvent<LinenBinComponent, InteractHandEvent>(OnInteractHand, before: new[] { typeof(BinSystem) });
        // Absorbent towels consume AfterInteract; anchoring must run first.
        SubscribeLocalEvent<LinenBinComponent, InteractUsingEvent>(OnInteractUsing, after: new[] { typeof(AnchorableSystem) });
        SubscribeLocalEvent<LinenBinComponent, TileFireEvent>(OnTileFire);
    }

    private void OnStartup(Entity<LinenBinComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.HiddenSlot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.HiddenContainerId);
    }

    private void OnInteractHand(Entity<LinenBinComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !TryComp<BinComponent>(ent, out var bin))
            return;

        args.Handled = true;
        var user = args.User;

        if (bin.Items.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("linen-bin-empty", ("bin", ent)), ent, user);
            return;
        }

        var linen = bin.Items[^1];
        if (!_bin.TryRemoveFromBin(ent, linen, bin))
            return;

        _hands.TryPickupAnyHand(user, linen);
        _adminLog.Add(LogType.Pickup, LogImpact.Low,
            $"{ToPrettyString(user):player} took {ToPrettyString(linen)} from {ToPrettyString(ent)}.");

        DropHidden(ent, user, linen);
    }

    private void OnInteractUsing(Entity<LinenBinComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<BinComponent>(ent, out var bin))
            return;

        var user = args.User;
        var used = args.Used;

        if (_tool.HasQuality(used, ent.Comp.AnchorQuality))
            return;

        if (_tool.HasQuality(used, ent.Comp.DeconstructQuality))
        {
            // An empty bin goes to the construction graph.
            if (bin.Items.Count == 0 && ent.Comp.HiddenSlot.ContainedEntity == null)
                return;

            args.Handled = true;
            _popup.PopupClient(Loc.GetString("linen-bin-not-empty", ("bin", ent)), ent, user);
            return;
        }

        if (_whitelist.IsWhitelistPass(bin.Whitelist, used))
        {
            args.Handled = true;

            if (!_hands.CanDrop(user, used))
            {
                _popup.PopupClient(Loc.GetString("linen-bin-stuck", ("item", used)), ent, user);
                return;
            }

            if (!_bin.TryInsertIntoBin(ent, used, bin))
            {
                _popup.PopupClient(Loc.GetString("linen-bin-full", ("bin", ent)), ent, user);
                return;
            }

            _adminLog.Add(LogType.Pickup, LogImpact.Low,
                $"{ToPrettyString(user):player} put {ToPrettyString(used)} in {ToPrettyString(ent)}.");
            return;
        }

        if (bin.Items.Count == 0 || ent.Comp.HiddenSlot.ContainedEntity != null)
            return;

        if (!TryComp<ItemComponent>(used, out var item)
            || _item.GetItemSizeWeight(item.Size) > _item.GetItemSizeWeight(ent.Comp.MaxHiddenSize))
            return;

        args.Handled = true;

        if (!_hands.TryDropIntoContainer(user, used, ent.Comp.HiddenSlot))
        {
            _popup.PopupClient(Loc.GetString("linen-bin-stuck", ("item", used)), ent, user);
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} hid {ToPrettyString(used)} in {ToPrettyString(ent)}.");
    }
    private void OnTileFire(Entity<LinenBinComponent> ent, ref TileFireEvent args)
    {
        if (!TryComp<BinComponent>(ent, out var bin) || bin.Items.Count == 0)
            return;

        foreach (var linen in new List<EntityUid>(bin.Items))
        {
            Del(linen);
        }

        Dirty(ent, bin);

        DropHidden(ent, null, null);
    }

    private void DropHidden(Entity<LinenBinComponent> ent, EntityUid? user, EntityUid? linen)
    {
        if (ent.Comp.HiddenSlot.ContainedEntity is not { } hidden)
            return;

        if (!_container.Remove(hidden, ent.Comp.HiddenSlot))
            return;

        if (user != null && linen != null)
        {
            _popup.PopupClient(Loc.GetString("linen-bin-hidden-falls", ("item", hidden), ("linen", linen.Value)),
                ent, user.Value);
        }
    }
}
