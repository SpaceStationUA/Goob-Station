// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;
[Serializable, NetSerializable, DataRecord]
public sealed partial record RecordBackgroundEntry
{
    public const int MaxTitleLength = 64;
    public const int MaxAuthorLength = 64;
    public const int MaxBodyLength = 2048;
    public const int MaxPerBook = 10;

    [DataField]
    public RecordLogBook Book;

    [DataField]
    public string Title = string.Empty;

    [DataField]
    public string Author = string.Empty;

    [DataField]
    public string Body = string.Empty;
}
