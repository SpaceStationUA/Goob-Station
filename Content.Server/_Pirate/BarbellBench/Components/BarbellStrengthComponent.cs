namespace Content.Server._Pirate.BarbellBench.Components;

/// <summary>
/// Stores barbell training progress independently from PullerComponent.
/// PullerComponent may be removed and recreated by the movement system.
/// </summary>
[RegisterComponent]
public sealed partial class BarbellStrengthComponent : Component
{
    public float Progress;
    public bool StaminaBonusApplied;
}
