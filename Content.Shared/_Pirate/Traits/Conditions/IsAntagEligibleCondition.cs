using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

/// <summary>
/// Condition that checks if the player has enabled a specific antag preference.
/// </summary>
[DataDefinition]
public sealed partial class IsAntagEligibleCondition : BaseTraitCondition
{
    /// <summary>
    /// The antag prototype IDs to check for eligibility.
    /// </summary>
    [DataField("antags", required: true)]
    public List<ProtoId<AntagPrototype>> Antags = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (ctx.Profile == null)
            return false;

        foreach (var antagId in Antags)
        {
            if (ctx.Profile.AntagPreferences.Contains(antagId))
                return true;
        }

        return false;
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        var antagNames = new List<string>();

        foreach (var antagId in Antags)
        {
            if (proto.TryIndex(antagId, out var antagProto))
                antagNames.Add(loc.GetString(antagProto.Name));
            else
                antagNames.Add(antagId.Id);
        }

        var antagList = string.Join(", ", antagNames);

        return Invert
            ? loc.GetString("trait-condition-antag-not", ("antag", antagList))
            : loc.GetString("trait-condition-antag-is", ("antag",  antagList));
    }
}


