using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TelekineticPulsePowerComponent : BasePsionicPowerComponent
{
    public override EntProtoId ActionProtoId { get; set; } = "ActionTelekineticPulse";

    public override string PowerName { get; set; } = "psionic-power-name-telekinetic-pulse";

    public override string? PowerInitFeedback { get; set; } = "telekinetic-pulse-power-initialization-feedback";

    public override string? PowerMetapsionicFeedback { get; set; } = "telekinetic-pulse-power-metapsionic-feedback";

    public override int MinGlimmerChanged { get; set; } = 6;

    public override int MaxGlimmerChanged { get; set; } = 8;

    /// <summary>
    /// The radius in which entities will be pushed away.
    /// </summary>
    [DataField]
    public float Radius = 3f;

    /// <summary>
    /// The force applied to push entities away.
    /// </summary>
    [DataField]
    public float PushStrength = 5f;

    /// <summary>
    /// The sound to play when the ability is used.
    /// </summary>
    [DataField]
    public SoundSpecifier AbilitySound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");

    /// <summary>
    /// The effect to spawn when the ability is used.
    /// </summary>
    [DataField]
    public EntProtoId Effect = "EffectFlashTelekineticPulse";
}
