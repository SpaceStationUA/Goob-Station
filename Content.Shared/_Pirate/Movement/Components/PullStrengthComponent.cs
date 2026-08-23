namespace Content.Shared._Pirate.Movement.Components;

/// <summary>
/// Stores strength-training progress independently from PullerComponent.
/// </summary>
[RegisterComponent]
public sealed partial class PullStrengthComponent : Component
{
    public float Progress;
    public bool StaminaBonusApplied;

    public float DensityReduction => Progress switch
    {
        >= 1f => 1f,
        >= 0.7f => 0.7f,
        >= 0.4f => 0.4f,
        _ => 0f
    };
}
