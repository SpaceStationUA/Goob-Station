// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Billiards;

[Serializable, NetSerializable]
public enum BilliardTableUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class BilliardTableRackMessage(BilliardGameType gameType) : BoundUserInterfaceMessage
{
    public BilliardGameType GameType { get; } = gameType;
}

[Serializable, NetSerializable]
public sealed class BilliardTableOpenStorageMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class BilliardTableBuiState(
    int ballCount,
    bool surfaceClear,
    bool anchored) : BoundUserInterfaceState
{
    public int BallCount { get; } = ballCount;
    public int RequiredBallCount { get; } = BilliardTableComponent.RequiredBallCount;
    public bool SurfaceClear { get; } = surfaceClear;
    public bool Anchored { get; } = anchored;
}
