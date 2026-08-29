using Content.Pirate.Shared.Construction;
using Content.Server.Construction;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Construction;

public sealed class AlignToAdjacentWallsSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlignToAdjacentWallsComponent, AfterConstructionChangeEntityEvent>(OnConstructed);
    }

    private void OnConstructed(Entity<AlignToAdjacentWallsComponent> ent, ref AfterConstructionChangeEntityEvent args)
    {
        Align(ent);
    }

    /// <summary>Aligns a constructed entity with the dominant adjacent-wall axis.</summary>
    public void Align(Entity<AlignToAdjacentWallsComponent> ent)
    {
        var xform = Transform(ent);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _maps.TileIndicesFor(gridUid, grid, xform.Coordinates);

        var northSouth = HasWall(gridUid, grid, origin + new Vector2i(0, 1))
                       + HasWall(gridUid, grid, origin + new Vector2i(0, -1));
        var eastWest = HasWall(gridUid, grid, origin + new Vector2i(1, 0))
                     + HasWall(gridUid, grid, origin + new Vector2i(-1, 0));

        if (northSouth == eastWest)
            return;

        var degrees = northSouth > eastWest ? ent.Comp.AlongNorthSouth : ent.Comp.AlongEastWest;
        _transform.SetLocalRotation(ent, Angle.FromDegrees(degrees));
    }

    private int HasWall(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _maps.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);

        while (anchored.MoveNext(out var uid))
        {
            if (_tags.HasTag(uid.Value, WallTag))
                return 1;
        }

        return 0;
    }
}
