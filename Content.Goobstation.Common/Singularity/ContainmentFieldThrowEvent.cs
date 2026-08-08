// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Singularity;

/// <summary>
/// Raised on an entity that just collided with a containment field
/// </summary>
[ByRefEvent]
// Pirate: include the collider so field effects can target the entity that crossed it.
public record struct ContainmentFieldThrowEvent(EntityUid Entity, EntityUid Field, bool Cancelled = false);
