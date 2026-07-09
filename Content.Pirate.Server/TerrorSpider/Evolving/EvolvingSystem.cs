using Content.Pirate.Shared.TerrorSpider.Evolving;
using Content.Pirate.Shared.TerrorSpider.Evolving.Conditions;
using Content.Pirate.Shared.TerrorSpider.Evolving.EntitySystems;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Pirate.Server.TerrorSpider.Evolving;

public sealed class EvolvingSystem : SharedEvolvingSystem
{
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly NumberObjectiveSystem _numberObjectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvolveConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<EvolveConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!TryComp<NumberObjectiveComponent>(ent.Owner, out var objective))
            return;

        args.Progress = Math.Clamp((float) ent.Comp.Count / objective.Target, 0f, 1f);
    }

    public override EntityUid TryInitObjectives(EntityUid mindId, MindComponent mind, string objectiveId, EvolvingCondition condition)
    {
        var obj = _objectives.TryCreateObjective(mindId, mind, objectiveId);
        if (obj is not { Valid: true } objective)
            return EntityUid.Invalid;

        if (TryComp<EvolveConditionComponent>(objective, out var evolveCondition))
            evolveCondition.ConditionType = condition.Type;

        _numberObjectives.SetTarget(objective, condition.GetTarget());
        _numberObjectives.SetTitle(objective, $"objective-{condition.Type.ToString().ToLower()}-condition-title");
        _numberObjectives.SetDescription(objective, $"objective-{condition.Type.ToString().ToLower()}-condition-description");

        return objective;
    }
}
