using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared._JustDecor.Weapons.Melee;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SamuraiCounterComponent : Component
{
    [DataField]
    public TimeSpan IdleThreshold = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public bool Armed;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan LastMoveTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextAutoReady;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextManualReady;

    [DataField]
    public TimeSpan AutoCooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan ManualCooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan AutoReflectDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan ManualReflectDuration = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public float BaseReflectProb = 0.4f;
}
