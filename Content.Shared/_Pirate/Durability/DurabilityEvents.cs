// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Durability;

[ByRefEvent]
public record struct DurabilityChangeAttemptEvent(EntityUid Uid, FixedPoint2 Damage);

[ByRefEvent]
public record struct DurabilityDamageChangedEvent(EntityUid Uid, FixedPoint2 Damage, FixedPoint2 OldDamage);

[ByRefEvent]
public record struct DurabilityStateChangedEvent(
    DurabilityState OldState,
    DurabilityState NewState,
    EntityUid Weapon,
    EntityUid? Attacker = null,
    HashSet<EntityUid>? Targets = null,
    EntityUid? Used = null);

[ByRefEvent]
public record struct DurabilityStateChangedByEvent(
    DurabilityState OldState,
    DurabilityState NewState,
    EntityUid Weapon,
    EntityUid? Attacker = null,
    HashSet<EntityUid>? Targets = null,
    EntityUid? Used = null);

[Serializable, NetSerializable]
public sealed partial class RepairItemDoAfterEvent : DoAfterEvent
{
    [DataField]
    public Vector2 MinMax;

    private RepairItemDoAfterEvent()
    {
    }

    public RepairItemDoAfterEvent(Vector2 minMax)
    {
        MinMax = minMax;
    }

    public override DoAfterEvent Clone() => new RepairItemDoAfterEvent(MinMax);
}

[Serializable, NetSerializable]
public sealed partial class RepairToolDoAfterEvent : SimpleDoAfterEvent;
