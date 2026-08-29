// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>
/// Pirate: welding visor - added by the server to a mob while it's wearing at least one welding visor,
/// removed again once the last one is taken off. <see cref="Sources"/> is networked and tracks which visors
/// are lowered right now; the vision-obstruction overlay only draws while it's non-empty.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(WeldingVisorSystem))]
public sealed partial class WeldingVisorImpairedComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Sources = new();

    /// <summary>
    /// Server-side lifetime tracking. This is separate from <see cref="Sources"/> because raised visors still
    /// keep this component alive, even though they do not obstruct vision.
    /// </summary>
    public HashSet<EntityUid> WornVisors = new();
}
