using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PsionicFlashPowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionPsionicFlash";

    public override string PowerName { get; set; } = "psionic-power-name-psionic-flash";

    public override string? PowerInitFeedback { get; set; } = "psionic-flash-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "psionic-flash-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 5;

    public override int MaxGlimmerChanged { get; set; } = 8;

    /// <summary>
    /// The radius in which entities will be flashed.
    /// </summary>
    [DataField]
    public float Range = 5f;

    /// <summary>
    /// The duration of the flash effect.
    /// </summary>
    [DataField]
    public float FlashDuration = 8f;

    /// <summary>
    /// Movement speed modifier applied to flashed targets (0-1, lower = slower).
    /// </summary>
    [DataField]
    public float SlowTo = 0.5f;

    /// <summary>
    /// The sound to play when the ability is used.
    /// </summary>
    [DataField]
    public SoundSpecifier AbilitySound = new SoundPathSpecifier("/Audio/Weapons/flash.ogg");

    /// <summary>
    /// The effect to spawn when the ability is used.
    /// </summary>
    [DataField]
    public EntProtoId Effect = "EffectPyrokineticFlare";
}
