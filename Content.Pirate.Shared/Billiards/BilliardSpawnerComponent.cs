// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Billiards;

[RegisterComponent, NetworkedComponent]
public sealed partial class BilliardSpawnerComponent : Component
{
    [DataField]
    public EntProtoId BallPrototype = "BilliardBall";

    [DataField]
    public float BallSpacing = 0.13f;

    [DataField]
    public int Rows = 5;

    [DataField]
    public BilliardGameType GameType = BilliardGameType.Pyramid;
}
