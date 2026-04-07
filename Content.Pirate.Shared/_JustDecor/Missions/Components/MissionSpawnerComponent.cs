using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared._JustDecor.Missions.Components;

[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class MissionSpawnerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public List<EntProtoId> Prototypes { get; set; } = new();

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public List<EntProtoId> GameRules { get; set; } = new();

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float Chance { get; set; } = 1.0f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public bool SpawnAll { get; set; } = false;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float Offset { get; set; } = 0f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public bool DeleteAfterSpawn { get; set; } = false;

    [DataField]
    public bool Spawned = false;
}
