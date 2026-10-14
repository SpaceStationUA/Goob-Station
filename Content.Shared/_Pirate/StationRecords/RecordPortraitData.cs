// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;

[Serializable, NetSerializable]
public sealed class RecordPortraitData
{
    public byte[]? ImageData;

    public bool IsRender;

    public HumanoidCharacterProfile? Snapshot;

    public int Number;

    public string? Name;

    public int BirthYear;
}
