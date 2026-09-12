namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent]
public sealed partial class ModularSuitEmpShieldModuleComponent : Component
{
    [ViewVariables]
    public EntityUid? Wearer;
}

[RegisterComponent]
public sealed partial class ModularSuitEmpShieldedComponent : Component
{
    [ViewVariables]
    public EntityUid Suit;

    [ViewVariables]
    public EntityUid Module;

    [ViewVariables]
    public uint LastEmpTick = uint.MaxValue;

    [ViewVariables]
    public bool BlockedLastEmp;
}
