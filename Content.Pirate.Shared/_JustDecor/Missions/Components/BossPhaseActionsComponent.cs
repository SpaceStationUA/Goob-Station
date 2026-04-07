using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared._JustDecor.Missions.Components;

[RegisterComponent]
public sealed partial class BossPhaseActionsComponent : Component
{
    [DataField]
    public List<EntProtoId> ReinforcementPrototypes = new();

    [DataField]
    public int ReinforcementCount = 0;

    [DataField]
    public List<EntProtoId> TurretPrototypes = new();

    [DataField]
    public int TurretCount = 0;

    [DataField]
    public List<EntProtoId> BerserkReinforcements = new();

    [DataField]
    public int BerserkReinforcementCount = 0;

    [DataField]
    public List<EntProtoId> BerserkTurrets = new();

    [DataField]
    public int BerserkTurretCount = 0;

    [DataField]
    public float SpawnRadius = 3f;
}
