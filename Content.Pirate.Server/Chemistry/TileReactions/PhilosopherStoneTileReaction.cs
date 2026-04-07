using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Server.EntityEffects;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Pirate.Server.Chemistry.TileReactions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class PhilosopherStoneTileReaction : ITileReaction
{
    [DataField]
    public float RequiredVolume = 4f;

    public FixedPoint2 TileReact(TileRef tile, ReagentPrototype reagent, FixedPoint2 reactVolume, IEntityManager entityManager, List<ReagentData>? data = null)
    {
        var required = FixedPoint2.New(RequiredVolume);
        if (reactVolume < required)
            return FixedPoint2.Zero;

        return entityManager.System<PirateEntityEffectSystem>().TryApplyPhilosopherStoneTileTransmutation(tile)
            ? required
            : FixedPoint2.Zero;
    }
}
