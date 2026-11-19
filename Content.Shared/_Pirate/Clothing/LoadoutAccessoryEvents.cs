// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Clothing;

[ByRefEvent]
public record struct LoadoutAccessoryCheckEvent(bool IsAccessory = false);

[ByRefEvent]
public record struct LoadoutAccessoryAttachEvent(EntityUid Wearer, bool Attached = false);
