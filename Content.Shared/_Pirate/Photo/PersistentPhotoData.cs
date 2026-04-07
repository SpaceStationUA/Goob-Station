// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;
using Content.Shared.Mobs;

namespace Content.Shared._Pirate.Photo;

/// <summary>
/// Durable metadata captured when a photo is taken.
/// This stores facts about the shot rather than runtime entity references.
/// </summary>
public sealed record PhotoCaptureData(
    int AreaWidth,
    int AreaHeight,
    List<PhotoCapturedSubjectData> Subjects,
    List<string> RecognizedNames);

public sealed record PhotoCapturedSubjectData(
    string DisplayName,
    MobState State,
    string GenderKey,
    List<string> HeldItems,
    bool IsHumanoid);

/// <summary>
/// Persistent photo state used by album persistence and other round-trip flows.
/// </summary>
public sealed class PersistentPhotoData
{
    public byte[] ImageData { get; init; } = Array.Empty<byte>();
    public byte[]? PreviewData { get; init; }
    public string? CustomName { get; init; }
    public string? CustomDescription { get; init; }
    public string? Caption { get; init; }
    public string? BaseDescription { get; init; }
    public PhotoCaptureData? CaptureData { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
