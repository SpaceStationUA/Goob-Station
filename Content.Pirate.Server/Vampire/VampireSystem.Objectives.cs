using Content.Pirate.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Components;
using Content.Pirate.Shared.Vampire.Components;
using Content.Pirate.Shared.Vampire;

namespace Content.Pirate.Server.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private readonly NumberObjectiveSystem _number = default!;

    private void InitializeObjectives()
    {
        SubscribeLocalEvent<BloodDrainConditionComponent, ObjectiveGetProgressEvent>(OnBloodDrainGetProgress);
    }

    public void SetBloodDrainProgress(EntityUid vampire, float total)
    {
        if (!_mind.TryGetMind(vampire, out var mindId, out var mind))
            return;
        if (_mind.TryGetObjectiveComp<BloodDrainConditionComponent>(mindId, out var objective, mind))
            objective.BloodDranked = total;
    }

    public void AddBloodDrainProgress(EntityUid vampire, float delta)
    {
        if (!_mind.TryGetMind(vampire, out var mindId, out var mind))
            return;
        if (_mind.TryGetObjectiveComp<BloodDrainConditionComponent>(mindId, out var objective, mind))
            objective.BloodDranked += delta;
    }

    private void OnBloodDrainGetProgress(EntityUid uid, BloodDrainConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        var target = _number.GetTarget(uid);
        if (target > 0)
            args.Progress = MathF.Min(comp.BloodDranked / target, 1f);
        else args.Progress = 1f;
    }
}
