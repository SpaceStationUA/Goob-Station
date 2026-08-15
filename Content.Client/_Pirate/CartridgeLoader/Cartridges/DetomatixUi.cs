// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared._Pirate.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.CartridgeLoader.Cartridges;

public sealed partial class DetomatixUi : UIFragment
{
    private DetomatixUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new DetomatixUiFragment();

        _fragment.OnDetonatePressed += number =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new DetomatixUiMessageEvent(number)));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DetomatixUiState cast)
            _fragment?.UpdateState(cast);
    }
}
