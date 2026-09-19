// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Shitmed.Cybernetics;
using Content.Shared.EntityConditions;
using Content.Shared.Examine;

namespace Content.Shared._Pirate.Knowledge;

public sealed partial class SharedKnowledgeSystem
{
    [Dependency] private readonly SharedEntityConditionsSystem _conditions = default!;

    private void InitializeOnWear()
    {
        SubscribeLocalEvent<KnowledgeGrantOnWearComponent, ExaminedEvent>(OnWearExamined);
        SubscribeLocalEvent<KnowledgeGrantOnWearComponent, OrganChipInsertedEvent>(OnChipInserted);
        SubscribeLocalEvent<KnowledgeGrantOnWearComponent, OrganChipRemovedEvent>(OnChipRemoved);
    }

    private void OnWearExamined(Entity<KnowledgeGrantOnWearComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !ent.Comp.Examinable || ent.Comp.Skills.Count == 0)
            return;

        using (args.PushGroup(nameof(KnowledgeGrantOnWearComponent)))
        {
            args.PushMarkup(Loc.GetString("knowledge-grant-on-wear-examine"));
            foreach (var (id, level) in ent.Comp.Skills)
            {
                if (!AllKnowledges.ContainsKey(id) || !_prototypes.TryIndex(id, out var prototype))
                    continue;

                args.PushMarkup(Loc.GetString(
                    level < 0 ? "knowledge-grant-on-wear-examine-negative" : "knowledge-grant-on-wear-examine-positive",
                    ("skill", prototype.Name),
                    ("level", level)));
            }
        }
    }

    private void OnChipInserted(Entity<KnowledgeGrantOnWearComponent> ent, ref OrganChipInsertedEvent args)
    {
        if (args.Body is { } body)
            ApplyWearModifiers(body, ent);
    }

    private void OnChipRemoved(Entity<KnowledgeGrantOnWearComponent> ent, ref OrganChipRemovedEvent args)
    {
        if (args.Body is { } body)
            RemoveWearModifiers(body, ent);
    }

    public void ApplyWearModifiers(EntityUid wearer, Entity<KnowledgeGrantOnWearComponent> ent)
    {
        if (GetContainer(wearer) is { } store)
            ApplyWearModifiers(wearer, store, ent);
    }

    public void ApplyWearModifiers(
        EntityUid wearer,
        Entity<KnowledgeContainerComponent> store,
        Entity<KnowledgeGrantOnWearComponent> ent)
    {
        if (!SkillsEnabled || TerminatingOrDeleted(wearer))
            return;

        var disabled = TryComp<CyberneticsComponent>(ent.Owner, out var cybernetics) && cybernetics.Disabled;

        if (disabled || !_conditions.TryConditions(wearer, ent.Comp.Conditions))
        {
            RemoveAllTemporaryModifiers(store, ent.Owner);
            return;
        }

        foreach (var (id, level) in ent.Comp.Skills)
        {
            if (!AllKnowledges.ContainsKey(id))
            {
                Log.Error($"{ToPrettyString(ent)} grants unknown skill {id}.");
                continue;
            }

            SetTemporaryModifier(store, id, ent.Owner, level);
        }
    }

    public void RemoveWearModifiers(EntityUid wearer, Entity<KnowledgeGrantOnWearComponent> ent)
    {
        if (TerminatingOrDeleted(wearer) || GetContainer(wearer) is not { } store)
            return;

        RemoveAllTemporaryModifiers(store, ent.Owner);
    }
}
