using Content.Pirate.Shared.Fluids;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Pirate.Server.Fluids;

/// <summary>Disables water collision while an anchored bridge shares its tile.</summary>
public sealed class WaterBridgeSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        // FloorWaterSystem owns the water-side MapInit sync.
        SubscribeLocalEvent<WaterBridgeComponent, MapInitEvent>(OnBridgeMapInit);
        SubscribeLocalEvent<WaterBridgeComponent, AnchorStateChangedEvent>(OnBridgeAnchorChanged);
        SubscribeLocalEvent<WaterBridgeComponent, EntityTerminatingEvent>(OnBridgeTerminating);
    }

    private void OnBridgeMapInit(Entity<WaterBridgeComponent> ent, ref MapInitEvent args)
    {
        UpdateWaterOnBridgeTile(ent.Owner);
    }

    private void OnBridgeAnchorChanged(Entity<WaterBridgeComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdateWaterOnBridgeTile(ent.Owner);
    }

    private void OnBridgeTerminating(Entity<WaterBridgeComponent> ent, ref EntityTerminatingEvent args)
    {
        // Ignore this bridge because it remains anchored during termination.
        UpdateWaterOnBridgeTile(ent.Owner, ent.Owner);
    }

    private void UpdateWaterOnBridgeTile(EntityUid bridge, EntityUid? ignore = null)
    {
        var xform = Transform(bridge);
        if (!TryGetTile(xform, out var gridUid, out var grid, out var tile))
            return;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (HasComp<FloorWaterComponent>(ent.Value))
                UpdateWater(ent.Value, ignore);
        }
    }

    public void UpdateWater(EntityUid water, EntityUid? ignore = null)
    {
        if (!TryComp<PhysicsComponent>(water, out var physics))
            return;

        var xform = Transform(water);
        if (!TryGetTile(xform, out var gridUid, out var grid, out var tile))
            return;

        var bridged = TileHasBridge(gridUid, grid, tile, ignore);
        _physics.SetCanCollide(water, !bridged, body: physics);
    }

    private bool TileHasBridge(EntityUid gridUid, MapGridComponent grid, Vector2i tile, EntityUid? ignore)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (ent.Value != ignore && HasComp<WaterBridgeComponent>(ent.Value))
                return true;
        }

        return false;
    }

    private bool TryGetTile(TransformComponent xform, out EntityUid gridUid, out MapGridComponent grid, out Vector2i tile)
    {
        gridUid = default;
        grid = default!;
        tile = default;

        if (xform.GridUid is not { } gUid || !TryComp(gUid, out MapGridComponent? gComp))
            return false;

        gridUid = gUid;
        grid = gComp;
        tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        return true;
    }
}
