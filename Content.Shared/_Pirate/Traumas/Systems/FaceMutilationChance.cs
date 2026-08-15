using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body.Part;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas;

/// <summary>
/// Pirate face mutilation chance: head wounds become more likely to mutilate as integrity falls.
/// </summary>
public sealed partial class FaceMutilationChance : TraumaChance
{
    public override FixedPoint2? Calculate(in TraumaChanceArgs args)
    {
        if (args.Part.PartType != BodyPartType.Head
            || args.Target.Comp.WoundableSeverity < WoundableSeverity.Moderate
            || args.Target.Comp.WoundableSeverity == WoundableSeverity.Severed
            || args.Target.Comp.IntegrityCap <= 0)
            return null;

        var trauma = args.EntityManager.System<TraumaSystem>();
        if (trauma.HasWoundableTrauma(args.Target, TraumaSystem.FaceMutilation, args.Target.Comp))
            return null;

        var damageFraction = FixedPoint2.Clamp(
            1f - args.Target.Comp.WoundableIntegrity / args.Target.Comp.IntegrityCap,
            0,
            1);
        return damageFraction * 0.6f;
    }
}
