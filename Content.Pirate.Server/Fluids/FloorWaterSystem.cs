using Content.Pirate.Shared.Fluids;
using Content.Pirate.Shared.Wetness.Systems;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Map.Components;

namespace Content.Pirate.Server.Fluids;

public sealed class FloorWaterSystem : EntitySystem
{
    [Dependency] private readonly SharedWetnessSystem _wetness = null!;
    [Dependency] private readonly SharedMapSystem _map = null!;
    [Dependency] private readonly WaterBridgeSystem _waterBridge = null!;

    private const float AbsorbInterval = 1f;

    private float _absorbAccumulator;

    private EntityQuery<FloorWaterComponent> _floorWaterQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FloorWaterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FloorWaterComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<FloorWaterComponent, StepTriggeredOffEvent>(OnStepTriggered);

        _floorWaterQuery = GetEntityQuery<FloorWaterComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    private void OnMapInit(Entity<FloorWaterComponent> ent, ref MapInitEvent args)
    {
        _waterBridge.UpdateWater(ent.Owner);
    }

    private void OnStepAttempt(Entity<FloorWaterComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    private void OnStepTriggered(Entity<FloorWaterComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (HasComp<InventoryComponent>(args.Tripper))
            _wetness.ImmerseInWater(args.Tripper, ent.Comp.ImmersionFlow);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _absorbAccumulator += frameTime;
        if (_absorbAccumulator < AbsorbInterval)
            return;

        _absorbAccumulator = 0f;

        // Scan puddles because water tiles are more numerous.
        var query = EntityQueryEnumerator<PuddleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
                continue;

            if (IsOnWater((gridUid, grid), _map.TileIndicesFor(gridUid, grid, xform.Coordinates)))
                QueueDel(uid);
        }
    }

    private bool IsOnWater(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, grid.Comp, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_floorWaterQuery.HasComp(ent))
                return true;
        }

        return false;
    }
}
