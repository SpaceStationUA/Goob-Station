// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Shuttles.Warp;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.Shuttles.Warp;

public sealed class WarpDriveBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private WarpDriveWindow? _window;

    public WarpDriveBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<WarpDriveWindow>();
        _window.UpdateWindow(this);
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not WarpDriveBuiState driveState)
            return;

        _window?.UpdateState(driveState);
    }

    public void Toggle()
    {
        SendMessage(new WarpDriveToggleMessage());
    }

    public void Jump()
    {
        SendMessage(new WarpDriveJumpMessage());
    }

    public void SetCourse(string sector)
    {
        SendMessage(new WarpDriveSetCourseMessage(sector));
    }
}
