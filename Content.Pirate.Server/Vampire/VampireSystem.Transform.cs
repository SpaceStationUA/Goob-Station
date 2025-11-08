using Content.Server.Body.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Nutrition.Components;
using Content.Pirate.Shared.Vampire;
using Content.Pirate.Shared.Vampire.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry;

namespace Content.Pirate.Server.Vampire;

public sealed partial class VampireSystem
{

    /// <summary>
    /// Add vulnerability to holy water when ingested or slashed, and take damage from the bible
    /// </summary>
    public void MakeVulnerableToHoly(Entity<VampireComponent> vampire)
    {
        //React to being beaten with the bible
        // TODO: Add UnholyComponent when available
        // EnsureComp<UnholyComponent>(vampire);

        //Take damage from holy water splash
        if (TryComp<ReactiveComponent>(vampire, out var reactive))
        {
            if (reactive.ReactiveGroups == null)
                reactive.ReactiveGroups = new();

            if (!reactive.ReactiveGroups.ContainsKey("WeakToHoly"))
            {
                reactive.ReactiveGroups.Add("WeakToHoly", new() { ReactionMethod.Touch });
            }
        }

        if (!TryComp<BodyComponent>(vampire, out var bodyComponent))
            return;

        //Add vampire and bloodsucker to all metabolizing organs
        //And restrict diet to Pills (and liquids)
        foreach (var organ in _body.GetBodyOrgans(vampire, bodyComponent))
        {
            if (TryComp<MetabolizerComponent>(organ.Id, out var metabolizer))
            {
                if (TryComp<StomachComponent>(organ.Id, out var stomachComponent))
                {
                    //Override the stomach, prevents humans getting sick when ingesting blood
                    stomachComponent.SpecialDigestible = VampireComponent.AcceptableFoods;
                    stomachComponent.IsSpecialDigestibleExclusive = true;
                }

                // Set metabolizer types for vampire metabolism
                var newTypes = new HashSet<ProtoId<MetabolizerTypePrototype>>();
                if (metabolizer.MetabolizerTypes != null)
                {
                    foreach (var type in metabolizer.MetabolizerTypes)
                        newTypes.Add(type);
                }
                newTypes.Add(VampireComponent.MetabolizerVampire);
                newTypes.Add(VampireComponent.MetabolizerBloodsucker);

                _metabolism.SetMetabolizerTypes((organ.Id, metabolizer), newTypes);
            }
        }
    }

    public void AddStartingAbilities(EntityUid vampire)
    {
        if (!TryComp<VampireComponent>(vampire, out var comp))
            return;

        foreach (var actionId in comp.BaseVampireActions)
        {
            var action = _action.AddAction(vampire, actionId);

            if (!action.HasValue)
                return;

            if (TryComp<InstantActionComponent>(action, out var instantActionComponent))
            {
                if (instantActionComponent.Event is VampireSelfPowerEvent instantActionEvent)
                {
                    comp.UnlockedPowers.Add(instantActionEvent.DefinitionName.Id, GetNetEntity(action));
                }
            }

            if (TryComp<EntityTargetActionComponent>(action, out var entityActionComponent))
            {
                if (entityActionComponent.Event is VampireTargetedPowerEvent entityActionEvent)
                {
                    comp.UnlockedPowers.Add(entityActionEvent.DefinitionName.Id, GetNetEntity(action));
                }
            }
        }

        UpdateBloodDisplay(vampire);
    }

}
