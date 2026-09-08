// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.ListeningPost.DropConsole;

[Serializable, NetSerializable]
public enum SyndicateDropConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum SyndicateDropMode : byte
{
    Automatic,

    Manual,

    Pod,
}

[Serializable, NetSerializable]
public readonly record struct SyndicateDropRecord(
    NetEntity Grid,
    Vector2i Tile,
    Vector2i MapPosition,
    int Price,
    SyndicateDropMode Mode,
    TimeSpan Time);

[Serializable, NetSerializable]
public sealed class SyndicateDropConsoleUiState : BoundUserInterfaceState
{
    public bool Manual;

    public TimeSpan NextDrop;

    public NetEntity? TargetGrid;

    public NetEntity? SelectedGrid;
    public Vector2i? SelectedTile;

    public int Charges;

    public int MaxCharges;

    public List<SyndicateDropRecord> DropHistory;

    public bool PadLinked;

    public TimeSpan PodCooldownEnd;

    public SyndicateDropConsoleUiState(
        bool manual,
        TimeSpan nextDrop,
        NetEntity? targetGrid,
        NetEntity? selectedGrid,
        Vector2i? selectedTile,
        int charges,
        int maxCharges,
        List<SyndicateDropRecord> dropHistory,
        bool padLinked,
        TimeSpan podCooldownEnd)
    {
        Manual = manual;
        NextDrop = nextDrop;
        TargetGrid = targetGrid;
        SelectedGrid = selectedGrid;
        SelectedTile = selectedTile;
        Charges = charges;
        MaxCharges = maxCharges;
        DropHistory = dropHistory;
        PadLinked = padLinked;
        PodCooldownEnd = podCooldownEnd;
    }
}

[Serializable, NetSerializable]
public sealed class SyndicateDropConsoleSetModeMessage : BoundUserInterfaceMessage
{
    public readonly bool Manual;

    public SyndicateDropConsoleSetModeMessage(bool manual)
    {
        Manual = manual;
    }
}

[Serializable, NetSerializable]
public sealed class SyndicateDropConsoleSelectTileMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Grid;
    public readonly Vector2 LocalPosition;

    public SyndicateDropConsoleSelectTileMessage(NetEntity grid, Vector2 localPosition)
    {
        Grid = grid;
        LocalPosition = localPosition;
    }
}

[Serializable, NetSerializable]
public sealed class SyndicateDropConsoleNudgeTargetMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity DisplayedGrid;
    public readonly Vector2i Delta;

    public SyndicateDropConsoleNudgeTargetMessage(NetEntity displayedGrid, Vector2i delta)
    {
        DisplayedGrid = displayedGrid;
        Delta = delta;
    }
}

[Serializable, NetSerializable]
public sealed class SyndicateDropConsoleClearTargetMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SyndicateDropConsoleLaunchMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SyndicateDropConsolePodSendMessage : BoundUserInterfaceMessage;
