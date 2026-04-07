using Content.Server.Fluids.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Maps;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Pirate.Server.Chemistry.TileReactions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class BubbleGasTileReaction : ITileReaction
{
    [DataField]
    public float RequiredVolume = 5f;

    [DataField]
    public float Duration = 8f;

    [DataField]
    public int SpreadAmount = 2;

    [DataField]
    public string PrototypeId = "Smoke";

    [DataField]
    public string CloudReagent = "AlchemistBubbleGasCloud";

    public FixedPoint2 TileReact(TileRef tile, ReagentPrototype reagent, FixedPoint2 reactVolume, IEntityManager entityManager, List<ReagentData>? data = null)
    {
        var required = FixedPoint2.New(RequiredVolume);
        if (reactVolume < required)
            return FixedPoint2.Zero;

        if (!entityManager.TryGetComponent<MapGridComponent>(tile.GridUid, out _))
            return FixedPoint2.Zero;

        var turf = entityManager.System<TurfSystem>();
        var coords = turf.GetTileCenter(tile);
        var smoke = entityManager.SpawnEntity(PrototypeId, coords);
        if (!entityManager.TryGetComponent<SmokeComponent>(smoke, out var smokeComp))
        {
            entityManager.QueueDeleteEntity(smoke);
            return FixedPoint2.Zero;
        }

        var solution = new Solution(CloudReagent, required);
        entityManager.System<SmokeSystem>().StartSmoke(smoke, solution, Duration, SpreadAmount, smokeComp);
        return required;
    }
}
