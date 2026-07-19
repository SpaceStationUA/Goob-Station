// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Ranching;

[Serializable, NetSerializable]
public sealed partial class FertilizeDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public record struct RanchingEggLaidEvent;

[ByRefEvent]
public record struct RanchingHappinessChangedEvent(float OldValue, float NewValue);
