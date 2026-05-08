// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Pirate.Shared.EntityEffects.EffectConditions;

/// <summary>
/// Condition that checks if the blood contains VampireToxin marker.
/// This is used to prevent metabolism effects on vampire blood.
/// </summary>
public sealed partial class HasVampireToxin : EntityEffectCondition
{
    [DataField]
    public bool Invert = false;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        // This condition requires access to the reagent data, which is only available in EntityEffectReagentArgs
        if (args is not EntityEffectReagentArgs reagentArgs || reagentArgs.Source == null || reagentArgs.Reagent == null)
            return false;

        // Check the specific reagent entry being metabolized for VampireToxin marker.
        var hasVampireToxin = reagentArgs.Source.Contents
            .Any(reagentEntry => reagentEntry.Reagent.Prototype == reagentArgs.Reagent.ID
                && reagentEntry.Reagent.EnsureReagentData().OfType<DnaData>().Any(dna => dna.VampireToxin));

        return hasVampireToxin ^ Invert;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-guidebook-has-vampire-toxin", ("invert", Invert));
    }
}
