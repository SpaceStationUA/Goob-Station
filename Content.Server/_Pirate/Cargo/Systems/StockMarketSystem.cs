using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server._Pirate.Cargo.Components;
using Content.Server._Pirate.CartridgeLoader.Cartridges;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared._Pirate.CartridgeLoader.Cartridges;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Cargo.Systems;

public sealed class StockMarketSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private ISawmill _sawmill = default!;
    private const float MaxPrice = 262144;
    private readonly Dictionary<EntityUid, TimeSpan> _nextUpdate = new();

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _log.GetSawmill("admin.stock_market");

        SubscribeLocalEvent<StockTradingCartridgeComponent, CartridgeMessageEvent>(OnStockTradingMessage);
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<StationStockMarketComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            if (!_nextUpdate.TryGetValue(uid, out var nextUpdate))
            {
                _nextUpdate[uid] = curTime + component.UpdateInterval;
                continue;
            }

            if (curTime < nextUpdate)
                continue;

            _nextUpdate[uid] = curTime + component.UpdateInterval;
            UpdateStockPrices(uid, component);
        }
    }

    private void OnStationInitialized(StationInitializedEvent args)
    {
        if (!TryComp<StationStockMarketComponent>(args.Station, out var stockMarket) ||
            !HasComp<StationCargoOrderDatabaseComponent>(args.Station) ||
            !HasComp<StationBankAccountComponent>(args.Station))
        {
            return;
        }

        _nextUpdate[args.Station] = _timing.CurTime + stockMarket.UpdateInterval;
        UpdateStockMarket(args.Station);
    }

    private void OnStockTradingMessage(Entity<StockTradingCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not StockTradingUiMessageEvent message)
            return;

        var user = args.Actor;
        var companyIndex = message.CompanyIndex;
        var amount = message.Amount;
        var loader = GetEntity(args.LoaderUid);

        if (ent.Comp.Station is not {} station || !TryComp<StationStockMarketComponent>(station, out var stockMarket))
            return;

        if (companyIndex < 0 || companyIndex >= stockMarket.Companies.Count)
            return;

        if (!TryComp<AccessReaderComponent>(ent, out var access))
            return;

        if (!_idCard.TryGetIdCard(loader, out var idCard) || !_access.IsAllowed(idCard, ent.Owner, access))
        {
            _audio.PlayEntity(stockMarket.DenySound, loader, user);
            return;
        }

        try
        {
            var company = stockMarket.Companies[companyIndex];

            bool success;
            switch (message.Action)
            {
                case StockTradingUiAction.Buy:
                    _adminLogger.Add(LogType.Action,
                        LogImpact.Medium,
                        $"{ToPrettyString(user):user} attempting to buy {amount} stocks of {company.LocalizedDisplayName}");
                    success = TryChangeStocks(station, stockMarket, companyIndex, amount, user);
                    break;
                case StockTradingUiAction.Sell:
                    _adminLogger.Add(LogType.Action,
                        LogImpact.Medium,
                        $"{ToPrettyString(user):user} attempting to sell {amount} stocks of {company.LocalizedDisplayName}");
                    success = TryChangeStocks(station, stockMarket, companyIndex, -amount, user);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown UiAction type [{message.Action}]");
            }

            _audio.PlayEntity(success ? stockMarket.ConfirmSound : stockMarket.DenySound, loader, user);
        }
        finally
        {
            UpdateStockMarket(station);
        }
    }

    private void UpdateStockMarket(EntityUid station)
    {
        var ev = new StockMarketUpdatedEvent(station);
        RaiseLocalEvent(ref ev);
    }

    private bool TryChangeStocks(
        EntityUid station,
        StationStockMarketComponent stockMarket,
        int companyIndex,
        int amount,
        EntityUid user)
    {
        if (amount == 0 || companyIndex < 0 || companyIndex >= stockMarket.Companies.Count)
            return false;

        if (!TryComp<StationBankAccountComponent>(station, out var bank))
            return false;

        var company = stockMarket.Companies[companyIndex];
        var totalValue = (int) Math.Round(company.CurrentPrice * amount);

        if (!stockMarket.StockOwnership.TryGetValue(companyIndex, out var currentOwned))
            currentOwned = 0;

        if (amount > 0)
        {
            if (bank.Accounts[bank.PrimaryAccount] < totalValue)
                return false;
        }
        else
        {
            var selling = -amount;
            if (currentOwned < selling)
                return false;
        }

        var newAmount = currentOwned + amount;
        if (newAmount > 0)
            stockMarket.StockOwnership[companyIndex] = newAmount;
        else
            stockMarket.StockOwnership.Remove(companyIndex);

        _cargo.UpdateBankAccount((station, bank), -totalValue, bank.PrimaryAccount);

        var verb = amount > 0 ? "bought" : "sold";
        _adminLogger.Add(LogType.Action,
            LogImpact.Medium,
            $"[StockMarket] {ToPrettyString(user):user} {verb} {Math.Abs(amount)} stocks of {company.LocalizedDisplayName} at {company.CurrentPrice:F2} spesos each (Total: {totalValue})");

        return true;
    }

    private void UpdateStockPrices(EntityUid station, StationStockMarketComponent stockMarket)
    {
        for (var i = 0; i < stockMarket.Companies.Count; i++)
        {
            var company = stockMarket.Companies[i];
            var changeType = DetermineMarketChange(stockMarket.MarketChanges);
            var multiplier = CalculatePriceMultiplier(changeType);

            UpdatePriceHistory(ref company);

            var oldPrice = company.CurrentPrice;
            company.CurrentPrice *= 1 + multiplier;
            company.CurrentPrice = MathF.Max(company.CurrentPrice, company.BasePrice * 0.1f);
            company.CurrentPrice = MathF.Min(company.CurrentPrice, MaxPrice);

            stockMarket.Companies[i] = company;

            var percentChange = (company.CurrentPrice - oldPrice) / oldPrice * 100;

            UpdateStockMarket(station);

            _adminLogger.Add(LogType.Action,
                LogImpact.Medium,
                $"[StockMarket] Company '{company.LocalizedDisplayName}' price updated by {percentChange:+0.00;-0.00}% from {oldPrice:0.00} to {company.CurrentPrice:0.00}");
        }
    }

    public bool TryChangeStocksPrice(
        EntityUid station,
        StationStockMarketComponent stockMarket,
        float newPrice,
        int companyIndex)
    {
        if (newPrice > MaxPrice)
        {
            _sawmill.Error($"New price cannot be greater than {MaxPrice}.");
            return false;
        }

        if (companyIndex < 0 || companyIndex >= stockMarket.Companies.Count)
            return false;

        var company = stockMarket.Companies[companyIndex];
        UpdatePriceHistory(ref company);

        company.CurrentPrice = MathF.Max(newPrice, company.BasePrice * 0.1f);
        stockMarket.Companies[companyIndex] = company;

        UpdateStockMarket(station);
        return true;
    }

    public bool TryAddCompany(
        EntityUid station,
        StationStockMarketComponent stockMarket,
        float basePrice,
        string displayName)
    {
        var company = new StockCompany
        {
            LocalizedDisplayName = displayName,
            BasePrice = basePrice,
            CurrentPrice = basePrice,
            PriceHistory = [],
        };

        UpdatePriceHistory(ref company);
        stockMarket.Companies.Add(company);

        UpdateStockMarket(station);
        return true;
    }

    private static void UpdatePriceHistory(ref StockCompany company)
    {
        company.PriceHistory ??= [];

        while (company.PriceHistory.Count < 5)
        {
            company.PriceHistory.Add(company.BasePrice);
        }

        company.PriceHistory.Add(company.CurrentPrice);

        if (company.PriceHistory.Count > 5)
            company.PriceHistory.RemoveAt(1);
    }

    private MarketChange DetermineMarketChange(List<MarketChange> marketChanges)
    {
        var roll = _random.NextFloat();
        var cumulative = 0f;

        foreach (var change in marketChanges)
        {
            cumulative += change.Chance;
            if (roll <= cumulative)
                return change;
        }

        return marketChanges[0];
    }

    private float CalculatePriceMultiplier(MarketChange change)
    {
        var u1 = _random.NextFloat();
        var u2 = _random.NextFloat();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

        var range = change.Range.Y - change.Range.X;
        var mean = (change.Range.Y + change.Range.X) / 2;
        var stdDev = range / 6.0f;

        var result = (float) (mean + (stdDev * randStdNormal));
        return Math.Clamp(result, change.Range.X, change.Range.Y);
    }
}

[ByRefEvent]
public record struct StockMarketUpdatedEvent(EntityUid Station);
