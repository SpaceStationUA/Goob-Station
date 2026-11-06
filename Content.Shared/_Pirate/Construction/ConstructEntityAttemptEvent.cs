// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Construction;

/// <summary>Raised on the builder before an RCD creates an entity or consumes charges.</summary>
[ByRefEvent]
public record struct ConstructEntityAttemptEvent(string Prototype, bool ShowPopup = true, bool Cancelled = false);
