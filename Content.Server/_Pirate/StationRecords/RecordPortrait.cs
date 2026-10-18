// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Preferences;

namespace Content.Server._Pirate.StationRecords;

// A separate entry keeps portraits shared across console record types.
public sealed class RecordPortrait
{
    public HumanoidCharacterProfile? ProfileSnapshot;

    public byte[]? ImageData;

    public byte[]? PreviewData;

    public bool IsRender;

    public int Number;

    public string? Name;

    public int BirthYear;
}
