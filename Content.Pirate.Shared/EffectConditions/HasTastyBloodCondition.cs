using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Pirate.Shared.EntityEffects.EffectConditions;

/// <summary>
/// Condition that checks if the blood contains the TastyBlood marker.
/// </summary>
public sealed partial class HasTastyBlood : EntityEffectCondition
{
    [DataField]
    public bool Invert = false;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs || reagentArgs.Source == null || reagentArgs.Reagent == null)
            return false;

        var hasTastyBlood = reagentArgs.Source.Contents
            .Any(reagentEntry => reagentEntry.Reagent.Prototype == reagentArgs.Reagent.ID
                && reagentEntry.Reagent.EnsureReagentData().OfType<DnaData>().Any(dna => dna.TastyBlood));

        return hasTastyBlood ^ Invert;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-guidebook-has-tasty-blood", ("invert", Invert));
    }
}
