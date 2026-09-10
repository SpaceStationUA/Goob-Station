using Content.Server.Objectives.Components;
using Content.Shared.Roles.Components;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Objectives.Components;
using Robust.Shared.Random;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles protect target selection for Malf AI objectives, prioritizing traitors and traitor targets.
/// </summary>
public sealed class MalfAiPickProtectTargetSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfAiPickProtectTargetComponent, ObjectiveAssignedEvent>(OnObjectiveAssigned);
    }

    private void OnObjectiveAssigned(EntityUid uid, MalfAiPickProtectTargetComponent comp, ref ObjectiveAssignedEvent args)
    {
        if (!TryComp<MalfAiSabotageObjectiveComponent>(uid, out var sabotageComp))
            return;

        if (TryPickProtectTarget(args.MindId, out var target))
        {
            // Use standard target system instead of custom fields
            if (TryComp<TargetObjectiveComponent>(uid, out var targetComp))
                _target.SetTarget(uid, target, targetComp);
        }
    }

    private bool TryPickProtectTarget(EntityUid mind, out EntityUid picked)
    {
        picked = default;

        var candidates = new List<EntityUid>();
        var traitorMinds = new List<Entity<MindComponent>>();
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var candidate, out var candidateMind))
        {
            if (candidate == mind ||
                candidateMind.OwnedEntity == null ||
                !_role.MindHasRole<TraitorRoleComponent>(candidate))
                continue;

            traitorMinds.Add((candidate, candidateMind));
            candidates.Add(candidate);
        }

        // Second priority: Find traitor targets from their objectives.
        if (candidates.Count == 0)
        {
            foreach (var traitorMind in traitorMinds)
            {
                foreach (var objective in traitorMind.Comp.Objectives)
                {
                    if (TryComp<TargetObjectiveComponent>(objective, out var target) &&
                        target.Target is { } targetMind &&
                        targetMind != mind)
                        candidates.Add(targetMind);
                }
            }
        }

        // Fallback: any crew member.
        if (candidates.Count == 0)
        {
            query = EntityQueryEnumerator<MindComponent>();
            while (query.MoveNext(out var candidate, out var candidateMind))
            {
                if (candidate != mind && candidateMind.OwnedEntity != null)
                    candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
            return false;

        picked = _random.Pick(candidates);
        return true;
    }
}
