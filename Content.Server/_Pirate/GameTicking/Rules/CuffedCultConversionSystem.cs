// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._DV.CosmicCult;
using Content.Shared._DV.CosmicCult;
using Content.Shared._DV.CosmicCult.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;

namespace Content.Server._Pirate.GameTicking.Rules;

/// <summary>
/// Allows a single cosmic cultist to invoke a conversion glyph when its target is fully cuffed.
/// </summary>
public sealed class CuffedCultConversionSystem : EntitySystem
{
    [Dependency] private readonly SharedCuffableSystem _cuffable = default!;
    [Dependency] private readonly SharedCosmicCultSystem _cosmicCult = default!;
    [Dependency] private readonly CosmicGlyphSystem _glyph = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicGlyphConversionComponent, GetCosmicGlyphCultistRequirementEvent>(OnGetRequirement);
    }

    private void OnGetRequirement(
        Entity<CosmicGlyphConversionComponent> ent,
        ref GetCosmicGlyphCultistRequirementEvent args)
    {
        var targets = _glyph.GetTargetsNearGlyph(
            ent,
            ent.Comp.ConversionRange,
            target => _cosmicCult.EntityIsCultist(target));

        if (targets.Count != 1)
            return;

        foreach (var target in targets)
        {
            if (TryComp<CuffableComponent>(target.Owner, out var cuffable) &&
                _cuffable.IsCuffed((target.Owner, cuffable)))
            {
                args.RequiredCultists = 1;
            }
        }
    }
}

[ByRefEvent]
public record struct GetCosmicGlyphCultistRequirementEvent(int RequiredCultists);
