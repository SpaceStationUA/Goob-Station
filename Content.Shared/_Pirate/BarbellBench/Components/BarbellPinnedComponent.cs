using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.BarbellBench.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BarbellPinnedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Bench;

    [DataField, AutoNetworkedField]
    public double PinDurationSeconds = 27.0;

    public TimeSpan PinnedAt;
}
