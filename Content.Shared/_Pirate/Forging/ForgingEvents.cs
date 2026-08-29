// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Forging;

[ByRefEvent]
public record struct ItemForgedEvent(ProtoId<ForgedItemPrototype> Item);

[ByRefEvent]
public record struct ForgingCompletedEvent(
    MetalPrototype Metal,
    ForgedItemPrototype Item,
    EntityUid Target,
    EntityUid? User);

[ByRefEvent]
public record struct MetalWorkableChangedEvent(bool Workable);

[ByRefEvent]
public record struct MetalChangedEvent(MetalPrototype Metal);

[ByRefEvent]
public record struct MetalWroughtEvent(EntityUid Result, EntityUid? User);

[ByRefEvent]
public record struct ForgingWorkInitializedEvent(FixedPoint2 Work);

[ByRefEvent]
public record struct DamageOnHoldingAttemptEvent(EntityUid Source, bool Cancelled = false);
