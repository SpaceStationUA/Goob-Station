// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.Damage;

/// <summary>
/// Naturally restores Soul damage after the victim has gone a short time without taking more.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(SoulDamageRegenerationSystem))]
public sealed partial class SoulDamageRegenerationComponent : Component
{
    [DataField]
    public TimeSpan RecoveryDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public FixedPoint2 HealAmount = 1;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, Access(Other = AccessPermissions.ReadWrite)]
    public TimeSpan NextHeal;
}
