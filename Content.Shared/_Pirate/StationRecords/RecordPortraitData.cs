// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CriminalRecords;
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

    public static RecordPortraitData FromRecord(CriminalRecord record)
    {
        return new RecordPortraitData
        {
            ImageData = record.PortraitImageData,
            IsRender = record.PortraitIsRender,
            Snapshot = record.PortraitProfileSnapshot,
            Number = record.PortraitNumber,
            Name = record.PortraitName,
            BirthYear = record.PortraitBirthYear,
        };
    }
}
