// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

public sealed partial class SharedKnowledgeSystem
{
    // The source ledger is server-owned; clients consume the replicated total.
    private bool OwnsModifierLedger => _network.IsServer;

    public Entity<KnowledgeComponent>? SetTemporaryModifier(
        Entity<KnowledgeContainerComponent> store,
        EntProtoId id,
        EntityUid source,
        int amount)
    {
        if (!OwnsModifierLedger || EnsureKnowledge(store, id, popup: false) is not { } knowledge)
            return null;

        var sources = EnsureComp<KnowledgeTemporaryModifierSourcesComponent>(knowledge.Owner);
        if (sources.EntitySources.TryGetValue(source, out var existing) && existing == amount)
            return knowledge;

        sources.EntitySources[source] = amount;
        RecalculateTemporaryLevel(knowledge);
        return knowledge;
    }

    public void RemoveTemporaryModifier(Entity<KnowledgeContainerComponent> store, EntProtoId id, EntityUid source)
    {
        if (!OwnsModifierLedger ||
            GetKnowledge(store, id) is not { } knowledge ||
            !TryComp<KnowledgeTemporaryModifierSourcesComponent>(knowledge.Owner, out var sources) ||
            !sources.EntitySources.Remove(source))
            return;

        RecalculateTemporaryLevel(knowledge);
    }

    public void RemoveAllTemporaryModifiers(Entity<KnowledgeContainerComponent> store, EntityUid source)
    {
        if (!OwnsModifierLedger)
            return;

        foreach (var uid in store.Comp.Knowledge.Values)
        {
            if (!_knowledgeQuery.TryComp(uid, out var knowledge) ||
                !TryComp<KnowledgeTemporaryModifierSourcesComponent>(uid, out var sources) ||
                !sources.EntitySources.Remove(source))
                continue;

            RecalculateTemporaryLevel((uid, knowledge));
        }
    }

    public Entity<KnowledgeComponent>? SetNamedTemporaryModifier(
        Entity<KnowledgeContainerComponent> store,
        EntProtoId id,
        string source,
        int amount)
    {
        if (!OwnsModifierLedger || EnsureKnowledge(store, id, popup: false) is not { } knowledge)
            return null;

        var sources = EnsureComp<KnowledgeTemporaryModifierSourcesComponent>(knowledge.Owner);
        if (sources.NamedSources.TryGetValue(source, out var existing) && existing == amount)
            return knowledge;

        sources.NamedSources[source] = amount;
        RecalculateTemporaryLevel(knowledge);
        return knowledge;
    }

    public void RemoveNamedTemporaryModifier(Entity<KnowledgeContainerComponent> store, EntProtoId id, string source)
    {
        if (!OwnsModifierLedger ||
            GetKnowledge(store, id) is not { } knowledge ||
            !TryComp<KnowledgeTemporaryModifierSourcesComponent>(knowledge.Owner, out var sources) ||
            !sources.NamedSources.Remove(source))
            return;

        RecalculateTemporaryLevel(knowledge);
    }

    public void RemoveNamedTemporaryModifiers(Entity<KnowledgeContainerComponent> store, string source)
    {
        if (!OwnsModifierLedger)
            return;

        foreach (var uid in store.Comp.Knowledge.Values)
        {
            if (!_knowledgeQuery.TryComp(uid, out var knowledge) ||
                !TryComp<KnowledgeTemporaryModifierSourcesComponent>(uid, out var sources) ||
                !sources.NamedSources.Remove(source))
                continue;

            RecalculateTemporaryLevel((uid, knowledge));
        }
    }

    // Rebuild the aggregate from employer and source contributions.
    public void RecalculateTemporaryLevel(Entity<KnowledgeComponent> knowledge)
    {
        if (!OwnsModifierLedger)
            return;

        var total = CompOrNull<EmployerKnowledgeBonusComponent>(knowledge.Owner)?.Level ?? 0;

        if (TryComp<KnowledgeTemporaryModifierSourcesComponent>(knowledge.Owner, out var sources))
        {
            foreach (var value in sources.EntitySources.Values)
                total += value;

            foreach (var value in sources.NamedSources.Values)
                total += value;
        }

        if (knowledge.Comp.TemporaryLevel == total)
            return;

        knowledge.Comp.TemporaryLevel = total;
        Dirty(knowledge);
    }

