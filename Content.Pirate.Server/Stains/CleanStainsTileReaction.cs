using System.Linq;
using Content.Pirate.Shared.Stains.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Pirate.Server.Stains;

/// <summary>
/// Cleans stains from entities touched by a cleaning tile reaction.
/// </summary>
[DataDefinition]
public sealed partial class CleanStainsTileReaction : ITileReaction
{
    FixedPoint2 ITileReaction.TileReact(TileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager,
        List<ReagentData>? data)
    {
        var lookup = entityManager.System<EntityLookupSystem>();
        var stains = entityManager.System<StainSystem>();

        foreach (var entity in lookup.GetLocalEntitiesIntersecting(tile, 0f).ToArray())
        {
            if (entityManager.HasComponent<InventoryComponent>(entity))
                stains.CleanEntityAndEquipment(entity);
            else if (entityManager.HasComponent<StainableComponent>(entity))
                stains.TryCleanStain(entity);
        }

        return FixedPoint2.Zero;
    }
}
