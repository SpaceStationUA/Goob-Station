using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class ModularSuitActionModuleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action;
}
