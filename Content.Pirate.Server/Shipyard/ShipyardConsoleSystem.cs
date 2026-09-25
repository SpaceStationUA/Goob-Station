using Content.Pirate.Shared.Shipyard;
using Content.Pirate.Shared.Shipyard.Prototypes;
using Content.Server.Cargo.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Station.Components;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Map.Components;

namespace Content.Pirate.Server.Shipyard;

public sealed class ShipyardConsoleSystem : SharedShipyardConsoleSystem
{
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly StationSystem _station = default!;
    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ShipyardConsoleComponent>(ShipyardConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
        });
        SubscribeLocalEvent<BankBalanceUpdatedEvent>(OnBalanceUpdated);
    }

    protected override void TryPurchase(Entity<ShipyardConsoleComponent> ent, EntityUid user, VesselPrototype vessel)
    {
        if (_whitelist.IsWhitelistFail(vessel.Whitelist, ent))
            return;

        if (vessel.Price <= 0)
        {
            Deny(ent, user, "shipyard-console-invalid-price");
            return;
        }

        var categoryAllowed = false;
        foreach (var category in vessel.Categories)
        {
            if (!ent.Comp.Categories.Contains(category))
                continue;

            categoryAllowed = true;
            break;
        }

        if (!categoryAllowed)
        {
            Deny(ent, user, "shipyard-console-purchase-failed");
            return;
        }

        EntityUid destination;
        Entity<StationBankAccountComponent>? bankAccount = null;
        if (ent.Comp.UseStationFunds)
        {
            bankAccount = GetBankAccount(ent);
            if (bankAccount is not { } bank)
            {
                Deny(ent, user, "shipyard-console-no-bank");
                return;
            }

            if (!_cargo.TryGetAccount((bank.Owner, (StationBankAccountComponent?) bank.Comp), bank.Comp.PrimaryAccount, out var balance) || balance < vessel.Price)
            {
                var popup = Loc.GetString("cargo-console-insufficient-funds", ("cost", vessel.Price));
                Popup.PopupEntity(popup, ent, user, PopupType.SmallCaution);
                Audio.PlayPvs(ent.Comp.DenySound, ent);
                return;
            }

            if (!TryComp<StationDataComponent>(bank.Owner, out var stationData) ||
                _station.GetLargestGrid((bank.Owner, stationData)) is not { } grid ||
                !TryComp<MapGridComponent>(grid, out _))
            {
                Deny(ent, user, "shipyard-console-purchase-failed");
                return;
            }

            destination = grid;
        }
        else
        {
            if (Transform(ent).GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out _))
            {
                Deny(ent, user, "shipyard-console-purchase-failed");
                return;
            }

            destination = grid;
        }
        var fundsCharged = false;
        var fundsRefunded = false;
        Action? refundFunds = null;
        if (bankAccount is { } account)
        {
            _cargo.UpdateBankAccount((account.Owner, account.Comp), -vessel.Price, account.Comp.PrimaryAccount);
            fundsCharged = true;
            refundFunds = () =>
            {
                if (!fundsCharged || fundsRefunded)
                    return;

                fundsRefunded = true;
                _cargo.UpdateBankAccount((account.Owner, account.Comp), vessel.Price, account.Comp.PrimaryAccount);
            };
        }

        if (!_shipyard.TrySendShuttle(destination, vessel.Path, vessel.Delay, out var shuttle, refundFunds))
        {
            refundFunds?.Invoke();
            Deny(ent, user, "shipyard-console-purchase-failed");
            return;
        }


        if (vessel.Delay > 0)
            _radio.SendRadioMessage(ent, Loc.GetString("shipyard-console-docking", ("vessel", Loc.GetString(vessel.Name)), ("delay", vessel.Delay)), ent.Comp.Channel, ent);
        Audio.PlayPvs(ent.Comp.ConfirmSound, ent);
    }

    private void Deny(Entity<ShipyardConsoleComponent> ent, EntityUid user, string message)
    {
        Popup.PopupEntity(Loc.GetString(message), ent, user, PopupType.SmallCaution);
        Audio.PlayPvs(ent.Comp.DenySound, ent);
    }

    private void OnBalanceUpdated(ref BankBalanceUpdatedEvent args)
    {
        var query = EntityQueryEnumerator<ShipyardConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!_ui.IsUiOpen(uid, ShipyardConsoleUiKey.Key))
                continue;

            if (GetBankAccount(uid) is { } bank &&
                bank.Owner == args.Station &&
                args.Balance.TryGetValue(bank.Comp.PrimaryAccount, out var balance))
                UpdateUI((uid, component), balance);
        }
    }

    private void OnOpened(Entity<ShipyardConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!ent.Comp.UseStationFunds)
        {
            UpdateUI(ent, 0);
            return;
        }

        if (GetBankAccount(ent) is { } bank)
            UpdateUI(ent, bank.Comp.Accounts.GetValueOrDefault(bank.Comp.PrimaryAccount));
    }

    private void UpdateUI(Entity<ShipyardConsoleComponent> ent, int balance)
    {
        if (_shipyard.Enabled)
            _ui.SetUiState(ent.Owner, ShipyardConsoleUiKey.Key, new ShipyardConsoleState(balance));
    }

    private Entity<StationBankAccountComponent>? GetBankAccount(EntityUid console)
    {
        if (_station.GetOwningStation(console) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
            return null;

        return (station, bank);
    }
}
