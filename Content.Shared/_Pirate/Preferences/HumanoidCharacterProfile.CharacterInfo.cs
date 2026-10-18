// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared._Pirate.StationRecords;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    public const int MaxCharacterInfoLength = 4096;

    // Keep the legacy FlavorText backing for serialized profile compatibility.
    public string PhysicalDescription
    {
        get => FlavorText;
        set => FlavorText = value;
    }

    [DataField]
    public string PersonalityDescription { get; set; } = string.Empty;

    [DataField]
    public string PersonalNotes { get; set; } = string.Empty;

    [DataField]
    public string OOCNotes { get; set; } = string.Empty;

    [DataField]
    public string Secrets { get; set; } = string.Empty;

    [DataField]
    public string ExploitableInfo { get; set; } = string.Empty;

    [DataField]
    public List<RecordBackgroundEntry> RecordBackgrounds { get; set; } = new();

    public HumanoidCharacterProfile WithPhysicalDescription(string description)
    {
        return new(this) { PhysicalDescription = description };
    }

    public HumanoidCharacterProfile WithPersonalityDescription(string description)
    {
        return new(this) { PersonalityDescription = description };
    }

    public HumanoidCharacterProfile WithPersonalNotes(string notes)
    {
        return new(this) { PersonalNotes = notes };
    }

    public HumanoidCharacterProfile WithOOCNotes(string notes)
    {
        return new(this) { OOCNotes = notes };
    }

    public HumanoidCharacterProfile WithSecrets(string secrets)
    {
        return new(this) { Secrets = secrets };
    }

    public HumanoidCharacterProfile WithExploitableInfo(string info)
    {
        return new(this) { ExploitableInfo = info };
    }

    public HumanoidCharacterProfile WithRecordBackgrounds(IEnumerable<RecordBackgroundEntry> entries)
    {
        return new(this) { RecordBackgrounds = new List<RecordBackgroundEntry>(entries) };
    }

    public HumanoidCharacterProfile WithCharacterInfo(
        string personalityDescription,
        string personalNotes,
        string oocNotes,
        string secrets,
        string exploitableInfo)
    {
        return new(this)
        {
            PersonalityDescription = personalityDescription,
            PersonalNotes = personalNotes,
            OOCNotes = oocNotes,
            Secrets = secrets,
            ExploitableInfo = exploitableInfo,
        };
    }

    private void CopyPirateCharacterInfo(HumanoidCharacterProfile other)
    {
        PersonalityDescription = other.PersonalityDescription;
        PersonalNotes = other.PersonalNotes;
        OOCNotes = other.OOCNotes;
        Secrets = other.Secrets;
        ExploitableInfo = other.ExploitableInfo;
        RecordBackgrounds = new List<RecordBackgroundEntry>(other.RecordBackgrounds);
    }

    private bool PirateCharacterInfoEquals(HumanoidCharacterProfile other)
    {
        return PersonalityDescription == other.PersonalityDescription
            && PersonalNotes == other.PersonalNotes
            && OOCNotes == other.OOCNotes
            && Secrets == other.Secrets
            && ExploitableInfo == other.ExploitableInfo
            && RecordBackgrounds.SequenceEqual(other.RecordBackgrounds);
    }

    private void EnsurePirateCharacterInfoValid()
    {
        PersonalityDescription = SanitizeCharacterInfo(PersonalityDescription);
        PersonalNotes = SanitizeCharacterInfo(PersonalNotes);
        OOCNotes = SanitizeCharacterInfo(OOCNotes);
        Secrets = SanitizeCharacterInfo(Secrets);
        ExploitableInfo = SanitizeCharacterInfo(ExploitableInfo);
        RecordBackgrounds = SanitizeRecordBackgrounds(RecordBackgrounds);
    }

    private static List<RecordBackgroundEntry> SanitizeRecordBackgrounds(List<RecordBackgroundEntry> entries)
    {
        var result = new List<RecordBackgroundEntry>();
        var perBook = new Dictionary<RecordLogBook, int>();
        foreach (var entry in entries)
        {
            if (entry.Book is not (RecordLogBook.Security or RecordLogBook.Psionics or RecordLogBook.Crew))
                continue;

            var body = SanitizeCharacterInfo(entry.Body.Trim(), RecordBackgroundEntry.MaxBodyLength);
            var count = perBook.GetValueOrDefault(entry.Book);
            if (body.Length == 0 || count >= RecordBackgroundEntry.MaxPerBook)
                continue;

            perBook[entry.Book] = count + 1;
            result.Add(new RecordBackgroundEntry
            {
                Book = entry.Book,
                Title = SanitizeLine(entry.Title, RecordBackgroundEntry.MaxTitleLength),
                Author = SanitizeLine(entry.Author, RecordBackgroundEntry.MaxAuthorLength),
                Body = body,
            });
        }

        return result;
    }

    private static string SanitizeLine(string value, int maxLength)
    {
        return SanitizeCharacterInfo(value.Replace('\r', ' ').Replace('\n', ' ').Trim(), maxLength);
    }

    private static string SanitizeCharacterInfo(string value, int maxLength = MaxCharacterInfoLength)
    {
        var sanitized = FormattedMessage.RemoveMarkupOrThrow(value);
        return sanitized.Length > maxLength
            ? sanitized[..maxLength]
            : sanitized;
    }

    private void AddPirateCharacterInfoHash(ref HashCode hashCode)
    {
        hashCode.Add(PersonalityDescription);
        hashCode.Add(PersonalNotes);
        hashCode.Add(OOCNotes);
        hashCode.Add(Secrets);
        hashCode.Add(ExploitableInfo);
        foreach (var entry in RecordBackgrounds)
        {
            hashCode.Add(entry);
        }
    }
}
