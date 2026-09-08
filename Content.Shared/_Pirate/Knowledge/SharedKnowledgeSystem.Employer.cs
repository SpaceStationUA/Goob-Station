// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Contractors.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

public sealed partial class SharedKnowledgeSystem
{
    /// <summary>
    /// Adds the employer's mastery bonuses without spending points or changing learned progress.
    /// Call after applying the species and saved character profiles.
    /// </summary>
    public void ApplyEmployerBonuses(EntityUid holder, string? employerId)
    {
        if (!SkillsEnabled || string.IsNullOrWhiteSpace(employerId) ||
            !_prototypes.TryIndex<EmployerPrototype>(employerId, out var employer) ||
            employer.KnowledgeBonuses.Count == 0)
            return;

        var store = EnsureKnowledgeContainer(holder);
        foreach (var (id, mastery) in employer.KnowledgeBonuses)
        {
            if (mastery <= 0 || !AllKnowledges.ContainsKey(id) ||
                EnsureKnowledge(store, id, popup: false) is not { } knowledge)
                continue;

            ApplyEmployerBonus(knowledge, mastery);
        }
    }

    private void ApplyEmployerBonus(Entity<KnowledgeComponent> knowledge, int mastery)
    {
        var bonus = EnsureComp<EmployerKnowledgeBonusComponent>(knowledge.Owner);
        // Remove only our previous contribution; preserve temporary levels from other sources.
        var baseLevel = knowledge.Comp.LearnedLevel + knowledge.Comp.TemporaryLevel - bonus.Level;
        // Level 100 has an internal mastery beyond the last displayed rank. An employer must
        // not upgrade an already-masterful skill into that extra level range.
        var maxMastery = MasteryNames.Length - 1;
        var targetMastery = Math.Min(GetMastery(baseLevel) + Math.Min(mastery, maxMastery), maxMastery);
        var level = Math.Max(GetInverseMastery(targetMastery) - baseLevel, 0);

        knowledge.Comp.TemporaryLevel += level - bonus.Level;
        bonus.Mastery = mastery;
        bonus.Level = level;
        Dirty(knowledge);
    }

    private void MergeEmployerBonus(Entity<KnowledgeComponent> source, Entity<KnowledgeComponent> destination)
    {
        var sourceMastery = CompOrNull<EmployerKnowledgeBonusComponent>(source.Owner)?.Mastery ?? 0;
        var destinationMastery = CompOrNull<EmployerKnowledgeBonusComponent>(destination.Owner)?.Mastery ?? 0;
        var mastery = Math.Max(sourceMastery, destinationMastery);
        if (mastery > 0)
            ApplyEmployerBonus(destination, mastery);
    }
}
