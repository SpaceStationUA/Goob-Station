using Content.Server.Popups;
using Content.Shared.Hands.Components;
using Content.Shared.Alert;
using Content.Shared.Hands.EntitySystems;
using Content.Pirate.Shared.OfferItem;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Robust.Shared.Player;

namespace Content.Pirate.Server.OfferItem;

public sealed class OfferItemSystem : SharedOfferItemSystem
{
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OfferItemComponent, AcceptOfferAlertEvent>(OnAcceptOffer);
    }

    private void OnAcceptOffer(Entity<OfferItemComponent> ent, ref AcceptOfferAlertEvent args)
    {
        Receive(ent.Owner, ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OfferItemComponent>();
        while (query.MoveNext(out var uid, out var offerItem))
        {
            if (!offerItem.IsInReceiveMode &&
                (offerItem.IsInOfferMode || offerItem.Target != null) &&
                !TryGetOfferedItem(uid, offerItem, out _))
            {
                UnOffer(uid, offerItem);
                continue;
            }

            if (!offerItem.IsInReceiveMode)
            {
                _alertsSystem.ClearAlert(uid, "Offer");
                continue;
            }

            _alertsSystem.ShowAlert(uid, "Offer");
        }
    }

    /// <summary>
    /// Accepting the offer and receive item
    /// </summary>
    public void Receive(EntityUid uid, OfferItemComponent? component = null)
    {
        if (!Resolve(uid, ref component) ||
            !TryComp<OfferItemComponent>(component.Target, out var offerItem) ||
            offerItem.Hand == null ||
            component.Target == null ||
            !TryComp<HandsComponent>(uid, out var hands))
            return;

        if (!_interaction.InRangeUnobstructed(uid, component.Target.Value, offerItem.MaxOfferDistance))
        {
            UnReceive(uid, component);
            return;
        }

        if (TryGetOfferedItem(component.Target.Value, offerItem, out var offeredItem))
        {
            if (!_hands.TryPickup(uid, offeredItem.Value, handsComp: hands))
            {
                _popup.PopupEntity(Loc.GetString("offer-item-full-hand"), uid, uid);
                return;
            }

            _popup.PopupEntity(Loc.GetString("offer-item-give",
                ("item", Identity.Entity(offeredItem.Value, EntityManager)),
                ("target", Identity.Entity(uid, EntityManager))),
                component.Target.Value,
                component.Target.Value);
            _popup.PopupEntity(Loc.GetString("offer-item-give-other",
                    ("user", Identity.Entity(component.Target.Value, EntityManager)),
                    ("item", Identity.Entity(offeredItem.Value, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager))),
                component.Target.Value,
                Filter.PvsExcept(component.Target.Value, entityManager: EntityManager),
                true);
        }

        ResetOfferState(offerItem);
        Dirty(component.Target.Value, offerItem);

        ResetOfferState(component);
        Dirty(uid, component);
    }
}
