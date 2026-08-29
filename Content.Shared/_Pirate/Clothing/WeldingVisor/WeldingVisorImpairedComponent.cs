// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>
/// Pirate: welding visor - added to a mob while it's wearing at least one welding visor (of any position),
/// removed again once the last one is taken off. <see cref="Sources"/> tracks which of those are actually
/// lowered right now; the vision-obstruction overlay only draws while it's non-empty. Toggling a visor only
/// ever mutates this set - the component itself is added/removed exclusively on equip/unequip - so raising a
/// visor isn't treated any differently from lowering one by client-side prediction.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(WeldingVisorSystem))]
public sealed partial class WeldingVisorImpairedComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Sources = new();
}
