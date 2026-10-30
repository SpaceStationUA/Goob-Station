// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Interaction;

[ByRefEvent]
public record struct GetSmartEquipStorageItemEvent(EntityUid? Item);
