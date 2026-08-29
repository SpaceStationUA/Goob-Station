// SPDX-License-Identifier: MIT
// Pirate: meson vision - ported from space-wizards/space-station-14#44601 ("Mesons (XRayVision)").
//
// Deviation from upstream, and the only one: upstream's shader decides visibility per-pixel by sampling the
// engine's FOV shadow-depth map, which it reaches through IClydeViewport.FovRenderTarget - added by
// RobustToolbox#6781, merged upstream 2026-07-10. Our engine is pinned at 270.1.0 (2026-01-01) and has no
// public path to that render target (Clyde._fovRenderTarget is private, and the sandbox whitelist has no
// System.Reflection.FieldInfo entry, so reflection is not an option either). We therefore resolve occlusion
// per-tile on the CPU with a raycast against the occluder set. Same rule as upstream - a tile is drawn only
// when line of sight to it is blocked - just at tile granularity instead of pixel granularity, and bounded by
// XRayVisionComponent.Range to keep the raycast count sane.

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

/// <summary>
/// Overlay that shows tiles hidden behind walls.
/// </summary>
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

    // Not readonly - FindGridsIntersecting takes this by ref.
    private List<Entity<MapGridComponent>> _grids = [];
    private readonly Dictionary<Tile, Dictionary<byte, Texture>> _tileVariations = [];

    /// <summary>
    /// Cached delegate so the per-tile raycast does not allocate a closure every call.
    /// </summary>
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
        ShowTiles = showTiles;
        Range = range;
        TileAlpha = tileAlpha;
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

        // Still routed through a shader (rather than drawn plain) purely for light_mode unshaded - without
        // it the lighting/FOV pass blackens the tiles straight back out. No tint of its own: revealed tiles
        // are meant to read as whatever color the goggles' full-screen shader is currently applying, same as
        // the rest of the screen - not a separate, distinctly-colored patch.
        handle.UseShader(_tileShader);
        DrawTiles(args, handle, viewerXform);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTiles(in OverlayDrawArgs args, DrawingHandleWorld handle, TransformComponent viewerXform)
    {
        using var _ = _prof.Group("XRayVisionOverlay.DrawTiles");

        var eyePos = _transform.GetWorldPosition(viewerXform);

        // Only consider what is both on screen and inside the reveal radius - every tile that survives this
        // costs a raycast.
        var bounds = args.WorldAABB.Intersect(Box2.CenteredAround(eyePos, new Vector2(Range * 2f)));
        if (bounds.IsEmpty())
            return;

        var rangeSquared = Range * Range;

        // Neutral white - alpha only, so this dims the tiles without shifting their hue. Unshaded tiles would
        // otherwise draw at full texture brightness and glare against the dimly-lit floor around them.
        var modulate = Color.White.WithAlpha(TileAlpha);

        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId, bounds, ref _grids);

        foreach (var grid in _grids)
        {
            var gridWorldMatrix = _transform.GetWorldMatrix(grid.Owner);
            var (gridPos, gridRot) = _transform.GetWorldPositionRotation(grid.Owner);
            handle.SetTransform(gridWorldMatrix);

            foreach (var tileRef in _map.GetTilesIntersecting(grid.Owner, grid.Comp, bounds))
            {
                if (tileRef.Tile.IsEmpty)
                    continue;

                if (!_tileDefManager.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef) || tileDef.Sprite is not { } sprite)
                    continue;

                // Skip tiles that have a wall on them - upstream does the same, so you see the floor beyond a
                // wall rather than the wall itself.
                if (TileHasOccluder(grid, tileRef.GridIndices))
                    continue;

                var tileLocalCenter = _map.ToCenterCoordinates(tileRef, grid.Comp).Position;
                var tileCenter = gridPos + gridRot.RotateVec(tileLocalCenter);

                if (Vector2.DistanceSquared(eyePos, tileCenter) > rangeSquared)
                    continue;

                // Only reveal what we cannot already see.
                if (!IsHidden(args.MapId, eyePos, tileCenter))
                    continue;

                var texture = GetTileTexture(tileRef.Tile, tileDef, sprite);
                handle.DrawTextureRect(texture, _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize), modulate);
            }
        }
    }

    /// <summary>
    /// CPU stand-in for upstream's FOV shadow-map lookup: true when line of sight from the eye to the point is
    /// broken by an occluder.
    /// </summary>
    private bool IsHidden(MapId mapId, Vector2 eyePos, Vector2 target)
    {
        var delta = target - eyePos;
        var distance = delta.Length();

        if (distance <= float.Epsilon)
            return false;

        var ray = new CollisionRay(eyePos, delta / distance, (int) CollisionGroup.Opaque);
        return _physics.IntersectRayWithPredicate(mapId, ray, distance, _ignoreNonOccluder).Any();
    }

    /// <summary>
    /// Predicate for <see cref="IsHidden"/> - returning true tells the raycast to ignore the entity.
    /// </summary>
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

    /// <summary>
    /// Tile spritesheets lay their variants out horizontally, so slice the atlas and cache the result.
    /// </summary>
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
}
