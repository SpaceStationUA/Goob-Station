namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class ModularSuitStorageModuleComponent : Component
{
    [DataField]
    public string ContainerId = "storagebase";
}
