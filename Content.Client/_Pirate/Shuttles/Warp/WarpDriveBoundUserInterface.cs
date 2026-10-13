// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Shuttles.Warp;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.Shuttles.Warp;

/// <summary>
///     The warp drive console.
/// </summary>
/// <remarks>
///     Deliberately a plain machine window rather than anything web-backed. This is the surface a
///     crew is looking at when the star is going nova, so it has to stay cheap and legible; the
///     sector chart that picks a destination is a separate, advisory window that only ever sends
///     a course back to the server.
/// </remarks>
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

    /// <summary>
    ///     Spools the drive up, or takes it down if it is already running.
    /// </summary>
    public void Toggle()
    {
        SendMessage(new WarpDriveToggleMessage());
    }
}