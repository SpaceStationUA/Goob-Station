using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared._JustDecor.Weapons.Melee;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BladeModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public float Radius = 5f;

    [DataField, AutoNetworkedField]
    public float WalkSpeedMultiplier = 0.6f;

    [DataField, AutoNetworkedField]
    public float SprintSpeedMultiplier = 0.6f;

    [DataField]
    public TimeSpan SlowdownDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan PulseInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextPulse;

    [DataField]
    public EntityUid? User;

    [DataField]
    public EntityUid? ToggleAction;
}
