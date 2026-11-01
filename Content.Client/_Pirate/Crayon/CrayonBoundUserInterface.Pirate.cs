// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using Content.Shared._Pirate.Crayon;

namespace Content.Client.Crayon.UI;

public sealed partial class CrayonBoundUserInterface
{
    private void InitializePirateArt()
    {
        if (_menu == null)
            return;

        _menu.SetCrayon(Owner);
        _menu.OnCustomSelected += SelectCustom;
        _menu.OnRotationSelected += degrees => SendPredictedMessage(new CrayonRotationMessage(degrees));
    }

    private void SelectCustom(CrayonSymbol symbol)
    {
        SendMessage(new CrayonSelectSymbolMessage(symbol));
    }
}
