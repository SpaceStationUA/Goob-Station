// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Stunnable;

namespace Content.Server._Pirate.Silicons.HTN;

/// <summary>
/// Checks only the target already stored in the NPC blackboard.
/// </summary>
public sealed partial class TargetStunnedPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    [DataField]
    public bool Stunned = true;

    [DataField(required: true)]
    public string TargetKey = string.Empty;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entityManager))
            return false;

        return Stunned == _entityManager.HasComponent<StunnedComponent>(target);
    }
}
