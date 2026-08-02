// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Mind.Components;

// Pirate: multiz - was [NetworkedComponent, AutoGenerateComponentState] with [AutoNetworkedField] LastMob.
// LastMob intentionally keeps pointing at a mob even after it is deleted (round-end "what you played as",
// see GameTicker.RoundFlow which guards with TerminatingOrDeleted). It is only ever read server-side, but
// the generated network state called GetNetEntity on the dangling EntityUid, crashing PvsSystem state
// serialization whenever the mind was force-included in a client's PVS (e.g. multiz probe-eye session
// overrides pulling a mind whose last mob was gibbed). Made server-only to remove the dangling network ref.
[RegisterComponent]
public sealed partial class MindLastMobComponent : Component
{
    /// <summary>
    /// The last mob entity this mind was in.
    /// Can be null.
    /// </summary>
    [DataField]
    public EntityUid? LastMob { get; set; }
}
