// SPDX-License-Identifier: MIT

using Robust.Shared.Player;

namespace Content.Server.Ghost.Roles.Components;

[ByRefEvent]
public record struct TakeGhostRoleEvent(ICommonSession Player)
{
    /// <summary>
    /// Pirate: Prevents role-specific takeover handlers from creating the role.
    /// </summary>
    public bool Cancelled { get; set; } // Pirate: allow role-specific validation immediately before takeover.

    public bool TookRole { get; set; }
}
