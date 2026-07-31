// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Common.Heretic;

/// <summary>
/// Raised on a Heretic before an action is allowed to cast.
/// </summary>
[ByRefEvent]
public record struct HereticMagicCastAttemptEvent(EntityUid Action, bool Cancelled = false);
