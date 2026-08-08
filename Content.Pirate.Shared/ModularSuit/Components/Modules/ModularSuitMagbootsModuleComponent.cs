using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class ModularSuitMagbootsModuleComponent : Component
{
    [DataField]
    public string TargetSlot = "shoes";

    [DataField(required: true)]
    public ComponentRegistry? ActiveComponents { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Enabled;
}
