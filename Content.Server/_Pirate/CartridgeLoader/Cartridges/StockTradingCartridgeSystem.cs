using Content.Server.CartridgeLoader;
using Content.Server.Cargo.Components;
using Content.Server.Station.Systems;
using Content.Server._Pirate.Cargo.Components;
using Content.Server._Pirate.Cargo.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared._Pirate.CartridgeLoader.Cartridges;

namespace Content.Server._Pirate.CartridgeLoader.Cartridges;

public sealed class StockTradingCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StockTradingCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<StockMarketUpdatedEvent>(OnStockMarketUpdated);
        SubscribeLocalEvent<BankBalanceUpdatedEvent>(OnBalanceUpdated);
    }

    private void OnBalanceUpdated(ref BankBalanceUpdatedEvent args)
    {
        UpdateAllCartridges(args.Station);
    }

    private void OnUiReady(Entity<StockTradingCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUI(ent, args.Loader);
    }

    private void OnStockMarketUpdated(ref StockMarketUpdatedEvent args)
    {
        UpdateAllCartridges(args.Station);
    }

    private void UpdateAllCartridges(EntityUid station)
    {
        var query = EntityQueryEnumerator<StockTradingCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out var comp, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loader || comp.Station != station)
                continue;

            UpdateUI((uid, comp), loader);
        }
    }

    private void UpdateUI(Entity<StockTradingCartridgeComponent> ent, EntityUid loader)
    {
        if (_station.GetOwningStation(loader) is { } station)
            ent.Comp.Station = station;

        if (!TryComp<StationStockMarketComponent>(ent.Comp.Station, out var stockMarket) ||
            !TryComp<StationBankAccountComponent>(ent.Comp.Station, out var bankAccount))
        {
            return;
        }

        var state = new StockTradingUiState(
            entries: stockMarket.Companies,
            ownedStocks: stockMarket.StockOwnership,
            balance: bankAccount.Accounts[bankAccount.PrimaryAccount]
        );

        _cartridgeLoader.UpdateCartridgeUiState(loader, state);
    }
}
