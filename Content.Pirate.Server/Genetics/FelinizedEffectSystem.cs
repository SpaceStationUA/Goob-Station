// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared.Genetics;
using Content.Pirate.Shared.Genetics.EntityEffects;
using Content.Server.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Genetics;

[RegisterComponent]
public sealed partial class GeneticsFelinizedComponent : Component
{
    public EntProtoId? OriginalEars;
    public List<Marking> OriginalSpecial = new();
    public EntityUid? Ears;
    public EntityUid? Tail;
}

public sealed class FelinizedEffectSystem : EntityEffectSystem<HumanoidAppearanceComponent, SetFelinized>
{
    [Dependency] private readonly GeneticsBodySystem _geneticsBody = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _appearance = default!;

    protected override void Effect(Entity<HumanoidAppearanceComponent> ent, ref EntityEffectEvent<SetFelinized> args)
    {
        if (args.Effect.Revert)
        {
            if (!TryComp<GeneticsFelinizedComponent>(ent, out var previous))
                return;

            if (previous.Ears is {} ears && Exists(ears))
                Del(ears);
            if (previous.Tail is {} tail && Exists(tail))
                Del(tail);
            if (previous.OriginalEars is {} original && _geneticsBody.GetOrgan(ent, "Head") is {} head)
            {
                Insert(head, "ears", original);
            }

            ent.Comp.MarkingSet.RemoveCategory(MarkingCategories.Special);
            foreach (var marking in previous.OriginalSpecial)
                ent.Comp.MarkingSet.AddBack(MarkingCategories.Special, marking);
            ent.Comp.MarkingSet.Remove(MarkingCategories.Tail, "GeneticsCatTail");
            // Donor behavior intentionally removes the cat tail without regrowing a former tail.
            ChangeMetabolism(ent, "Animal", "Human");
            RemComp<GeneticsFelinizedComponent>(ent);
            Dirty(ent);
            return;
        }

        if (HasComp<GeneticsFelinizedComponent>(ent) ||
            _geneticsBody.GetOrgan(ent, "Head") is not {} targetHead ||
            _geneticsBody.GetOrgan(ent, "Chest") is not {} chest)
            return;

        var state = AddComp<GeneticsFelinizedComponent>(ent);
        if (ent.Comp.MarkingSet.Markings.TryGetValue(MarkingCategories.Special, out var special))
            state.OriginalSpecial = special.Select(marking => new Marking(marking)).ToList();

        if (_geneticsBody.GetOrgan(ent, "Ears") is {} oldEars)
        {
            state.OriginalEars = MetaData(oldEars).EntityPrototype?.ID;
            Del(oldEars);
        }
        if (_geneticsBody.GetOrgan(ent, "Tail") is {} oldTail)
            _geneticsBody.Detach(oldTail);

        state.Ears = Insert(targetHead, "ears", "OrganCatEars");
        state.Tail = Insert(chest, "tail", "OrganCatTail");
        // Local anatomy has no VisualOrgan system: use the original donor sprites as forced markings.
        ent.Comp.MarkingSet.RemoveCategory(MarkingCategories.Special);
        ent.Comp.MarkingSet.RemoveCategory(MarkingCategories.Tail);
        _appearance.AddMarking(ent, "GeneticsCatEars", Color.White, forced: true);
        _appearance.AddMarking(ent, "GeneticsCatTail", Color.White, forced: true);
        ChangeMetabolism(ent, "Human", "Animal");
        Dirty(ent);
    }

    private EntityUid Insert(EntityUid part, string slot, EntProtoId prototype)
    {
        _body.TryCreateOrganSlot(part, slot, out _);
        return SpawnInContainerOrDrop(prototype, part, SharedBodySystem.GetOrganContainerId(slot));
    }

    private void ChangeMetabolism(EntityUid body, string removed, string added)
    {
        foreach (var (uid, _) in _body.GetBodyOrgans(body))
        {
            if (!TryComp<MetabolizerComponent>(uid, out var metabolizer))
                continue;
            metabolizer.MetabolizerTypes ??= new();
            metabolizer.MetabolizerTypes.Remove(removed);
            metabolizer.MetabolizerTypes.Add(added);
        }
    }
}
