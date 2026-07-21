namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class ModularSuitModuleContainerRequirementComponent : Component
{
    [DataField(required: true)]
    public string RequiredContainerId = string.Empty;

    [DataField]
    public LocId FailureMessage = "modsuit-module-requires-item";
}
