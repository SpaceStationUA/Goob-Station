namespace Content.Pirate.Shared._JustDecor.Missions.Components;

[RegisterComponent]
public sealed partial class MissionBossComponent : Component
{
    [DataField]
    public int ShieldPhase = 1;

    [DataField]
    public int ReinforcementPhase = 2;

    [DataField]
    public int BerserkPhase = 3;

    [DataField]
    public float BerserkSpeedMultiplier = 1.25f;

    [DataField]
    public float BerserkDamageMultiplier = 1.25f;

    [DataField]
    public int LastProcessedPhase = 0;

    [DataField]
    public bool BerserkActive;
}
