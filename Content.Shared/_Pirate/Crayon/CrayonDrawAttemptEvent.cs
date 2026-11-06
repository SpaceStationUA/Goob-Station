// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Crayon;

/// <summary>Raised on a crayon before drawing on a paint surface.</summary>
[ByRefEvent]
public record struct CrayonDrawAttemptEvent(EntityUid User, bool Cancelled = false);
