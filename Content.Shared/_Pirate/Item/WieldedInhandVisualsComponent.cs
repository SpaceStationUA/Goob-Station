using Content.Shared.Hands.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Item;

[RegisterComponent, NetworkedComponent]
[Access(typeof(Content.Shared.Wieldable.SharedWieldableSystem))]
public sealed partial class WieldedInhandVisualsComponent : Component
{
    [DataField("wieldedInhandVisuals")]
    [Access(typeof(Content.Shared.Wieldable.SharedWieldableSystem), Other = AccessPermissions.ReadExecute)]
    public Dictionary<HandLocation, List<PrototypeLayerData>> WieldedInhandVisuals = new();

}
