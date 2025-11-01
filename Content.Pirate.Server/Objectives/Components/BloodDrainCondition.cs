using Content.Pirate.Server.Vampire;
using Content.Server.Objectives.Systems;

namespace Content.Pirate.Server.Objectives.Components;

[RegisterComponent, Access(typeof(VampireSystem))]
public sealed partial class BloodDrainConditionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BloodDranked = 0f;
}
