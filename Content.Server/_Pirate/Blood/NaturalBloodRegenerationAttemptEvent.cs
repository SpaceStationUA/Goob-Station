// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;

namespace Content.Server.Pirate.Blood.Events;

/// <summary>
///     Raised on a mob when its bloodstream tries to perform natural blood regeneration.
/// </summary>
[ByRefEvent]
public sealed class NaturalBloodRegenerationAttemptEvent : CancellableEntityEventArgs
{
    /// <summary>
    ///     How much blood the mob will regenerate on this tick. Can be negative.
    /// </summary>
    public FixedPoint2 Amount;
}
