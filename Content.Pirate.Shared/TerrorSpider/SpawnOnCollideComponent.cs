using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.TerrorSpider;

[RegisterComponent]
public sealed partial class SpawnOnCollideComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype = string.Empty;

    [DataField]
    public bool Collided;
}
