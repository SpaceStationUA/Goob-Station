// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Common.Heretic;

/// <summary>
/// Raised on an access-checking user before an access reader evaluates its credentials.
/// </summary>
[ByRefEvent]
public record struct BeforeAccessReaderCheckEvent(EntityUid Reader, bool Cancelled = false);
