namespace Content.Pirate.Shared._JustDecor.Missions.Components;

[RegisterComponent]
public sealed partial class BossRegenerationComponent : Component
{
    [DataField]
    public float HealPerSecond = 5f;

    [DataField]
    public float TickInterval = 1f;

    [DataField]
    public float Accumulator = 0f;
}
