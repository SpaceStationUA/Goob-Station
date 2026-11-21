// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Server.Systems;

[RegisterComponent]
public sealed partial class GhostPossessedComponent : Component
{
    public EntityUid Spirit;
    public bool? RoleWasTaken;
    public bool RoleWasListed;
}
