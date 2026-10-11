using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.SpawnItemOnGib;

[RegisterComponent]
public sealed partial class SpawnItemsOnGibComponent : Component
{
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> ItemsToSpawn = new();
}
