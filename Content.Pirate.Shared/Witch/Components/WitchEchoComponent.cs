using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Witch.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class WitchEchoComponent : Component
{
    [DataField]
    public int Repeats = 3;

    [DataField]
    public float DelaySeconds = 0.6f;
}
