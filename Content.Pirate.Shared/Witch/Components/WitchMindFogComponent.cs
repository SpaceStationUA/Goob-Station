using System;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Pirate.Shared.Witch.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WitchMindFogComponent : Component
{
    [DataField, AutoNetworkedField]
    public Direction UpDirection = Direction.North;

    [DataField, AutoNetworkedField]
    public Direction DownDirection = Direction.South;

    [DataField, AutoNetworkedField]
    public Direction LeftDirection = Direction.West;

    [DataField, AutoNetworkedField]
    public Direction RightDirection = Direction.East;

    [DataField]
    public float ShuffleInterval = 0.85f;

    [DataField]
    public TimeSpan NextShuffle;
}
