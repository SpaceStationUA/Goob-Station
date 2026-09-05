// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Construction;

/// <summary>
/// Raised directly on the prospective builder before construction consumes any materials.
/// </summary>
[ByRefEvent]
public record struct ConstructAttemptEvent(string Prototype, bool LogError = true, bool Cancelled = false);
