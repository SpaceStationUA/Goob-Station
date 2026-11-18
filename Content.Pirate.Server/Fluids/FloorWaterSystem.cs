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
    private readonly HashSet<EntityUid> _pendingPuddles = [];

    private EntityQuery<FloorWaterComponent> _floorWaterQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FloorWaterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FloorWaterComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<FloorWaterComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<FloorWaterComponent, AnchorStateChangedEvent>(OnWaterAnchored);
        SubscribeLocalEvent<FloorWaterComponent, MoveEvent>(OnWaterMoved);
        SubscribeLocalEvent<PuddleComponent, ComponentStartup>(OnPuddleStartup);
        SubscribeLocalEvent<PuddleComponent, MoveEvent>(OnPuddleMoved);
        SubscribeLocalEvent<PuddleComponent, EntityUnpausedEvent>(OnPuddleUnpaused);

        _floorWaterQuery = GetEntityQuery<FloorWaterComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    private void OnMapInit(Entity<FloorWaterComponent> ent, ref MapInitEvent args)
    {
        _waterBridge.UpdateWater(ent.Owner);
        QueuePuddlesOnWater(ent.Owner);
    }

    private void OnPuddleStartup(Entity<PuddleComponent> ent, ref ComponentStartup args)
        => _pendingPuddles.Add(ent.Owner);

    private void OnPuddleUnpaused(Entity<PuddleComponent> ent, ref EntityUnpausedEvent args)
        => _pendingPuddles.Add(ent.Owner);

    private void OnPuddleMoved(Entity<PuddleComponent> ent, ref MoveEvent args)
    {
        if (!args.OnlyRotation)
            _pendingPuddles.Add(ent.Owner);
    }

    private void OnWaterMoved(Entity<FloorWaterComponent> ent, ref MoveEvent args)
    {
        if (!args.OnlyRotation)
            QueuePuddlesOnWater(ent.Owner);
    }

    private void OnWaterAnchored(Entity<FloorWaterComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            QueuePuddlesOnWater(ent.Owner);
    }

    private void QueuePuddlesOnWater(EntityUid uid)
    {
        var xform = Transform(uid);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (HasComp<PuddleComponent>(other.Value))
                _pendingPuddles.Add(other.Value);
        }
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

        foreach (var uid in _pendingPuddles)
        {
            if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid is not { } gridUid ||
                !_gridQuery.TryComp(gridUid, out var grid) || !HasComp<PuddleComponent>(uid) || Paused(uid))
                continue;

            if (IsOnWater((gridUid, grid), _map.TileIndicesFor(gridUid, grid, xform.Coordinates)))
                QueueDel(uid);
        }
        _pendingPuddles.Clear();
    }

    private bool IsOnWater(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntities(grid, grid.Comp, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_floorWaterQuery.HasComp(ent))
                return true;
        }

        return false;
    }
}
