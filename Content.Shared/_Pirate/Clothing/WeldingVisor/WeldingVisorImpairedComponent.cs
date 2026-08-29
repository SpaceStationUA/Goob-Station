// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>Pirate: welding visor - tracks worn and lowered visors for the overlay.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(WeldingVisorSystem))]
public sealed partial class WeldingVisorImpairedComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Sources = new();

    /// <summary>Tracks worn visors independently of lowered sources.</summary>
    public HashSet<EntityUid> WornVisors = new();
}
