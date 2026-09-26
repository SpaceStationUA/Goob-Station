using Content.Pirate.Shared.Shipyard;
using Content.Pirate.Shared.Shipyard.Prototypes;
using Content.Shared.Access.Systems;
using Content.Shared.Whitelist;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Shipyard;

public sealed class ShipyardConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly AccessReaderSystem _access;
    private readonly EntityWhitelistSystem _whitelist;
    private ShipyardConsoleMenu? _menu;

    public ShipyardConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _access = EntMan.System<AccessReaderSystem>();
        _whitelist = EntMan.System<EntityWhitelistSystem>();
    }

    protected override void Open()
    {
        base.Open();
        if (_menu is null)
        {
            _menu = new ShipyardConsoleMenu(Owner, _proto, EntMan, _player, _access, _whitelist);
            _menu.OnClose += Close;
            _menu.OnPurchased += Purchase;
        }

        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ShipyardConsoleState shipyardState)
            _menu?.UpdateState(shipyardState);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _menu is { } menu)
        {
            menu.OnClose -= Close;
            menu.OnPurchased -= Purchase;
            menu.Close();
            _menu = null;
        }

        base.Dispose(disposing);
    }

    private void Purchase(string id)
    {
        SendMessage(new ShipyardConsolePurchaseMessage(id));
    }
}
