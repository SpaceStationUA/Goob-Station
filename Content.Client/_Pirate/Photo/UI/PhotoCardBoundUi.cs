// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Photo;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.Photo.UI;

public sealed class PhotoCardBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PhotoCardWindow? _window;

    public PhotoCardBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PhotoCardWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not PhotoCardUiState cast)
            return;

        _window.SetMetadata(cast.CustomName, cast.Caption);

        if (cast.ImageData == null)
        {
            _window.ClearImage();
            return;
        }

        _window.ShowImage(cast.ImageData);
    }

}


