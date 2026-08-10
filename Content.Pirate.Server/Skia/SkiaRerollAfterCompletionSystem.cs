// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Objectives.Components;
using Content.Server.Roles.Jobs;
using Content.Pirate.Shared.Skia;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Popups;

namespace Content.Pirate.Server.Skia;

public sealed class SkiaRerollAfterCompletionSystem : EntitySystem
{
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    private readonly HashSet<SkiaRerollAfterCompletionComponent> _objectivesToAdd = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkiaRerollAfterCompletionComponent, ObjectiveAfterAssignEvent>(OnObjectiveAfterAssign);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _objectivesToAdd.Clear();
        var query = EntityQueryEnumerator<SkiaRerollAfterCompletionComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Rerolled || !HasComp<ObjectiveComponent>(uid))
                continue;

            if (!TryComp<MindComponent>(component.MindUid, out var mind))
                continue;

            if (!_objectives.IsCompleted(uid, new Entity<MindComponent>(component.MindUid, mind)))
                continue;

            RemCompDeferred<SkiaRerollAfterCompletionComponent>(uid);
            component.Rerolled = true;
            _objectivesToAdd.Add(component);
        }

        foreach (var component in _objectivesToAdd)
        {
            var mindUid = component.MindUid;
            if (!TryComp<MindComponent>(mindUid, out var mind))
                continue;

            if (_objectives.TryCreateObjective(mindUid, mind, component.RerollObjectivePrototype) is not { } newObjective)
                continue;

            if (component.RerollObjectiveMessage is { } message)
            {
                var body = mind.CurrentEntity ?? mindUid;
                if (TryComp<TargetObjectiveComponent>(newObjective, out var target)
                    && TryComp<MindComponent>(target.Target, out var targetMind))
                {
                    var targetName = targetMind.CharacterName ?? "Unknown";
                    var targetJob = _jobs.MindTryGetJobName(target.Target);
                    _popup.PopupEntity(Loc.GetString(message, ("targetName", targetName), ("job", targetJob)), body, body, PopupType.Large);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString(message), body, body, PopupType.Large);
                }
            }

            _mind.AddObjective(mindUid, mind, newObjective);
        }
    }

    private static void OnObjectiveAfterAssign(
        Entity<SkiaRerollAfterCompletionComponent> entity,
        ref ObjectiveAfterAssignEvent args)
    {
        entity.Comp.MindUid = args.MindId;
    }
}
