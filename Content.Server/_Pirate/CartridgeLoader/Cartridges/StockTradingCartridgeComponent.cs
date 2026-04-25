namespace Content.Server._Pirate.CartridgeLoader.Cartridges;

[RegisterComponent, Access(typeof(StockTradingCartridgeSystem))]
public sealed partial class StockTradingCartridgeComponent : Component
{
    [DataField]
    public EntityUid? Station;
}
