using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class SpeciesPrototype
{
    [DataField("clothingSpeciesFallback")]
    public Dictionary<string, ProtoId<SpeciesPrototype>> ClothingSpeciesFallback { get; private set; } = new();
}
