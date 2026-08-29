// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Silicons;

/// <summary>
/// Raised on an inserted borg brain or MMI after it enters a chassis.
/// </summary>
[ByRefEvent]
public record struct BorgBrainInsertedEvent(EntityUid Chassis, EntityUid Brain);

/// <summary>
/// Raised on a removed borg brain or MMI after it leaves a chassis.
/// </summary>
[ByRefEvent]
public record struct BorgBrainRemovedEvent(EntityUid Chassis, EntityUid Brain);
