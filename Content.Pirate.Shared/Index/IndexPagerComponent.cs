// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Index;

/// <summary>
///     The Index pager. Using an unbound pager makes the user an <see cref="IndexMemberComponent"/>,
///     binding the pager to them. Prescriptions sent by the Index administration show up in its UI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IndexPagerComponent : Component
{
    /// <summary>
    ///     The Index member this pager is bound to, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Member;

    /// <summary>
    ///     Whether this pager has been claimed by a member.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Bound;

    /// <summary>
    ///     Prescriptions (messages) received from the Index administration. Only the latest one is
    ///     kept - a new prescription replaces the previous. Server-only data, forwarded to the UI
    ///     through <see cref="IndexPagerBoundUserInterfaceState"/>.
    /// </summary>
    [DataField]
    public List<string> Prescriptions = new();
}
