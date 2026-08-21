using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.BarbellBench.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BarbellPinnedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Bench;

    public TimeSpan PinnedAt;
}
