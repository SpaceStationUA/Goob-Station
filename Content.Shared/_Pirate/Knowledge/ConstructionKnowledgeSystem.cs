// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Construction;
using Content.Shared._Pirate.Construction;
using Content.Shared._Pirate.Forging;
using Content.Shared._Pirate.Knowledge.Quality;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Gates explicitly annotated recipes and rolls their result quality.
/// </summary>
public sealed class ConstructionKnowledgeSystem : EntitySystem
{
    private static readonly ProtoId<QualityPrototype> BaseQuality = "BaseQuality";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly QualitySystem _quality = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KnowledgeHolderComponent, ConstructAttemptEvent>(OnConstructAttempt);
        SubscribeLocalEvent<KnowledgeHolderComponent, ConstructedEvent>(OnConstructed);
        SubscribeLocalEvent<KnowledgeHolderComponent, ForgingCompletedEvent>(OnForgingCompleted);
    }

    private void OnConstructAttempt(Entity<KnowledgeHolderComponent> ent, ref ConstructAttemptEvent args)
    {
        if (args.Cancelled || !_knowledge.SkillsEnabled ||
            !_prototypes.TryIndex<ConstructionPrototype>(args.Prototype, out var prototype) ||
            prototype.Theory.Count == 0)
            return;

        if (_knowledge.GetContainer(ent.Owner) is not { } store)
        {
            if (args.LogError)
                _popup.PopupClient(Loc.GetString("knowledge-construction-no-container"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            args.Cancelled = true;
            return;
        }

        foreach (var (id, requiredMastery) in prototype.Theory)
        {
            var actualMastery = _knowledge.GetKnowledge(store, id) is { } unit
                ? SharedKnowledgeSystem.GetMastery(unit.Comp.NetLevel)
                : 0;
            if (actualMastery >= requiredMastery)
                continue;

            if (args.LogError)
            {
                var skillName = _prototypes.TryIndex(id, out var skillPrototype)
                    ? skillPrototype.Name
                    : id.Id;
                _popup.PopupClient(Loc.GetString(
                        "knowledge-construction-missing",
                        ("knowledge", skillName),
                        ("mastery", SharedKnowledgeSystem.GetMasteryString(requiredMastery))),
                    ent.Owner,
                    ent.Owner,
                    PopupType.MediumCaution);
            }

            args.Cancelled = true;
            return;
        }
    }

    private void OnConstructed(Entity<KnowledgeHolderComponent> ent, ref ConstructedEvent args)
    {
        if (!_prototypes.TryIndex<ConstructionPrototype>(args.Prototype, out var prototype))
            return;

        if (prototype.Experience.Count > 0 && _knowledge.GetContainer(ent.Owner) is { } store)
        {
            foreach (var (id, amount) in prototype.Experience)
                _knowledge.AddExperience(store, id, amount);
        }

        if (!prototype.UseQuality ||
            (prototype.Theory.Count == 0 && prototype.Practical is not { Count: > 0 }))
            return;

        if (EnsureComp<QualityComponent>(args.Entity, out var qualityAlreadyPresent))
            return;

        qualityAlreadyPresent.LevelDeltas = CombineRequirements(prototype);
        qualityAlreadyPresent.QualityFactors = prototype.QualityPrototype ?? BaseQuality;
        Dirty(args.Entity, qualityAlreadyPresent);
        _quality.RollQuality((args.Entity, qualityAlreadyPresent), ent.Owner);
    }

    private void OnForgingCompleted(Entity<KnowledgeHolderComponent> ent, ref ForgingCompletedEvent args)
    {
        if (EnsureComp<QualityComponent>(args.Target, out var qualityAlreadyPresent))
            return;

        qualityAlreadyPresent.LevelDeltas = new Dictionary<EntProtoId, int>(args.Item.Skills);
        foreach (var id in qualityAlreadyPresent.LevelDeltas.Keys.ToArray())
            qualityAlreadyPresent.LevelDeltas[id] += args.Metal.MasteryOffset;

        qualityAlreadyPresent.QualityFactors = args.Item.QualityPrototype ?? BaseQuality;
        Dirty(args.Target, qualityAlreadyPresent);
        _quality.RollQuality((args.Target, qualityAlreadyPresent), ent.Owner);
    }

    public static Dictionary<EntProtoId, int> CombineRequirements(ConstructionPrototype prototype)
    {
        var combined = prototype.Practical is null
            ? new Dictionary<EntProtoId, int>()
            : new Dictionary<EntProtoId, int>(prototype.Practical);

        foreach (var (id, mastery) in prototype.Theory)
        {
            if (!combined.TryGetValue(id, out var existing) || mastery > existing)
                combined[id] = mastery;
        }

        return combined;
    }
}
