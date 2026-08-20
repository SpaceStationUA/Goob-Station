using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Whitelist;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class HolsterModuleHandler : ModuleActionHandler
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ModularSuitActionHolderComponent, ToggleHolsterModuleEvent>(OnToggle);
    }

    private void OnToggle(Entity<ModularSuitActionHolderComponent> ent, ref ToggleHolsterModuleEvent args)
    {
        if (args.Handled)
            return;

        // The holster is a mechanical mount, so it stays usable while the suit is not deployed.
        if (!TryFindModuleByAction(ent, args.Action, out var moduleEnt, requireActive: false))
            return;

        if (!TryComp<ModularSuitModuleComponent>(moduleEnt, out var moduleComp) || !moduleComp.IsActive)
            return;

        var holster = Container.GetContainer(moduleEnt.Value, args.TargetContainerId);
        if (holster == null)
            return;

        var user = args.Performer;
        if (holster.ContainedEntities.Count > 0)
        {
            var item = holster.ContainedEntities[0];
            var activeHand = _hands.GetActiveHand(user);
            if (activeHand != null && _hands.CanPickupToHand(user, item, activeHand, showPopup: true))
            {
                if (!ModularSuit.TryUseCoreCharge(ent.Owner, moduleComp.PowerInstanceUsage))
                    return;

                if (!_hands.TryPickup(user, item))
                    return;

                Audio.PlayPvs(args.EjectSound, ent.Owner);
            }
        }
        else
        {
            if (!_itemSlots.TryGetSlot(moduleEnt.Value, args.TargetContainerId, out var slot))
                return;

            var item = _hands.GetActiveItem(user);
            if (item == null)
            {
                Popup.PopupEntity(Loc.GetString("modsuit-holster-empty-hand"), ent.Owner, user);
                return;
            }

            if (_whitelist.IsWhitelistFail(slot.Whitelist, item.Value)
                || _whitelist.IsWhitelistPass(slot.Blacklist, item.Value)
                || !Container.CanInsert(item.Value, holster))
            {
                Popup.PopupEntity(Loc.GetString("modsuit-holster-cant-holster"), ent.Owner, user);
            }
            else
            {
                if (!ModularSuit.TryUseCoreCharge(ent.Owner, moduleComp.PowerInstanceUsage))
                    return;

                if (!Container.Insert(item.Value, holster))
                {
                    Popup.PopupEntity(Loc.GetString("modsuit-holster-cant-holster"), ent.Owner, user);
                    return;
                }

                Audio.PlayPvs(args.InsertSound, ent.Owner);
            }
        }

        args.Handled = true;
        _actions.SetToggled(args.Action.Owner, holster.ContainedEntities.Count > 0);
    }
}
