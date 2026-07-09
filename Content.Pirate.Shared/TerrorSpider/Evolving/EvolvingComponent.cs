using Content.Pirate.Shared.TerrorSpider.Evolving.Conditions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.TerrorSpider.Evolving;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class EvolvingComponent : Component
{
    [DataField(required: true)]
    public EntProtoId EvolveTo;

    [DataField]
    public EntProtoId EvolveActionId = "ActionTerrorSpiderEvolve";

    [DataField, AutoNetworkedField]
    public EntityUid? EvolveActionEntity;

    [DataField(serverOnly: true)]
    public List<EvolvingCondition> Conditions = [];

    [DataField]
    public string ObjectiveId = "EvolveObjective";

    [DataField]
    public List<EntityUid> Objectives = [];
}

public enum EvolveType
{
    EggsInjected,
    SpiderWebsSpawned,
    DamageDeal
}
