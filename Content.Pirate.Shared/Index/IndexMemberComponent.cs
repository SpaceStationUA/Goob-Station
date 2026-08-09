// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Index;

/// <summary>
///     Marks an entity as an Index member. Grants membership when someone claims a pager.
///     Tracks the member's KARMIC CONSEQUENCE count and the pager bound to them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IndexMemberComponent : Component
{
    /// <summary>
    ///     KARMIC CONSEQUENCE count. At 10+, weapons have a 10% chance to shift into a fpoon.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int KarmicConsequence;

    /// <summary>
    ///     The pager bound to this member (the one they claimed).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Pager;

    /// <summary>
    ///     Admin-set flag: the next Caduceus transformation is guaranteed to be a fpoon.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool NextWeaponFpoon;
}
