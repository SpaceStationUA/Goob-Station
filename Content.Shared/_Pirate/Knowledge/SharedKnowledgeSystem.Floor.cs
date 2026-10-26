// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

public sealed partial class SharedKnowledgeSystem
{
    public int GetFloorLevel(EntityUid holder, EntProtoId id)
        => TryComp<KnowledgeFloorComponent>(holder, out var floor) && !floor.Exclude.Contains(id)
            ? Math.Clamp(floor.Level, 0, 100)
            : 0;

    public void SyncHolderFloor(Entity<KnowledgeContainerComponent> store)
    {
        if (!OwnsModifierLedger || !SkillsEnabled)
            return;

        if (store.Comp.Holder is not { } holder || !TryComp<KnowledgeFloorComponent>(holder, out var floor))
        {
            ClearFloor(store);
            return;
        }

        var level = Math.Clamp(floor.Level, 0, 100);
        foreach (var id in AllKnowledges.Keys.ToArray())
        {
            var existing = GetKnowledge(store, id);
            var floorLevel = floor.Exclude.Contains(id) ? 0 : level;
            if (existing is null && floorLevel == 0)
                continue;

            if (existing is not { } knowledge)
            {
                if (EnsureKnowledge(store, id, popup: false) is not { } created)
                    continue;

                knowledge = created;
                knowledge.Comp.FloorGranted = true;
            }

            if (knowledge.Comp.FloorLevel == floorLevel)
                continue;

            knowledge.Comp.FloorLevel = floorLevel;
            Dirty(knowledge);
        }
    }

    private void ClearFloor(Entity<KnowledgeContainerComponent> store)
    {
        if (!OwnsModifierLedger)
            return;

        var removed = false;
        foreach (var (id, uid) in store.Comp.Knowledge.ToArray())
        {
            if (!_knowledgeQuery.TryComp(uid, out var knowledge) ||
                knowledge.FloorLevel == 0 && !knowledge.FloorGranted)
                continue;

            // Preserve skills learned while the floor was active.
            if (knowledge.FloorGranted && knowledge.LearnedLevel == 0 && knowledge.TemporaryLevel == 0)
            {
                store.Comp.Knowledge.Remove(id);
                PredictedQueueDel(uid);
                removed = true;
                continue;
            }

            knowledge.FloorLevel = 0;
            knowledge.FloorGranted = false;
            Dirty(uid, knowledge);
        }

        if (removed)
            DirtyField(store, store.Comp, nameof(KnowledgeContainerComponent.Knowledge));
    }
}
