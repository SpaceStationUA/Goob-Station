// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;


[Serializable, NetSerializable]
public sealed class RecordPortraitPrintMessage : BoundUserInterfaceMessage
{
    public readonly uint RecordKey;
    public readonly byte[]? ImageData;

    public RecordPortraitPrintMessage(uint recordKey, byte[]? imageData)
    {
        RecordKey = recordKey;
        ImageData = imageData == null ? null : (byte[]) imageData.Clone();
    }
}

[Serializable, NetSerializable]
public sealed class RecordPortraitUploadMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RecordPortraitRenderRequestEvent : EntityEventArgs
{
    public NetEntity Station;
    public uint RecordId;
    public HumanoidCharacterProfile Profile = default!;
    public string? JobPrototype;
}

[Serializable, NetSerializable]
public sealed class RecordPortraitRenderResultEvent : EntityEventArgs
{
    public NetEntity Station;
    public uint RecordId;
    public byte[] ImageData = [];
}

[Serializable, NetSerializable]
public sealed class RecordPortraitStoreGeneratedMessage : BoundUserInterfaceMessage
{
    public readonly uint RecordKey;
    public readonly byte[] ImageData;

    public RecordPortraitStoreGeneratedMessage(uint recordKey, byte[] imageData)
    {
        RecordKey = recordKey;
        ImageData = (byte[]) imageData.Clone();
    }
}
