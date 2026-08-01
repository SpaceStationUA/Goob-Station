// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;

namespace Content.Pirate.Common.Heretic;

/// <summary>
/// Raised on a holosign projector before it consumes a charge and places its sign.
/// </summary>
[ByRefEvent]
public record struct BeforeHolosignUsedEvent(
    EntityUid User,
    EntityCoordinates ClickLocation,
    bool Handled = false,
    bool Cancelled = false);
