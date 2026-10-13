// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    public const int MaxCharacterInfoLength = 4096;

    public const int MaxRecordBackgroundLength = 1024;

    /// <summary>
    /// Physical description keeps using the existing flavor text field for backwards compatibility.
    /// </summary>
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
    public string SecurityRecordBackground { get; set; } = string.Empty;

    [DataField]
    public string CrewRecordBackground { get; set; } = string.Empty;

    [DataField]
    public string PsionicsRecordBackground { get; set; } = string.Empty;

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

    public HumanoidCharacterProfile WithSecurityRecordBackground(string text)
    {
        return new(this) { SecurityRecordBackground = text };
    }

    public HumanoidCharacterProfile WithCrewRecordBackground(string text)
    {
        return new(this) { CrewRecordBackground = text };
    }

    public HumanoidCharacterProfile WithPsionicsRecordBackground(string text)
    {
        return new(this) { PsionicsRecordBackground = text };
    }

    public HumanoidCharacterProfile WithRecordBackgrounds(string security, string crew, string psionics)
    {
        return new(this)
        {
            SecurityRecordBackground = security,
            CrewRecordBackground = crew,
            PsionicsRecordBackground = psionics,
        };
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
        SecurityRecordBackground = other.SecurityRecordBackground;
        CrewRecordBackground = other.CrewRecordBackground;
        PsionicsRecordBackground = other.PsionicsRecordBackground;
    }

    private bool PirateCharacterInfoEquals(HumanoidCharacterProfile other)
    {
        return PersonalityDescription == other.PersonalityDescription
            && PersonalNotes == other.PersonalNotes
            && OOCNotes == other.OOCNotes
            && Secrets == other.Secrets
            && ExploitableInfo == other.ExploitableInfo
            && SecurityRecordBackground == other.SecurityRecordBackground
            && CrewRecordBackground == other.CrewRecordBackground
            && PsionicsRecordBackground == other.PsionicsRecordBackground;
    }

    private void EnsurePirateCharacterInfoValid()
    {
        PersonalityDescription = SanitizeCharacterInfo(PersonalityDescription);
        PersonalNotes = SanitizeCharacterInfo(PersonalNotes);
        OOCNotes = SanitizeCharacterInfo(OOCNotes);
        Secrets = SanitizeCharacterInfo(Secrets);
        ExploitableInfo = SanitizeCharacterInfo(ExploitableInfo);
        SecurityRecordBackground = SanitizeCharacterInfo(SecurityRecordBackground, MaxRecordBackgroundLength);
        CrewRecordBackground = SanitizeCharacterInfo(CrewRecordBackground, MaxRecordBackgroundLength);
        PsionicsRecordBackground = SanitizeCharacterInfo(PsionicsRecordBackground, MaxRecordBackgroundLength);
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
        hashCode.Add(SecurityRecordBackground);
        hashCode.Add(CrewRecordBackground);
        hashCode.Add(PsionicsRecordBackground);
    }
}
