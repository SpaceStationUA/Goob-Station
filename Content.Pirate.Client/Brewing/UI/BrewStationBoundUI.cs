using System;
using Content.Pirate.Shared.Brewing;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

namespace Content.Pirate.Client.Brewing.UI;

[UsedImplicitly]
public sealed class BrewStationBoundUI : BoundUserInterface
{
    [ViewVariables]
    private BrewStationWindow? _window;

    public BrewStationBoundUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new BrewStationWindow();
        _window.OnClose += Close;
        _window.StartPressed += () => SendMessage(new BrewStationStartMixMessage());

        if (State is BrewStationBoundUserInterfaceState state)
            _window.UpdateState(state);

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not BrewStationBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Close();
        _window = null;
    }
}
