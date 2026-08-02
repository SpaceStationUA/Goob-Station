using System.Numerics;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Pirate.ZLevels.Core;

/// <summary>
/// Re-homes wall fixtures mapped over an empty tile onto the deck grid below them.
///
/// With no tile to snap to, the engine leaves them map-parented, unanchored and dynamic — fine in the
/// paused mapping view, but they drift/fall in a live round. At map-init we re-parent them onto the grid
/// they sit over and anchor (or pin Static over a hole). Only <see cref="FixtureWhitelist"/> entities are
/// touched.
/// </summary>
public sealed class ZOrphanedFixtureSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    // WallMount covers wallmounts/signs/machines; lights only carry the WallLight tag.
    private static readonly EntityWhitelist FixtureWhitelist = new()
    {
        Components = ["WallMount"],
        Tags = ["WallLight"],
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, TransformComponent xform, ref MapInitEvent args)
    {
        // Only map-parented (gridless) fixtures are orphans.
        if (xform.GridUid != null || xform.MapUid is not { } mapUid || xform.ParentUid != mapUid)
            return;

        if (!_whitelist.IsValid(FixtureWhitelist, uid))
            return;

        var worldPos = _transform.GetWorldPosition(xform);

        // Slightly fat AABB so a point over a hole still resolves to the surrounding deck grid.
        var aabb = new Box2(worldPos - new Vector2(0.15f, 0.15f), worldPos + new Vector2(0.15f, 0.15f));
        var grids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(xform.MapID, aabb, ref grids, approx: true, includeMap: false);
        if (grids.Count == 0)
            return;

        var deck = grids[0];

        // Re-parenting onto a terminating grid just errors out in the transform system.
        if (TerminatingOrDeleted(deck.Owner))
            return;

        // Without this SharedGridTraversalSystem snaps it back to the map (the hole tile reads as "no grid").
        xform.GridTraversal = false;

        var localPos = Vector2.Transform(worldPos, _transform.GetInvWorldMatrix(deck.Owner));
        _transform.SetCoordinates(uid, new EntityCoordinates(deck.Owner, localPos));

        // Solid tile: anchor. Hole: pin Static so it neither drifts nor z-falls.
        var tile = _map.TileIndicesFor(deck.Owner, deck.Comp, Transform(uid).Coordinates);
        if (_map.TryGetTileRef(deck.Owner, deck.Comp, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            _transform.AnchorEntity(uid, Transform(uid));
        }
        else if (TryComp<PhysicsComponent>(uid, out var body))
        {
            _physics.SetBodyType(uid, BodyType.Static, body: body);
        }
    }
}
