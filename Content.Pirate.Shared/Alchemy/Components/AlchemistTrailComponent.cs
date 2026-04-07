using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Alchemy.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlchemistTrailComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? TrailStatusEffect;

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> TrailReagent = "Water";

    [DataField, AutoNetworkedField]
    public float SpillQuantity = 2f;
}
