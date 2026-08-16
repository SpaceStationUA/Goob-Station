// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Mind.Filters;
using Content.Server._Pirate.Objectives.Components; // Pirate: honor target-objective immunity.
using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Revolutionary.Components;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Handles assinging a target to an objective entity with <see cref="TargetObjectiveComponent"/> using different components.
/// These can be combined with condition components for objective completions in order to create a variety of objectives.
/// </summary>
public sealed class PickObjectiveTargetSystem : EntitySystem
{
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PickSpecificPersonComponent, ObjectiveAssignedEvent>(OnSpecificPersonAssigned);
        SubscribeLocalEvent<PickRandomPersonComponent, ObjectiveAssignedEvent>(OnRandomPersonAssigned);
    }

    private void OnSpecificPersonAssigned(Entity<PickSpecificPersonComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetObjectiveComponent>(ent.Owner, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        if (args.Mind.OwnedEntity == null)
        {
            args.Cancelled = true;
            return;
        }

        var user = args.Mind.OwnedEntity.Value;
        if (!TryComp<TargetOverrideComponent>(user, out var targetComp) || targetComp.Target == null)
        {
            args.Cancelled = true;
            return;
        }

        // Pirate: immune entities and their minds cannot be assigned as specific targets.
        if (IsTargetObjectiveImmune(targetComp.Target.Value))
        {
            args.Cancelled = true;
            return;
        }

        _target.SetTarget(ent.Owner, targetComp.Target.Value);
    }

    private void OnRandomPersonAssigned(Entity<PickRandomPersonComponent> ent, ref ObjectiveAssignedEvent args)
    {
        // invalid objective prototype
        if (!TryComp<TargetObjectiveComponent>(ent, out var target))
        {
            args.Cancelled = true;
            return;
        }

        // target already assigned
        if (target.Target != null)
            return;

        // Pirate: filter immune candidates before picking so one bad roll cannot cancel the objective.
        var filters = ent.Comp.Filters.ToList();
        filters.Add(new ObjectiveImmuneFilter());

        // couldn't find a target :(
        if (_mind.PickFromPool(ent.Comp.Pool, filters, args.MindId) is not {} picked)
        {
            args.Cancelled = true;
            return;
        }

        _target.SetTarget(ent, picked, target);
    }

    /// <summary>
    /// Picks an alive target mind for contract systems that need a custom predicate.
    /// </summary>
    public void AssignRandomTarget(
        EntityUid uid,
        ref ObjectiveAssignedEvent args,
        Predicate<EntityUid> filter,
        bool fallbackToAny = true)
    {
        if (!TryComp<TargetObjectiveComponent>(uid, out var target))
        {
            args.Cancelled = true;
            return;
        }

        if (target.Target != null)
            return;

        var candidates = _mind.GetAliveHumans(args.MindId)
            .Where(mind => !IsTargetObjectiveImmune(mind.Owner))
            .ToList();

        // Avoid assigning the same kill target twice.
        foreach (var objective in args.Mind.Objectives)
        {
            if (HasComp<KillPersonConditionComponent>(objective) &&
                TryComp<TargetObjectiveComponent>(objective, out var kill))
            {
                candidates.RemoveAll(mind => mind.Owner == kill.Target);
            }
        }

        // Targets on a different map cannot be reached during ordinary play.
        if (args.Mind.OwnedEntity is {} mob)
        {
            var map = Transform(mob).MapID;
            candidates.RemoveAll(mind => mind.Comp.OwnedEntity is not {} body || Transform(body).MapID != map);
        }

        var filtered = candidates.Where(mind => filter(mind.Owner)).ToList();
        if (filtered.Count == 0)
        {
            if (!fallbackToAny || candidates.Count == 0)
            {
                args.Cancelled = true;
                return;
            }

            filtered = candidates;
        }

        _target.SetTarget(uid, _random.Pick(filtered).Owner, target);
    }

    // Pirate: target overrides may point at either a body or a mind.
    private bool IsTargetObjectiveImmune(EntityUid target)
    {
        if (HasComp<TargetObjectiveImmuneComponent>(target))
            return true;

        if (TryComp<MindComponent>(target, out var targetMind))
        {
            return targetMind.OwnedEntity is { } owned &&
                   HasComp<TargetObjectiveImmuneComponent>(owned);
        }

        return _mind.TryGetMind(target, out var mindId, out _) &&
               HasComp<TargetObjectiveImmuneComponent>(mindId);
    }
}
