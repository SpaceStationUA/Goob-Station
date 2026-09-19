using Content.Shared.Turrets;

namespace Content.Shared._Pirate.Turrets;

/// <summary>Sets a mapped deployable turret's initial armament state.</summary>
[RegisterComponent]
public sealed partial class TurretStartupStateComponent : Component
{
    /// <summary>-1 retracts the turret; non-negative values select a fire mode.</summary>
    [DataField]
    public int ArmamentState = 1;
}
