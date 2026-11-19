// SPDX-License-Identifier: AGPL-3.0-or-later
// Based on the accessory system of space_station_ADT (ADTAccessorySystem, AGPL-3.0),
// with per-category limits and holders in any inventory slot.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Pirate.Clothing;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Clothing.Accessories;

public sealed class ClothingAccessorySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const SlotFlags WornHolderSlots = SlotFlags.WITHOUT_POCKET & ~SlotFlags.SUITSTORAGE;
    private static readonly VerbCategory DetachCategory = new("pirate-clothing-accessory-verb-category", null);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingAccessoryHolderComponent, InteractUsingEvent>(OnHolderInteractUsing);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, GetVerbsEvent<AlternativeVerb>>(OnHolderVerbs);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ExaminedEvent>(OnHolderExamined);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, EntityTerminatingEvent>(OnHolderTerminating);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, GotEquippedEvent>(OnHolderEquipped);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, GotUnequippedEvent>(OnHolderUnequipped);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, InventoryRelayedEvent<GetVerbsEvent<InnateVerb>>>(OnRelayInnateVerbs);

        SubscribeLocalEvent<ClothingAccessoryComponent, AfterInteractEvent>(OnAccessoryAfterInteract);
        SubscribeLocalEvent<ClothingAccessoryComponent, ClothingAccessoryAttachDoAfterEvent>(OnAttachDoAfter);
        SubscribeLocalEvent<ClothingAccessoryComponent, LoadoutAccessoryCheckEvent>(OnLoadoutCheck);
        SubscribeLocalEvent<ClothingAccessoryComponent, LoadoutAccessoryAttachEvent>(OnLoadoutAttach);
        SubscribeLocalEvent<ClothingAccessoryComponent, GetClothingAccessoryWearerEvent>(OnGetWearer);
    }

    #region Interaction

    private void OnHolderInteractUsing(Entity<ClothingAccessoryHolderComponent> ent, ref InteractUsingEvent args)
    {
        // Let other interaction handlers process rejected items.
        if (args.Handled ||
            !TryComp<ClothingAccessoryComponent>(args.Used, out var accessory) ||
            !Accepts(ent, accessory.Category))
            return;

        args.Handled = true;
        TryAttach(ent, (args.Used, accessory), args.User);
    }

    private void OnAccessoryAfterInteract(Entity<ClothingAccessoryComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryFindWornHolder(target, ent.Comp, out var holder))
            return;

        args.Handled = true;

        if (target == args.User)
        {
            TryAttach(holder.Value, ent, args.User);
            return;
        }

        if (!CanAttach(holder.Value, ent, args.User))
            return;

        var doAfter = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.AttachDelay,
            new ClothingAccessoryAttachDoAfterEvent(),
            ent.Owner,
            target,
            ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var message = Loc.GetString("pirate-clothing-accessory-attach-start",
            ("user", Identity.Entity(args.User, EntityManager)),
            ("accessory", ent.Owner),
            ("target", Identity.Entity(target, EntityManager)));

        _popup.PopupPredicted(message, target, args.User);
    }

    private void OnAttachDoAfter(Entity<ClothingAccessoryComponent> ent, ref ClothingAccessoryAttachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!TryFindWornHolder(target, ent.Comp, out var holder))
            return;

        TryAttach(holder.Value, ent, args.User);
    }

    private void OnHolderVerbs(Entity<ClothingAccessoryHolderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var accessories = GetAccessories(ent);
        if (accessories.Count == 0)
            return;

        var user = args.User;
        var category = accessories.Count > 1 ? DetachCategory : null;

        // Verbs run later, when the container may have changed.
        foreach (var accessory in accessories.ToArray())
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = category == null
                    ? Loc.GetString("pirate-clothing-accessory-verb-detach", ("accessory", accessory))
                    : Name(accessory),
                Category = category,
                IconEntity = GetNetEntity(accessory),
                Priority = -1,
                Act = () => TryDetach(ent, accessory, user),
            });
        }
    }

    #endregion

    #region Loadout

    private void OnLoadoutCheck(Entity<ClothingAccessoryComponent> ent, ref LoadoutAccessoryCheckEvent args)
    {
        args.IsAccessory = true;
    }

    private void OnLoadoutAttach(Entity<ClothingAccessoryComponent> ent, ref LoadoutAccessoryAttachEvent args)
    {
        args.Attached = TryAttachToWorn(args.Wearer, ent);
    }

    public bool TryAttachToWorn(EntityUid wearer, Entity<ClothingAccessoryComponent> accessory)
    {
        var slots = _inventory.GetSlotEnumerator(wearer, WornHolderSlots);
        while (slots.NextItem(out var item))
        {
            if (!TryComp<ClothingAccessoryHolderComponent>(item, out var holder) ||
                !Accepts((item, holder), accessory.Comp.Category))
                continue;

            if (TryAttach((item, holder), accessory, null))
                return true;
        }

        return false;
    }

    #endregion

    #region Examine

    private void OnHolderExamined(Entity<ClothingAccessoryHolderComponent> ent, ref ExaminedEvent args)
    {
        var accessories = GetAccessories(ent);
        if (accessories.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("pirate-clothing-accessory-examine",
            ("accessories", ListNames(accessories))));
    }

    public void AddWearerExamine(EntityUid item, EntityUid wearer, ExaminedEvent args)
    {
        if (!TryComp<ClothingAccessoryHolderComponent>(item, out var holder))
            return;

        var accessories = GetAccessories((item, holder));
        if (accessories.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("pirate-clothing-accessory-examine-wearer",
            ("wearer", Identity.Entity(wearer, EntityManager)),
            ("holder", item),
            ("accessories", ListNames(accessories))));
    }

    private string ListNames(IReadOnlyList<EntityUid> accessories)
    {
        return string.Join(", ", accessories.Select(a => Loc.GetString("pirate-clothing-accessory-examine-item", ("accessory", a))));
    }

    #endregion

    #region Container

    private void OnInserted(Entity<ClothingAccessoryHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        _item.VisualsChanged(ent.Owner);

        var ev = new ClothingAccessoriesChangedEvent();
        RaiseLocalEvent(ent, ref ev);

        if (_inventory.TryGetContainingEntity(ent.Owner, out var wearer))
            GrantActions(args.Entity, wearer.Value);
    }

    private void OnRemoved(Entity<ClothingAccessoryHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        _item.VisualsChanged(ent.Owner);

        var ev = new ClothingAccessoriesChangedEvent();
        RaiseLocalEvent(ent, ref ev);

        if (_inventory.TryGetContainingEntity(ent.Owner, out var wearer))
            RemoveActions(args.Entity, wearer.Value);
    }

    private void OnHolderTerminating(Entity<ClothingAccessoryHolderComponent> ent, ref EntityTerminatingEvent args)
    {
        // Do not spawn contained items while their parent is being deleted.
        if (!_net.IsServer || TerminatingOrDeleted(Transform(ent).ParentUid))
            return;

        if (_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container))
            _container.EmptyContainer(container);
    }

    #endregion

    #region Worn accessories

    private void OnHolderEquipped(Entity<ClothingAccessoryHolderComponent> ent, ref GotEquippedEvent args)
    {
        foreach (var accessory in GetAccessories(ent))
        {
            GrantActions(accessory, args.Equipee);
        }
    }

    private void OnHolderUnequipped(Entity<ClothingAccessoryHolderComponent> ent, ref GotUnequippedEvent args)
    {
        foreach (var accessory in GetAccessories(ent))
        {
            RemoveActions(accessory, args.Equipee);
        }
    }

    private void OnRelayInnateVerbs(Entity<ClothingAccessoryHolderComponent> ent,
        ref InventoryRelayedEvent<GetVerbsEvent<InnateVerb>> args)
    {
        foreach (var accessory in GetAccessories(ent))
        {
            RaiseLocalEvent(accessory, args);
        }
    }

    private void GrantActions(EntityUid accessory, EntityUid wearer)
    {
        // Actions arrive with the game state.
        if (_timing.ApplyingState ||
            !TryComp<ClothingAccessoryComponent>(accessory, out var comp) ||
            GetCategory(comp) is not { } category)
            return;

        SlotFlags? slot = Enum.TryParse<SlotFlags>(category.Slot, true, out var flags) ? flags : null;
        var ev = new GetItemActionsEvent(_actionContainer, wearer, accessory, slot);
        RaiseLocalEvent(accessory, ev);

        if (ev.Actions.Count > 0)
            _actions.GrantActions(wearer, ev.Actions, accessory);
    }

    private void RemoveActions(EntityUid accessory, EntityUid wearer)
    {
        if (!_timing.ApplyingState)
            _actions.RemoveProvidedActions(wearer, accessory);
    }

    public bool TryGetWearer(EntityUid accessory, [NotNullWhen(true)] out EntityUid? wearer)
    {
        wearer = null;
        return HasComp<ClothingAccessoryComponent>(accessory) &&
               _container.TryGetContainingContainer(accessory, out var container) &&
               TryComp<ClothingAccessoryHolderComponent>(container.Owner, out var holder) &&
               container.ID == holder.ContainerId &&
               _inventory.InSlotWithAnyFlags(container.Owner, WornHolderSlots) &&
               _inventory.TryGetContainingEntity(container.Owner, out wearer);
    }

    private void OnGetWearer(Entity<ClothingAccessoryComponent> ent, ref GetClothingAccessoryWearerEvent args)
    {
        if (TryGetWearer(ent, out var wearer))
            args.Wearer = wearer;
    }

    #endregion

    #region API

    public IReadOnlyList<EntityUid> GetAccessories(EntityUid clothing)
    {
        return TryComp<ClothingAccessoryHolderComponent>(clothing, out var holder)
            ? GetAccessories((clothing, holder))
            : Array.Empty<EntityUid>();
    }

    public IReadOnlyList<EntityUid> GetAccessories(Entity<ClothingAccessoryHolderComponent> holder)
    {
        return _container.TryGetContainer(holder.Owner, holder.Comp.ContainerId, out var container)
            ? container.ContainedEntities
            : Array.Empty<EntityUid>();
    }

    public bool TryFindWornHolder(EntityUid wearer,
        ClothingAccessoryComponent accessory,
        [NotNullWhen(true)] out Entity<ClothingAccessoryHolderComponent>? holder)
    {
        holder = null;

        var slots = _inventory.GetSlotEnumerator(wearer, WornHolderSlots);
        while (slots.NextItem(out var item))
        {
            if (!TryComp<ClothingAccessoryHolderComponent>(item, out var comp) ||
                !Accepts((item, comp), accessory.Category))
                continue;

            holder = (item, comp);
            return true;
        }

        return false;
    }

    // Accessories are not holders, even when their base item grants a holder component.
    public bool Accepts(Entity<ClothingAccessoryHolderComponent> holder, string category)
    {
        return holder.Comp.Limits.ContainsKey(category) && !HasComp<ClothingAccessoryComponent>(holder);
    }

    public bool CanAttach(Entity<ClothingAccessoryHolderComponent> holder,
        Entity<ClothingAccessoryComponent> accessory,
        EntityUid? user)
    {
        if (!Accepts(holder, accessory.Comp.Category) ||
            !holder.Comp.Limits.TryGetValue(accessory.Comp.Category, out var limit))
        {
            Popup("pirate-clothing-accessory-not-allowed", holder, accessory, user);
            return false;
        }

        var attached = GetAccessories(holder);
        if (attached.Count >= holder.Comp.MaxAccessories)
        {
            Popup("pirate-clothing-accessory-full", holder, accessory, user);
            return false;
        }

        var sameCategory = 0;
        var proto = Prototype(accessory)?.ID;
        foreach (var other in attached)
        {
            if (!TryComp<ClothingAccessoryComponent>(other, out var otherComp) ||
                otherComp.Category != accessory.Comp.Category)
                continue;

            sameCategory++;

            if (!accessory.Comp.AllowDuplicates && proto != null && Prototype(other)?.ID == proto)
            {
                Popup("pirate-clothing-accessory-duplicate", holder, accessory, user);
                return false;
            }
        }

        if (sameCategory >= limit)
        {
            Popup("pirate-clothing-accessory-category-full", holder, accessory, user);
            return false;
        }

        return true;
    }

    public bool TryAttach(Entity<ClothingAccessoryHolderComponent> holder,
        Entity<ClothingAccessoryComponent> accessory,
        EntityUid? user)
    {
        if (!CanAttach(holder, accessory, user))
            return false;

        var container = _container.EnsureContainer<Container>(holder.Owner, holder.Comp.ContainerId);
        return _container.Insert(accessory.Owner, container);
    }

    public bool TryDetach(Entity<ClothingAccessoryHolderComponent> holder, EntityUid accessory, EntityUid user)
    {
        if (!_container.TryGetContainer(holder.Owner, holder.Comp.ContainerId, out var container) ||
            !_container.Remove(accessory, container))
            return false;

        _hands.PickupOrDrop(user, accessory);
        return true;
    }

    public ClothingAccessoryCategoryPrototype? GetCategory(ClothingAccessoryComponent accessory)
    {
        return _proto.TryIndex(accessory.Category, out var category) ? category : null;
    }

    #endregion

    private void Popup(string loc, EntityUid holder, EntityUid accessory, EntityUid? user)
    {
        if (user == null)
            return;

        _popup.PopupClient(Loc.GetString(loc, ("accessory", accessory), ("holder", holder)), holder, user.Value);
    }
}
