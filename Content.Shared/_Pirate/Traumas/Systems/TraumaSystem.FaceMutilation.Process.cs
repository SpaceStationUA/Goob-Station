using System;
using System.Collections.Generic;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;

public sealed partial class TraumaSystem
{
    private void InitFaceMutilation()
    {
        SubscribeLocalEvent<TraumaComponent, TraumaInducedEvent>(OnFaceMutilationTraumaInduced);
        SubscribeLocalEvent<TraumaComponent, TraumaBeingRemovedEvent>(OnFaceMutilationTraumaRemoved);
    }

    private void OnFaceMutilationTraumaInduced(
        Entity<TraumaComponent> trauma,
        ref TraumaInducedEvent args)
    {
        if (args.TraumaType != FaceMutilation)
            return;

        if (!_container.TryGetContainingContainer(
                (trauma.Owner, Transform(trauma.Owner), MetaData(trauma.Owner)),
                out var traumaContainer)
            || !TryComp<TraumaInflicterComponent>(traumaContainer.Owner, out var inflicter))
            return;

        ApplyFaceMutilationMarkings(trauma, args.TraumaTarget, inflicter);
    }

    private void OnFaceMutilationTraumaRemoved(
        Entity<TraumaComponent> trauma,
        ref TraumaBeingRemovedEvent args)
    {
        if (args.TraumaType == FaceMutilation)
            TryRemoveFaceMutilationMarkings(trauma);
    }

    private void ApplyFaceMutilationMarkings(
        Entity<TraumaComponent> faceTrauma,
        EntityUid traumaTarget,
        TraumaInflicterComponent inflicter)
    {
        if (!_net.IsServer
            || !TryComp<BodyPartComponent>(traumaTarget, out var targetPart)
            || targetPart.Body is not { } body
            || !TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
            return;

        var appliedMarkings = new List<string>();
        foreach (var markingId in inflicter.FaceMutilationMarkings)
        {
            if (!_markingManager.Markings.TryGetValue(markingId, out var markingProto))
                continue;

            var marking = markingProto.AsMarking();
            if (!_markingManager.IsValidMarking(marking, MarkingCategories.Head, humanoid.Species, humanoid.Sex))
                continue;

            _humanoid.AddMarking(body, markingId, sync: false, forced: true, humanoid: humanoid);
            appliedMarkings.Add(markingId);
        }

        if (appliedMarkings.Count == 0)
            return;

        faceTrauma.Comp.MarkingId = string.Join(',', appliedMarkings);
        Dirty(faceTrauma, faceTrauma.Comp);
        Dirty(body, humanoid);
    }

    private void TryRemoveFaceMutilationMarkings(Entity<TraumaComponent> trauma)
    {
        if (!_net.IsServer
            || trauma.Comp.TraumaType != FaceMutilation
            || trauma.Comp.TraumaTarget is not { } traumaTarget
            || trauma.Comp.MarkingId is not { } markingIdsRaw)
        {
            return;
        }

        var markingHolder = traumaTarget;
        if (TryComp<BodyPartComponent>(traumaTarget, out var traumaBodyPart)
            && traumaBodyPart.Body is { } body)
        {
            markingHolder = body;
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
