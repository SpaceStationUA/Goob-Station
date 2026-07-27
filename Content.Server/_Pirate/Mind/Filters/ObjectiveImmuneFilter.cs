// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Filters;

namespace Content.Server._Pirate.Mind.Filters;

/// <summary>
/// Removes minds marked as immune from a target-objective candidate pool.
/// </summary>
public sealed partial class ObjectiveImmuneFilter : MindFilter
{
    protected override bool ShouldRemove(
        Entity<MindComponent> mind,
        EntityUid? excluded,
        IEntityManager entMan,
        SharedMindSystem mindSys)
    {
        if (entMan.HasComponent<TargetObjectiveImmuneComponent>(mind))
            return true;

        return mind.Comp.OwnedEntity is { } entity &&
               entMan.HasComponent<TargetObjectiveImmuneComponent>(entity);
    }
}
