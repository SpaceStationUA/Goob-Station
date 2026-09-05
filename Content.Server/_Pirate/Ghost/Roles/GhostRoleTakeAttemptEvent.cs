// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Player;

namespace Content.Server._Pirate.Ghost.Roles;

[ByRefEvent]
public record struct GhostRoleTakeAttemptEvent(ICommonSession Player)
{
    public bool Cancelled { get; set; }
}
