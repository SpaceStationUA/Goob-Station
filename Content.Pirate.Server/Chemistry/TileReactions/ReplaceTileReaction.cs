using Content.Server.Fluids.EntitySystems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Maps;
using JetBrains.Annotations;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Chemistry.TileReactions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class ReplaceTileReaction : ITileReaction
{
    [DataField(required: true)]
    public string Tile = string.Empty;

    [DataField]
    public float RequiredVolume = 3f;

    public FixedPoint2 TileReact(TileRef tile, ReagentPrototype reagent, FixedPoint2 reactVolume, IEntityManager entityManager, List<ReagentData>? data = null)
    {
        if (RequiredVolume <= 0)
        {
            Logger.WarningS("pirate.tile-reaction", $"Skipping {nameof(ReplaceTileReaction)} for tile '{Tile}' because {nameof(RequiredVolume)} is {RequiredVolume}.");
            return FixedPoint2.Zero;
        }

        var required = FixedPoint2.New(RequiredVolume);
        if (reactVolume < required)
            return FixedPoint2.Zero;

        var prototype = IoCManager.Resolve<IPrototypeManager>().Index<ContentTileDefinition>(Tile);
        entityManager.System<TileSystem>().ReplaceTile(tile, prototype);
        return required;
    }
}
