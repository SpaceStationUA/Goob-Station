using Robust.Shared.GameStates;

using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Witch.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WitchRageComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<NpcFactionPrototype> Faction = "SimpleHostile";
}
