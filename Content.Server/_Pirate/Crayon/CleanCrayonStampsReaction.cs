// SPDX-License-Identifier: AGPL-3.0-only
// Pirate: adapted from AU-14/ColonialMarinesUniverse#2179.

using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared._Pirate.Crayon;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Map;

namespace Content.Server._Pirate.Crayon;

[DataDefinition]
public sealed partial class CleanCrayonStampsReaction : ITileReaction
{
    [DataField]
    public FixedPoint2 CleanCost { get; private set; } = FixedPoint2.New(0.25f);

    public FixedPoint2 TileReact(TileRef tile, ReagentPrototype reagent, FixedPoint2 reactVolume, IEntityManager entityManager, List<ReagentData>? data)
    {
        var lookup = entityManager.System<EntityLookupSystem>();
        var amount = FixedPoint2.Zero;
        foreach (var uid in lookup.GetLocalEntitiesIntersecting(tile, 0.5f))
        {
            if (amount + CleanCost > reactVolume)
                break;

            if (!entityManager.HasComponent<CrayonStampComponent>(uid))
                continue;

            entityManager.QueueDeleteEntity(uid);
            amount += CleanCost;
        }

        return amount;
    }
}
