// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Shuttles.Warp;

/// <summary>
///     A warp drive's presence on a grid, as read by the FTL range check.
/// </summary>
/// <remarks>
///     It sits on the grid because <c>SharedShuttleSystem.GetFTLRange</c> resolves against the grid, and it
///     is networked so the shuttle map can draw the reachable radius. It is kept apart from
///     <c>FTLDriveComponent</c>: that data is rewritten whenever a bluespace drive is powered or unpowered,
///     and warp range would race with it.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarpDriveGridComponent : Component
{
    /// <summary>
    ///     Whether a warp drive on this grid is engaged and granting range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    ///     The FTL radius granted while <see cref="Active"/>, in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range;
}
