using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class StockTradingUiState(
    List<StockCompany> entries,
    Dictionary<int, int> ownedStocks,
    float balance)
    : BoundUserInterfaceState
{
    public readonly List<StockCompany> Entries = entries;
    public readonly Dictionary<int, int> OwnedStocks = ownedStocks;
    public readonly float Balance = balance;
}

[DataDefinition, Serializable]
public partial struct StockCompany
{
    [DataField(required: true)]
    public LocId? DisplayName;

    private string? _displayName;

    [Access(Other = AccessPermissions.ReadWriteExecute)]
    public string LocalizedDisplayName
    {
        get => _displayName ?? Loc.GetString(DisplayName ?? string.Empty);
        set => _displayName = value;
    }

    [DataField(required: true)]
    public float CurrentPrice;

    [DataField(required: true)]
    public float BasePrice;

    [DataField]
    public List<float>? PriceHistory;

    public StockCompany(string displayName, float currentPrice, float basePrice, List<float>? priceHistory)
    {
        DisplayName = displayName;
        _displayName = null;
        CurrentPrice = currentPrice;
        BasePrice = basePrice;
        PriceHistory = priceHistory ?? [];
    }
}
