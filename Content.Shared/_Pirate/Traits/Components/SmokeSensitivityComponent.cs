using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Traits.Components;

/// <summary>
///     This component is used for the Smoke Sensitivity trait.
///     The entity will cough when water vapor is present.
/// </summary>
[RegisterComponent]
public sealed partial class SmokeSensitivityComponent : Component
{
    /// <summary>
    ///     The time between coughs.
    /// </summary>
    [DataField("coughInterval")]
    public float CoughInterval = 3f;

    /// <summary>
    ///     The remaining time until the next cough.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float Accumulator;
}
