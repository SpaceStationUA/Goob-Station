using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

/// <summary>
/// Condition that checks if the player is a specific species.
/// Use Invert = true to check if the player is NOT the species.
/// </summary>
[DataDefinition]
public sealed partial class IsSpeciesCondition : BaseTraitCondition
{
    /// <summary>
    /// The species IDs to check for.
    /// </summary>
    [DataField("species", required: true)]
    public List<ProtoId<SpeciesPrototype>> Species = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.SpeciesId))
            return false;

        return Species.Any(s => s.Id.Equals(ctx.SpeciesId, StringComparison.OrdinalIgnoreCase));
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        var speciesNames = new List<string>();
        foreach (var species in Species)
        {
            if (proto.TryIndex(species, out var speciesProto))
                speciesNames.Add(loc.GetString(speciesProto.Name));
            else
                speciesNames.Add(species.Id);
        }

        var speciesList = string.Join(", ", speciesNames);

        return Invert
            ? loc.GetString("trait-condition-species-not", ("species", speciesList))
            : loc.GetString("trait-condition-species-is", ("species", speciesList));
    }
}


