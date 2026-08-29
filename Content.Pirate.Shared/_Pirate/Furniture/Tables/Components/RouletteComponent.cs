using System;
using System.Threading;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Furniture.Tables.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RouletteComponent : Component
{
    [DataField, AutoNetworkedField]
    public RouletteState State = RouletteState.Idle;

    [DataField, AutoNetworkedField]
    public int Result;

    [ViewVariables]
    public CancellationTokenSource? CancellationTokenSource;
}

[Serializable, NetSerializable]
public enum RouletteState : byte
{
    Idle,
    Rolling,
    Result
}

[Serializable, NetSerializable]
public enum RouletteVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum RouletteVisualLayers : byte
{
    Base
}
