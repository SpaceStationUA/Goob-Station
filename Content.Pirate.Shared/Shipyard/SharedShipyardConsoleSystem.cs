using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Content.Pirate.Shared.Shipyard.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Shipyard;

/// <summary>Shared interaction and validation for shipyard consoles.</summary>
public abstract class SharedShipyardConsoleSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ShipyardConsoleComponent>(ShipyardConsoleUiKey.Key, subs =>
        {
            subs.Event<ShipyardConsolePurchaseMessage>(OnPurchase);
        });
    }

    private void OnPurchase(Entity<ShipyardConsoleComponent> ent, ref ShipyardConsolePurchaseMessage msg)
    {
        var user = msg.Actor;
        if (!_access.IsAllowed(user, ent.Owner))
        {
            Popup.PopupClient(Loc.GetString("comms-console-permission-denied"), ent, user);
            Audio.PlayPredicted(ent.Comp.DenySound, ent, user);
            return;
        }

        if (!_proto.TryIndex(msg.Vessel, out var vessel) || _whitelist.IsWhitelistFail(vessel.Whitelist, ent))
            return;

        TryPurchase(ent, user, vessel);
    }

    protected virtual void TryPurchase(Entity<ShipyardConsoleComponent> ent, EntityUid user, VesselPrototype vessel)
    {
    }
}
