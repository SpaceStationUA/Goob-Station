namespace Content.Shared._Pirate.Movement.Components;

/// <summary>
/// Stores strength-training progress independently from PullerComponent.
/// </summary>
[RegisterComponent]
public sealed partial class PullStrengthComponent : Component
{
    [DataField]
    public float StrengthGain = 0.02f;

    [DataField]
    public float LowStrengthThreshold = 0.4f;

    [DataField]
    public float MediumStrengthThreshold = 0.7f;

    [DataField]
    public float HighStrengthThreshold = 1f;

    [DataField]
    public float LowDensityReduction = 0.4f;

    [DataField]
    public float MediumDensityReduction = 0.7f;

    [DataField]
    public float HighDensityReduction = 1f;

    [DataField]
    public float StaminaBonus = 25f;

    public float Progress;
    public bool StaminaBonusApplied;

    public float DensityReduction => Progress >= HighStrengthThreshold
        ? HighDensityReduction
        : Progress >= MediumStrengthThreshold
            ? MediumDensityReduction
            : Progress >= LowStrengthThreshold
                ? LowDensityReduction
                : 0f;
}
