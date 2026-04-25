using System;

namespace Content.Pirate.Shared.Witch.Components;

[RegisterComponent]
public sealed partial class WitchSubstitutionStatusEffectComponent : Component
{
    [DataField]
    public float Range = 6f;

    [DataField]
    public float FallbackDamage = 5f;

    [DataField]
    public float Interval = 1.5f;

    [DataField]
    public TimeSpan NextAttempt;
}
