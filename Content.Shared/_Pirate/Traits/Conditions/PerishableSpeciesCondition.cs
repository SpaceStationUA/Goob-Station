// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos.Rotting;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

[DataDefinition]
public sealed partial class PerishableSpeciesCondition : BaseTraitCondition
{
    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        return ctx.SpeciesId is { } id && ctx.Proto.TryIndex<SpeciesPrototype>(id, out var species)
            && ctx.Proto.Index(species.Prototype).HasComp<PerishableComponent>(ctx.CompFactory);
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
        => loc.GetString("trait-condition-perishable-species");
}
