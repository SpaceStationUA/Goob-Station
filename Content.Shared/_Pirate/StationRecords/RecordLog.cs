// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;

[Serializable, NetSerializable]
public enum RecordLogKind : byte
{
    StatusChange,

    Note,

    Background,
}

[Serializable, NetSerializable]
public enum RecordLogBook : byte
{
    Security,
    Psionics,
    Crew,
}

[Serializable, NetSerializable, DataRecord]
public sealed partial record RecordLogEntry
{
    [DataField]
    public uint Id;

    [DataField]
    public RecordLogKind Kind;

    [DataField]
    public string? Topic;

    [DataField]
    public string? Status;

    [DataField]
    public string Message = string.Empty;

    [DataField]
    public string? AuthorName;

    [DataField]
    public string? AuthorTitle;

    [DataField]
    public string? StationName;

    [DataField]
    public DateTime Time;

    [DataField]
    public TimeSpan RoundTime;
}

[Serializable, NetSerializable, DataRecord]
public sealed partial record RecordLogs
{
    [DataField]
    public uint NextId = 1;

    [DataField]
    public List<RecordLogEntry> Security = new();

    [DataField]
    public List<RecordLogEntry> Psionics = new();

    [DataField]
    public List<RecordLogEntry> Crew = new();

    public List<RecordLogEntry> Book(RecordLogBook book)
    {
        return book switch
        {
            RecordLogBook.Security => Security,
            RecordLogBook.Psionics => Psionics,
            _ => Crew,
        };
    }
}

public static class RecordLogNotes
{
    public const int MaxLength = 300;

    public static string Sanitize(string text)
    {
        var result = new System.Text.StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = result.Length > 0;
                continue;
            }

            if (space)
                result.Append(' ');
            space = false;
            result.Append(c);
        }

        return result.ToString();
    }
}

[Serializable, NetSerializable]
public sealed class RecordLogAddNoteMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class RecordLogPrintMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RecordLogDeleteMessage(uint entryId) : BoundUserInterfaceMessage
{
    public readonly uint EntryId = entryId;
}
