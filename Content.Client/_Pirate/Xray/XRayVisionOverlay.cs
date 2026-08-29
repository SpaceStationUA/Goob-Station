// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from space-wizards/space-station-14#44601 ("Mesons (XRayVision)").
// Uses CPU tile raycasts because the engine has no FOV shadow-map access.

using Content.Shared._Pirate.Xray;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;

namespace Content.Client._Pirate.Xray;

public sealed class XRayVisionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly ProfManager _prof = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _map;
    private readonly SharedPhysicsSystem _physics;
    private readonly SharedTransformSystem _transform;

    private readonly EntityQuery<OccluderComponent> _occluderQuery;
    private readonly EntityQuery<TransformComponent> _transformQuery;

    private static readonly ProtoId<ShaderPrototype> Shader = "XRayVision";
    private readonly ShaderInstance _tileShader;

    public const int ContentZIndex = Content.Client.Light.BeforeLightTargetOverlay.ContentZIndex + 1;

    private List<Entity<MapGridComponent>> _grids = [];
    private readonly Dictionary<Tile, Dictionary<byte, Texture>> _tileVariations = [];
    private readonly Dictionary<EntityUid, GridVisibilityCache> _visibilityCache = [];

    private readonly Func<EntityUid, bool> _ignoreNonOccluder;

    public bool ShowTiles { get; private set; }
    public float Range { get; private set; } = 10f;
    public float TileAlpha { get; private set; } = 0.2f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public XRayVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = ContentZIndex;
        _tileShader = _prototypeManager.Index(Shader).InstanceUnique();
        _lookup = _entManager.System<EntityLookupSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _physics = _entManager.System<SharedPhysicsSystem>();
        _transform = _entManager.System<SharedTransformSystem>();
        _occluderQuery = _entManager.GetEntityQuery<OccluderComponent>();
        _transformQuery = _entManager.GetEntityQuery<TransformComponent>();
        _ignoreNonOccluder = IgnoreNonOccluder;
    }

    public void SetParameters(bool showTiles, float range, float tileAlpha)
    {
        if (!MathHelper.CloseTo(Range, range))
            _visibilityCache.Clear();

        ShowTiles = showTiles;
        Range = range;
        TileAlpha = tileAlpha;
    }

    public void InvalidateVisibility(EntityUid? grid = null)
    {
        if (grid == null)
            _visibilityCache.Clear();
        else
            _visibilityCache.Remove(grid.Value);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewer = _player.LocalSession?.AttachedEntity;
        if (viewer == null)
            return;

        if (!_transformQuery.TryGetComponent(viewer.Value, out var viewerXform))
            return;

        if (viewerXform.MapID != args.MapId)
            return;

        if (args.Viewport.Eye == null)
            return;

        if (!ShowTiles)
            return;

        var handle = args.WorldHandle;

        // The unshaded shader prevents FOV from blacking out revealed tiles.
        handle.UseShader(_tileShader);
        DrawTiles(args, handle, viewerXform);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTiles(in OverlayDrawArgs args, DrawingHandleWorld handle, TransformComponent viewerXform)
    {
        using var _ = _prof.Group("XRayVisionOverlay.DrawTiles");

        var eyePos = _transform.GetWorldPosition(viewerXform);

        // Limit raycasts to visible tiles within range.
        var bounds = args.WorldAABB.Intersect(Box2.CenteredAround(eyePos, new Vector2(Range * 2f)));
        if (bounds.IsEmpty())
            return;

        var rangeSquared = Range * Range;
        var (eyeTile, rayOrigin) = GetEyeTile(eyePos, viewerXform);
        var occluderSignature = GetOccluderSignature(args.MapId, rayOrigin);

        // Dim unshaded tiles without tinting them.
        var modulate = Color.White.WithAlpha(TileAlpha);

        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId, bounds, ref _grids);

        foreach (var grid in _grids)
        {
            var gridWorldMatrix = _transform.GetWorldMatrix(grid.Owner);
            handle.SetTransform(gridWorldMatrix);
            var revealedTiles = GetRevealedTiles(args.MapId, grid, eyeTile, rayOrigin, rangeSquared, occluderSignature);

            foreach (var tileRef in _map.GetTilesIntersecting(grid.Owner, grid.Comp, bounds))
            {
                if (!revealedTiles.Contains(tileRef.GridIndices))
                    continue;

                if (!_tileDefManager.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef) || tileDef.Sprite is not { } sprite)
                    continue;

                var texture = GetTileTexture(tileRef.Tile, tileDef, sprite);
                handle.DrawTextureRect(texture, _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize), modulate);
            }
        }
    }

    private HashSet<Vector2i> GetRevealedTiles(
        MapId mapId,
        Entity<MapGridComponent> grid,
        EyeTile eyeTile,
        Vector2 rayOrigin,
        float rangeSquared,
        int occluderSignature)
    {
        if (_visibilityCache.TryGetValue(grid.Owner, out var cached)
            && cached.EyeTile == eyeTile
            && cached.OccluderSignature == occluderSignature)
            return cached.RevealedTiles;

        var revealed = new HashSet<Vector2i>();
        var rangeBounds = Box2.CenteredAround(rayOrigin, new Vector2(Range * 2f));
        var (gridPos, gridRot) = _transform.GetWorldPositionRotation(grid.Owner);

        foreach (var tileRef in _map.GetTilesIntersecting(grid.Owner, grid.Comp, rangeBounds))
        {
            if (tileRef.Tile.IsEmpty
                || !_tileDefManager.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef)
                || tileDef.Sprite == null
                || TileHasOccluder(grid, tileRef.GridIndices))
            {
                continue;
            }

            var tileLocalCenter = _map.ToCenterCoordinates(tileRef, grid.Comp).Position;
            var tileCenter = gridPos + gridRot.RotateVec(tileLocalCenter);

            if (Vector2.DistanceSquared(rayOrigin, tileCenter) <= rangeSquared
                && IsHidden(mapId, rayOrigin, tileCenter))
            {
                revealed.Add(tileRef.GridIndices);
            }
        }

        _visibilityCache[grid.Owner] = new GridVisibilityCache(eyeTile, occluderSignature, revealed);
        return revealed;
    }

    private int GetOccluderSignature(MapId mapId, Vector2 eyePos)
    {
        var rangeBounds = Box2.CenteredAround(eyePos, new Vector2(Range * 2f));
        var hash = new HashCode();
        var query = _entManager.EntityQueryEnumerator<OccluderComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var occluder, out var xform))
        {
            if (!occluder.Enabled || xform.MapID != mapId)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (!rangeBounds.Contains(position))
                continue;

            hash.Add(uid);
            hash.Add(position);
            hash.Add(occluder.BoundingBox);
        }

        return hash.ToHashCode();
    }

    private (EyeTile Tile, Vector2 Position) GetEyeTile(Vector2 eyePos, TransformComponent viewerXform)
    {
        if (viewerXform.GridUid is { } gridUid
            && _entManager.TryGetComponent<MapGridComponent>(gridUid, out var grid))
        {
            var indices = _map.WorldToTile(gridUid, grid, eyePos);
            var center = _map.ToCenterCoordinates(gridUid, indices, grid);
            return (new EyeTile(gridUid, indices), _transform.ToMapCoordinates(center).Position);
        }

        var mapIndices = new Vector2i((int) MathF.Floor(eyePos.X), (int) MathF.Floor(eyePos.Y));
        return (new EyeTile(null, mapIndices), mapIndices + new Vector2(0.5f));
    }

    private bool IsHidden(MapId mapId, Vector2 eyePos, Vector2 target)
    {
        var delta = target - eyePos;
        var distance = delta.Length();

        if (distance <= float.Epsilon)
            return false;

        var ray = new CollisionRay(eyePos, delta / distance, (int) CollisionGroup.Opaque);
        return _physics.IntersectRayWithPredicate(mapId, ray, distance, _ignoreNonOccluder).Any();
    }

    private bool IgnoreNonOccluder(EntityUid uid)
    {
        return !_occluderQuery.TryGetComponent(uid, out var occluder) || !occluder.Enabled;
    }

    private bool TileHasOccluder(Entity<MapGridComponent> grid, Vector2i indices)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid.Owner, grid.Comp, indices);
        while (anchored.MoveNext(out var ent))
        {
            if (_occluderQuery.TryGetComponent(ent, out var occluder) && occluder.Enabled)
                return true;
        }

        return false;
    }

    /// <summary>Gets a cached atlas slice for a tile variant.</summary>
    private Texture GetTileTexture(Tile tile, ITileDefinition tileDef, ResPath sprite)
    {
        if (_tileVariations.TryGetValue(tile, out var variants) && variants.TryGetValue(tile.Variant, out var cached))
            return cached;

        var atlas = _resCache.GetResource<TextureResource>(sprite);

        Texture texture;
        if (tileDef.Variants <= 1)
        {
            texture = atlas;
        }
        else
        {
            var size = atlas.Texture.Size.X / tileDef.Variants;
            var variant = tile.Variant % tileDef.Variants;
            var variantBounds = UIBox2.FromDimensions(variant * size, 0, size, atlas.Texture.Size.Y);
            texture = new AtlasTexture(atlas, variantBounds);
        }

        if (!_tileVariations.TryGetValue(tile, out variants))
        {
            variants = [];
            _tileVariations[tile] = variants;
        }

        variants[tile.Variant] = texture;
        return texture;
    }

    private readonly record struct EyeTile(EntityUid? Grid, Vector2i Indices);

    private sealed record GridVisibilityCache(EyeTile EyeTile, int OccluderSignature, HashSet<Vector2i> RevealedTiles);
}
