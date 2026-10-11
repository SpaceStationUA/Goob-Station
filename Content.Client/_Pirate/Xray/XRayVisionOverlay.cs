// SPDX-License-Identifier: MIT
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client._Pirate.Xray;

public sealed class XRayVisionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly ProfManager _prof = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;

    private readonly EntityQuery<OccluderComponent> _occluderQuery;
    private readonly EntityQuery<TransformComponent> _transformQuery;

    private static readonly ProtoId<ShaderPrototype> Shader = "XRayVision";
    private readonly ShaderInstance _tileShader;

    public const int ContentZIndex = Content.Client.Light.BeforeLightTargetOverlay.ContentZIndex + 1;

    private List<Entity<MapGridComponent>> _grids = [];
    private readonly Dictionary<Tile, Dictionary<byte, Texture>> _tileVariations = [];

    public bool ShowTiles { get; private set; }
    public Color TileOverlayColor { get; private set; } = new(1f, 1f, 1f, 0.2f);
    public float Scanlines { get; private set; }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public XRayVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = ContentZIndex;
        _tileShader = _prototypeManager.Index(Shader).InstanceUnique();
        _lookup = _entManager.System<EntityLookupSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _transform = _entManager.System<SharedTransformSystem>();
        _occluderQuery = _entManager.GetEntityQuery<OccluderComponent>();
        _transformQuery = _entManager.GetEntityQuery<TransformComponent>();
    }

    public void SetParameters(bool showTiles, Color tileOverlayColor, float scanlines)
    {
        ShowTiles = showTiles;
        TileOverlayColor = tileOverlayColor;
        Scanlines = scanlines;
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

        if (args.Viewport.Eye is not { } eye || !eye.DrawFov)
            return;

        if (!ShowTiles)
            return;

        var handle = args.WorldHandle;

        _tileShader.SetParameter("FOV_TEXTURE", args.Viewport.FovRenderTarget.Texture);
        _tileShader.SetParameter("FOV_CENTER", eye.Position.Position);
        _tileShader.SetParameter("OVERLAY_COLOR", TileOverlayColor);
        _tileShader.SetParameter("SCANLINES", Scanlines);

        handle.UseShader(_tileShader);
        DrawTiles(args, handle);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTiles(in OverlayDrawArgs args, DrawingHandleWorld handle)
    {
        using var _ = _prof.Group("XRayVisionOverlay.DrawTiles");

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        foreach (var grid in _grids)
        {
            var gridWorldMatrix = _transform.GetWorldMatrix(grid.Owner);
            handle.SetTransform(gridWorldMatrix);
            foreach (var tileRef in _map.GetTilesIntersecting(grid.Owner, grid.Comp, args.WorldAABB))
            {
                if (tileRef.Tile.IsEmpty || TileHasOccluder(grid, tileRef.GridIndices))
                    continue;

                if (!_tileDefManager.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef) || tileDef.Sprite is not { } sprite)
                    continue;

                var texture = GetTileTexture(tileRef.Tile, tileDef, sprite);
                handle.DrawTextureRect(texture, _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize));
            }
        }
    }

    private bool TileHasOccluder(Entity<MapGridComponent> grid, Vector2i indices)
    {
        var anchored = _map.GetAnchoredEntities(grid.Owner, grid.Comp, indices);
        while (anchored.MoveNext(out var ent))
        {
            if (_occluderQuery.TryGetComponent(ent, out var occluder) && occluder.Enabled)
                return true;
        }

        return false;
    }

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
