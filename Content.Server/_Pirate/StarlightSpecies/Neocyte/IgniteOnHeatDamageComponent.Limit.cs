namespace Content.Server.Damage.Components;

public sealed partial class IgniteOnHeatDamageComponent
{
    // Pirate: Neo-Diona caps ignition caused by heat damage, independently of other fire sources.
    [DataField]
    public float MaxFireStacks = float.MaxValue;
}
