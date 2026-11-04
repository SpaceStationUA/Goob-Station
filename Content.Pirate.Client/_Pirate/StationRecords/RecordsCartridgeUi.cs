// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Fragments;
using Content.Pirate.Shared._Pirate.StationRecords;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client._Pirate.StationRecords;

public sealed partial class RecordsCartridgeUi : UIFragment
{
    private RecordsCartridgeUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new RecordsCartridgeUiFragment();
        _fragment.OnMessage += (tab, message) =>
            userInterface.SendMessage(new CartridgeUiMessage(new RecordsCartridgeUiMessageEvent(tab, message)));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is RecordsCartridgeUiState recordsState)
            _fragment?.UpdateState(recordsState);
    }
}
