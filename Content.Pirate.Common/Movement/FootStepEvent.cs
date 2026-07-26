// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

namespace Content.Pirate.Common.Movement;

/// <summary>
/// Raised on a mob when it plays the footstep sound.
/// This mean it gets raised more often when sprinting, and isn't raised at all if you don't have footstep sounds.
/// </summary>
[ByRefEvent]
public readonly record struct FootStepEvent(EntityUid Mob, Angle WorldAngle);
