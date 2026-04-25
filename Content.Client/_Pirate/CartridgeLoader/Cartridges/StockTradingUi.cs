using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared._Pirate.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.CartridgeLoader.Cartridges;

public sealed partial class StockTradingUi : UIFragment
{
    private StockTradingUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new StockTradingUiFragment();

        _fragment.OnBuyButtonPressed += (company, amount) =>
        {
            SendStockTradingUiMessage(StockTradingUiAction.Buy, company, amount, userInterface);
        };
        _fragment.OnSellButtonPressed += (company, amount) =>
        {
            SendStockTradingUiMessage(StockTradingUiAction.Sell, company, amount, userInterface);
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is StockTradingUiState cast)
            _fragment?.UpdateState(cast);
    }

    private static void SendStockTradingUiMessage(
        StockTradingUiAction action,
        int company,
        int amount,
        BoundUserInterface userInterface)
    {
        var stockMessage = new StockTradingUiMessageEvent(action, company, amount);
        userInterface.SendMessage(new CartridgeUiMessage(stockMessage));
    }
}
