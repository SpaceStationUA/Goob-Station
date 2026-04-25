using System;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Alchemy.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class GravityPotionComponent : Component
{
    [DataField]
    public float Radius = 4.5f;

    [DataField]
    public float PullStrength = 5f;

    [DataField]
    public float Interval = 0.7f;

    public TimeSpan NextUpdate;
}
