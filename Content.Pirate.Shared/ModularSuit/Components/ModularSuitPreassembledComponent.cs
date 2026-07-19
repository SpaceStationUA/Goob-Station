using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class ModularSuitPreassembledComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Modules = new();
}
