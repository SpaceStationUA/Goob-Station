// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Billiards;

[RegisterComponent, NetworkedComponent]
public sealed partial class BilliardTableComponent : Component
{
    public const int RequiredBallCount = 16;

    [DataField]
    public float BallSpacing = 0.164f;
}