    public void PruneDeletedModifierSources(Entity<KnowledgeContainerComponent> store)
    {
        if (!OwnsModifierLedger)
            return;

        foreach (var uid in store.Comp.Knowledge.Values)
        {
            if (!_knowledgeQuery.TryComp(uid, out var knowledge) ||
                !TryComp<KnowledgeTemporaryModifierSourcesComponent>(uid, out var sources))
                continue;

            var removed = false;
            foreach (var source in sources.EntitySources.Keys.ToArray())
            {
                if (Exists(source) && !TerminatingOrDeleted(source))
                    continue;

                sources.EntitySources.Remove(source);
                removed = true;
            }

            if (removed)
                RecalculateTemporaryLevel((uid, knowledge));
        }
    }

    private void MergeModifierSources(Entity<KnowledgeComponent> source, Entity<KnowledgeComponent> destination)
    {
        if (!TryComp<KnowledgeTemporaryModifierSourcesComponent>(source.Owner, out var from))
            return;

        if (from.EntitySources.Count == 0 && from.NamedSources.Count == 0)
            return;

        var to = EnsureComp<KnowledgeTemporaryModifierSourcesComponent>(destination.Owner);

        foreach (var (key, value) in from.EntitySources)
        {
            if (to.EntitySources.TryGetValue(key, out var existing))
            {
                if (existing != value)
                    Log.Warning($"Merging {ToPrettyString(source)} into {ToPrettyString(destination)}: source {ToPrettyString(key)} disagrees ({existing} vs {value}); keeping {existing}.");
                continue;
            }

            to.EntitySources[key] = value;
        }

        foreach (var (key, value) in from.NamedSources)
        {
            if (to.NamedSources.TryGetValue(key, out var existing))
            {
                if (existing != value)
                    Log.Warning($"Merging {ToPrettyString(source)} into {ToPrettyString(destination)}: package '{key}' disagrees ({existing} vs {value}); keeping {existing}.");
                continue;
            }

            to.NamedSources[key] = value;
        }
    }

    private void MergeCompetency(EntityUid source, EntityUid destination)
    {
        if (!TryComp<KnowledgeCompetencyComponent>(source, out var from) || from.Minimums.Count == 0)
            return;

        var to = EnsureComp<KnowledgeCompetencyComponent>(destination);
        foreach (var (id, level) in from.Minimums)
        {
            if (to.Minimums.GetValueOrDefault(id) < level)
                to.Minimums[id] = level;
        }
    }

    public void GrantCompetency(EntityUid holder, IReadOnlyDictionary<EntProtoId, int> skills)
    {
        if (!OwnsModifierLedger || !SkillsEnabled)
            return;

        var store = EnsureKnowledgeContainer(holder);
        var competency = EnsureComp<KnowledgeCompetencyComponent>(store.Owner);

        foreach (var (id, level) in skills)
        {
            if (!AllKnowledges.ContainsKey(id))
            {
                Log.Error($"Competence package for {ToPrettyString(holder)} referenced unknown skill {id}.");
                continue;
            }

            var clamped = Math.Clamp(level, 0, 100);
            if (competency.Minimums.GetValueOrDefault(id) < clamped)
                competency.Minimums[id] = clamped;

            EnsureKnowledge(store, id, clamped, popup: false);
        }
    }

    public void ReplayCompetency(EntityUid holder)
    {
        if (!OwnsModifierLedger || !SkillsEnabled || GetContainer(holder) is not { } store ||
            !TryComp<KnowledgeCompetencyComponent>(store.Owner, out var competency))
            return;

        foreach (var (id, level) in competency.Minimums)
        {
            if (AllKnowledges.ContainsKey(id))
                EnsureKnowledge(store, id, level, popup: false);
        }
    }
}
