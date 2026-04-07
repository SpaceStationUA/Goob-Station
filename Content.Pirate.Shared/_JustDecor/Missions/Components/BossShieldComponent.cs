namespace Content.Pirate.Shared._JustDecor.Missions.Components;

[RegisterComponent]
public sealed partial class BossShieldComponent : Component
{
    [DataField]
    public float ShieldHp = 200f;

    [DataField]
    public float MaxShieldHp = 200f;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public bool ResetOnEnable = true;
}
