namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class ModularSuitSpringlockModuleComponent : Component;

[RegisterComponent]
public sealed partial class ModularSuitSpringlockInstalledComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Module = default!;
}
