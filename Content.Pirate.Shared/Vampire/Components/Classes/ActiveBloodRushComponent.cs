using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Vampire.Components.Classes;

/// <summary>
/// Marker component indicating Blood Rush is currently active
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveBloodRushComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.5f;
}
