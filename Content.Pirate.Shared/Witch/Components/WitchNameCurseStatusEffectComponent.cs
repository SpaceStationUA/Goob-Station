namespace Content.Pirate.Shared.Witch.Components;

[RegisterComponent]
public sealed partial class WitchNameCurseStatusEffectComponent : Component
{
    [DataField]
    public float Range = 7f;

    [DataField]
    public string? OverrideName;
}
