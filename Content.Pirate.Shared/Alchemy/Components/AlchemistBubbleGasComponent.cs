using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Alchemy.Components;

[RegisterComponent]
public sealed partial class AlchemistBubbleGasComponent : Component
{
    [DataField]
    public float MinInterval = 0.8f;

    [DataField]
    public float MaxInterval = 1.6f;

    [DataField]
    public float JumpSpeed = 4.5f;

    [DataField]
    public EntProtoId JumpEffect = "EffectEmpPulse";

    public TimeSpan NextAction;
}
