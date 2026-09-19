// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;

namespace Content.Shared._Pirate.PersistentText;

/// <summary>
/// Server-side snapshot of a persistent text (diary/book content).
/// Never sent to clients; only used by server persistence flows.
/// </summary>
public sealed class PersistentTextSnapshot
{
    public string OwnerKind { get; init; } = string.Empty;
    public int? ProfileId { get; init; }
    public string? OwnerId { get; init; }
    public string StorageKey { get; init; } = string.Empty;
    public DateTime SavedAt { get; init; }
    public string Content { get; init; } = string.Empty;
}
