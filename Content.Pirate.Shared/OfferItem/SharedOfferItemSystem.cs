using Content.Shared.Interaction;
using Content.Shared.IdentityManagement;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Network;

namespace Content.Pirate.Shared.OfferItem;

public abstract partial class SharedOfferItemSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OfferItemComponent, InteractUsingEvent>(SetInReceiveMode);
        SubscribeLocalEvent<OfferItemComponent, MoveEvent>(OnMove);
        InitializeInteractions();
    }

    private void SetInReceiveMode(EntityUid uid, OfferItemComponent component, InteractUsingEvent args)
    {
        if (!TryComp<OfferItemComponent>(args.User, out var offerItem))
            return;

        if (args.User == uid || component.IsInReceiveMode || !offerItem.IsInOfferMode ||
            (offerItem.IsInReceiveMode && offerItem.Target != uid))
            return;

        if (!_interaction.InRangeUnobstructed(args.User, uid, offerItem.MaxOfferDistance))
            return;

        if (!TryGetOfferedItem(args.User, offerItem, out var offeredItem))
            return;

        component.IsInReceiveMode = true;
        component.Target = args.User;

        Dirty(uid, component);

        offerItem.Target = uid;
        offerItem.IsInOfferMode = false;
        offerItem.Item = offeredItem.Value;

        Dirty(args.User, offerItem);

        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("offer-item-try-give",
                ("item", Identity.Entity(offeredItem.Value, EntityManager)),
                ("target", Identity.Entity(uid, EntityManager))),
                component.Target.Value,
                component.Target.Value);
            _popup.PopupEntity(Loc.GetString("offer-item-try-give-target",
                ("user", Identity.Entity(component.Target.Value, EntityManager)),
                ("item", Identity.Entity(offeredItem.Value, EntityManager))),
                component.Target.Value,
                uid);
        }

        args.Handled = true;
    }

    private void OnMove(EntityUid uid, OfferItemComponent component, ref MoveEvent args)
    {
        if (component.Target == null ||
            _interaction.InRangeUnobstructed(uid, component.Target.Value, component.MaxOfferDistance))
            return;

        if (component.IsInReceiveMode)
            UnReceive(uid, component);
        else
            UnOffer(uid, component);
    }

    /// <summary>
    /// Resets the <see cref="OfferItemComponent"/> of the user and the target
    /// </summary>
    protected void UnOffer(EntityUid uid, OfferItemComponent component)
    {
        ClearOfferState(uid, component, true);
    }


    /// <summary>
    /// Cancels the transfer of the item
    /// </summary>
    protected void UnReceive(EntityUid uid, OfferItemComponent? component = null)
    {
        if (component == null && !TryComp(uid, out component))
            return;

        ClearOfferState(uid, component, true);
    }

    /// <summary>
    /// Returns true if <see cref="OfferItemComponent.IsInOfferMode"/> = true
    /// </summary>
    protected bool IsInOfferMode(EntityUid? entity, OfferItemComponent? component = null)
    {
        return entity != null && Resolve(entity.Value, ref component, false) && component.IsInOfferMode;
    }

    protected bool TryGetOfferedItem(EntityUid uid, OfferItemComponent component, [NotNullWhen(true)] out EntityUid? item)
    {
        item = null;

        if (!TryComp<HandsComponent>(uid, out var hands))
            return false;

        if (component.Hand != null &&
            _hands.TryGetHeldItem((uid, hands), component.Hand, out item))
        {
            return true;
        }

        item = component.Item;
        return item != null && Exists(item.Value);
    }

    protected void ResetOfferState(OfferItemComponent component)
    {
        component.IsInOfferMode = false;
        component.IsInReceiveMode = false;
        component.Hand = null;
        component.Item = null;
        component.Target = null;
    }

    protected void ClearOfferState(EntityUid uid, OfferItemComponent component, bool popup)
    {
        var hasPair = TryResolveOfferPair(uid, component, out var offererUid, out var offerer, out var receiverUid, out var receiver);
        EntityUid? offeredItem = null;

        if (hasPair)
            TryGetOfferedItem(offererUid, offerer, out offeredItem);

        if (_net.IsServer && popup && hasPair && offeredItem != null)
        {
            _popup.PopupEntity(Loc.GetString("offer-item-no-give",
                ("item", Identity.Entity(offeredItem.Value, EntityManager)),
                ("target", Identity.Entity(receiverUid, EntityManager))),
                offererUid,
                offererUid);
            _popup.PopupEntity(Loc.GetString("offer-item-no-give-target",
                ("user", Identity.Entity(offererUid, EntityManager)),
                ("item", Identity.Entity(offeredItem.Value, EntityManager))),
                offererUid,
                receiverUid);
        }

        if (hasPair)
        {
            ResetOfferState(offerer);
            Dirty(offererUid, offerer);

            ResetOfferState(receiver);
            Dirty(receiverUid, receiver);
            return;
        }

        ResetOfferState(component);
        Dirty(uid, component);
    }

    private bool TryResolveOfferPair(
        EntityUid uid,
        OfferItemComponent component,
        out EntityUid offererUid,
        out OfferItemComponent offerer,
        out EntityUid receiverUid,
        out OfferItemComponent receiver)
    {
        offererUid = default;
        offerer = default!;
        receiverUid = default;
        receiver = default!;

        if (component.Target == null || !TryComp<OfferItemComponent>(component.Target.Value, out var other))
            return false;

        if (component.IsInReceiveMode)
        {
            receiverUid = uid;
            receiver = component;
            offererUid = component.Target.Value;
            offerer = other;
            return true;
        }

        offererUid = uid;
        offerer = component;
        receiverUid = component.Target.Value;
        receiver = other;
        return true;
    }
}
