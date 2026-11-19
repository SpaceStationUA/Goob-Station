// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Clothing;

[ByRefEvent]
public record struct GetClothingAccessoryWearerEvent(EntityUid? Wearer = null);
