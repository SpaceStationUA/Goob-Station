using System;
using System.Collections.Generic;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Random;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

public sealed partial class TraumaSystem
{
    public bool RandomFaceMutilationChance(
        Entity<WoundableComponent> target,
        Entity<TraumaInflicterComponent> woundInflicter)
    {
        if (!TryComp<BodyPartComponent>(target, out var bodyPart)
            || !bodyPart.Body.HasValue
            || bodyPart.PartType != BodyPartType.Head
            || target.Comp.WoundableSeverity < WoundableSeverity.Moderate
            || target.Comp.WoundableSeverity == WoundableSeverity.Severed
            || HasWoundableTrauma(target, TraumaType.FaceMutilation, target))
        {
            return false;
        }

        if (!TryComp<WoundComponent>(woundInflicter, out var woundComp))
            return false;

        var deduction = GetTraumaChanceDeduction(
            woundInflicter,
            bodyPart.Body.Value,
            target,
            woundComp.WoundSeverityPoint,
            TraumaType.FaceMutilation,
            BodyPartType.Head);

        if (deduction >= 1)
            return false;

        if (target.Comp.IntegrityCap <= 0)
            return false;

        var damageFraction = FixedPoint2.Clamp(
            1f - target.Comp.WoundableIntegrity / target.Comp.IntegrityCap,
            0,
            1);

        var chance = FixedPoint2.Clamp(
            damageFraction * 0.6f - deduction + woundInflicter.Comp.TraumasChances[TraumaType.FaceMutilation],
            0,
            1);

        return _random.Prob((float) chance);
    }

    private void ApplyFaceMutilationTrauma(
        Entity<WoundableComponent> target,
        EntityUid traumaTarget,
        Entity<TraumaInflicterComponent> inflicter)
    {
        var faceTraumaEnt = AddTrauma(traumaTarget, target, inflicter, TraumaType.FaceMutilation, 0);
        if (faceTraumaEnt == EntityUid.Invalid
            || !_net.IsServer)
        {
            return;
        }

        if (!TryComp<BodyPartComponent>(target, out var targetPartComp)
            || !targetPartComp.Body.HasValue
            || !TryComp<HumanoidAppearanceComponent>(targetPartComp.Body.Value, out var headOwnerHumanoid))
        {
            return;
        }

        var appliedMarkings = new List<string>();

        foreach (var markingId in inflicter.Comp.FaceMutilationMarkings)
        {
            if (!_markingManager.Markings.TryGetValue(markingId, out var markingProto))
                continue;

            var marking = markingProto.AsMarking();
            if (!_markingManager.IsValidMarking(marking, MarkingCategories.Head, headOwnerHumanoid.Species, headOwnerHumanoid.Sex))
                continue;

            _humanoid.AddMarking(targetPartComp.Body.Value, markingId, sync: false, forced: true, humanoid: headOwnerHumanoid);
            appliedMarkings.Add(markingId);
        }

        if (appliedMarkings.Count > 0 && TryComp<TraumaComponent>(faceTraumaEnt, out var traumaComp))
        {
            traumaComp.MarkingId = string.Join(',', appliedMarkings);
            Dirty(faceTraumaEnt, traumaComp);
        }

        Dirty(targetPartComp.Body.Value, headOwnerHumanoid);
    }

    private void TryRemoveFaceMutilationMarkings(Entity<TraumaComponent> trauma)
    {
        if (!_net.IsServer
            || trauma.Comp.TraumaType != TraumaType.FaceMutilation
            || trauma.Comp.TraumaTarget is not { } traumaTarget
            || trauma.Comp.MarkingId is not { } markingIdsRaw)
        {
            return;
        }

        var markingHolder = traumaTarget;
        if (TryComp<BodyPartComponent>(traumaTarget, out var traumaBodyPart)
            && traumaBodyPart.Body.HasValue)
        {
            markingHolder = traumaBodyPart.Body.Value;
        }

        if (!TryComp<HumanoidAppearanceComponent>(markingHolder, out var humanoid))
            return;

        var markingIds = markingIdsRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var markingId in markingIds)
        {
            if (_markingManager.Markings.TryGetValue(markingId, out var prototype))
                humanoid.MarkingSet.Remove(prototype.MarkingCategory, markingId);
        }

        Dirty(markingHolder, humanoid);
    }
}
