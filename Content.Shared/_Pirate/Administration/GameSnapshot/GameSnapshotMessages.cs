// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eui;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Administration.GameSnapshot;

public sealed record GameSnapshotFrame(ushort Width, ushort Height, byte[] Pixels)
{
    public const int MaxWidth = 1280;
    public const int MaxHeight = 720;
    public const int ChunkBytes = 32 * 1024;

    public static bool ValidSize(int width, int height, int total)
        => width is > 0 and <= MaxWidth && height is > 0 and <= MaxHeight
            && total > 0 && total <= width * height * 2 + 1024;

    public static bool ValidChunk(int width, int height, int total, int offset, int length)
        => ValidSize(width, height, total) && offset >= 0 && offset < total && offset % ChunkBytes == 0
            && length == Math.Min(ChunkBytes, total - offset);
}

public sealed class MsgRequestGameSnapshot : NetMessage
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    public override MsgGroups MsgGroup => MsgGroups.Command;
    public uint RequestId;
    public int Offset;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        RequestId = buffer.ReadUInt32();
        Offset = buffer.ReadInt32();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(RequestId);
        buffer.Write(Offset);
    }
}

public sealed class MsgGameSnapshotReply : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public uint RequestId;
    public ushort Width;
    public ushort Height;
    public int TotalBytes;
    public int Offset;
    public ReadOnlyMemory<byte> Pixels;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        RequestId = buffer.ReadUInt32();
        Width = buffer.ReadUInt16();
        Height = buffer.ReadUInt16();
        TotalBytes = buffer.ReadInt32();
        Offset = buffer.ReadInt32();
        var length = buffer.LengthBytes - buffer.PositionInBytes;
        if (GameSnapshotFrame.ValidChunk(Width, Height, TotalBytes, Offset, length))
            Pixels = new ReadOnlyMemory<byte>(buffer.PeekDataBuffer(), buffer.PositionInBytes, length);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(RequestId);
        buffer.Write(Width);
        buffer.Write(Height);
        buffer.Write(TotalBytes);
        buffer.Write(Offset);
        buffer.Write(Pixels.Span);
    }
}

[Serializable, NetSerializable]
public sealed class RefreshGameSnapshot : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class ContinueGameSnapshot(uint requestId, int offset) : EuiMessageBase
{
    public readonly uint RequestId = requestId;
    public readonly int Offset = offset;
}

[Serializable, NetSerializable]
public enum GameSnapshotStatus : byte
{
    Waiting,
    Received,
    Busy,
    Unavailable,
    Offline,
    TimedOut,
}

[Serializable, NetSerializable]
public sealed class GameSnapshotState(string username, GameSnapshotStatus status) : EuiStateBase
{
    public readonly string Username = username;
    public readonly GameSnapshotStatus Status = status;
    public uint RequestId;
    public ushort Width;
    public ushort Height;
    public int TotalBytes;
    public int Offset;
    public byte[]? Pixels;
}
