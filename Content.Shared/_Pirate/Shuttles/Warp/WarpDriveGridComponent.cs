// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Shuttles.Warp;

/// <summary>
///     The warp drive's presence on a grid, as seen by the FTL range check.
/// </summary>
/// <remarks>
///     This lives on the grid rather than on the machine because the thing that actually needs
///     to know the range is <c>SharedShuttleSystem.GetFTLRange</c>, which resolves against the
///     grid, not against a machine bolted to it. It is networked because the shuttle map draws
///     the FTL radius client-side, and a crew who cannot see the reachable bubble cannot plan a
///     jump.
///
///     Deliberately separate from <c>FTLDriveComponent</c> (bluespace). Bluespace range is a
///     passive property of the hull's drives and stacks by priority; warp range is a live
///     consequence of a machine being engaged right now. Writing warp range into the bluespace
///     data would make the two indistinguishable and would race
///     <c>FTLDriveSystem.UpdateFtlDrives</c>, which rewrites that data whenever a bluespace
///     drive is powered or unpowered.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarpDriveGridComponent : Component
{
    /// <summary>
    ///     Whether a warp drive on this grid is currently engaged, and therefore granting range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    ///     The FTL radius granted while <see cref="Active"/>, in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range;
}