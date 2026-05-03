// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Эдуард <36124833+Ertanic@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Tayrtahn <tayrtahn@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Security;
using Content.Shared.Preferences; // Pirate: records photos
using Content.Shared.StationRecords; // Pirate: records photos
using Robust.Shared.Serialization;

namespace Content.Shared.CriminalRecords;

/// <summary>
/// Criminal record for a crewmember.
/// Can be viewed and edited in a criminal records console by security.
/// </summary>
[Serializable, NetSerializable, DataRecord]
public sealed partial record CriminalRecord
{
    /// <summary>
    /// Status of the person (None, Wanted, Detained).
    /// </summary>
    [DataField]
    public SecurityStatus Status = SecurityStatus.None;

    /// <summary>
    /// When Status is Wanted, the reason for it.
    /// Should never be set otherwise.
    /// </summary>
    [DataField]
    public string? Reason;

    /// <summary>
    /// The name of the person who changed the status.
    /// </summary>
    [DataField]
    public string? InitiatorName;

    #region Pirate: records photos
    /// <summary>
    /// Snapshot of the linked general record data.
    /// Used when a criminal record temporarily exists without a general record entry.
    /// </summary>
    [DataField]
    public GeneralStationRecord? GeneralRecordSnapshot;
    /// <summary>
    /// Snapshot of the character profile taken when the criminal record is first created.
    /// Used to render a stable portrait that does not change with later appearance changes.
    /// </summary>
    [DataField]
    public HumanoidCharacterProfile? PortraitProfileSnapshot;
    /// <summary>
    /// Stored portrait image bytes (PNG) used for photo printing and uploaded portrait display.
    /// </summary>
    [DataField]
    public byte[]? PortraitImageData;

    /// <summary>
    /// Optional small preview PNG for card visuals.
    /// </summary>
    [DataField]
    public byte[]? PortraitPreviewData;

    #endregion

    /// <summary>
    /// Criminal history of the person.
    /// This should have charges and time served added after someone is detained.
    /// </summary>
    [DataField]
    public List<CrimeHistory> History = new();
}

/// <summary>
/// A line of criminal activity and the time it was added at.
/// </summary>
[Serializable, NetSerializable]
public record struct CrimeHistory(TimeSpan AddTime, string Crime, string? InitiatorName);
