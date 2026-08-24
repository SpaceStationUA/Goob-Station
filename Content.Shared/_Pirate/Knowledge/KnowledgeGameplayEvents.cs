// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

[ByRefEvent]
public record struct UserModifyInjectTimeEvent(EntityUid User, EntityUid Injector, TimeSpan Delay);

[ByRefEvent]
public record struct GetBlockFractionEvent(EntityUid User, EntityUid Blocker, float Fraction);

[ByRefEvent]
public record struct ModifyThrownSpeedEvent(EntityUid User, float BaseThrowSpeed, float Distance);

[ByRefEvent]
public record struct ModifyThrowInsertChanceEvent(EntityUid User, float Chance);

[ByRefEvent]
public record struct CookedFoodEvent(EntityUid User, EntProtoId Result, int Count = 1);
