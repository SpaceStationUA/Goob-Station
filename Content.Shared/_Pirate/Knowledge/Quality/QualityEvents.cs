// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Knowledge.Quality;

[ByRefEvent]
public record struct ApplyQualityEvent(int Quality, QualityPrototype Prototype)
{
    public readonly float Modifier(float power = 1.1f)
        => QualitySystem.QualityModifier(Quality, power);
}

/// <summary>
/// Copies quality from the event target to a newly created entity.
/// </summary>
[ByRefEvent]
public readonly record struct QualityTransferEvent(EntityUid Created);
