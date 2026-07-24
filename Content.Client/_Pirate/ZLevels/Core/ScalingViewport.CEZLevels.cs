/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using System.Numerics;
using Content.Client._Pirate.ZLevels.Core;
using Content.Shared._Pirate.ZLevels.Apertures.Components;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ITileDefinitionManager _tile = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!; // Pirate: multiz
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private CEClientZLevelsSystem? _zLevels;
    private SharedMapSystem? _mapSystem;
    // Lazily resolved for linked-deck eye reprojection.
    private SharedTransformSystem? _transform;

    private EntityQuery<TransformComponent>? _xformQuery;
    private EntityQuery<MapComponent>? _mapQuery;
    private IEye? _fallbackEye;
    // Last linked grid the eye stood on. Reused to keep decks aligned while the eye
    // hovers over gridless open space, instead of snapping back to raw world coords.
    private EntityUid? _lastLinkedGrid;
    private readonly Dictionary<int, IRenderTexture> _zApertureTargets = new();
    private readonly HashSet<int> _zApertureValidTargets = new();
    private readonly Dictionary<int, EntityUid> _zApertureMapUids = new();
    private readonly Dictionary<int, IEye> _zApertureEyes = new();
    private ZLevelApertureOverlay? _zApertureOverlay;
    private bool _zApertureCaptureThisFrame;

    public EntityUid? CEZLevelViewEntity { get; set; }

    // Cached reference to the engine's PlacementOverlay, found by type name. Pirate: multiz
    private Overlay? _cachedPlacementOverlay; // Pirate: multiz
    // Only the deepest pass may draw full-screen parallax.
    private Overlay? _cachedParallaxOverlay;

    /// <summary>
    /// We are looking for at least one empty tile on the screen.
    /// This is used to ensure that it makes sense to draw the z-planes and that they are visible.
    /// </summary>
    public bool TryFindEmptyTiles(EntityUid mapUid)
    {
        if (_xformQuery is null || !_xformQuery.Value.TryComp(mapUid, out var xform))
            return true;

        var drawBox = GetDrawBox();
        var mapId = xform.MapID;

        var corners = new[]
        {
            _eyeManager.ScreenToMap(drawBox.BottomLeft).Position,
            _eyeManager.ScreenToMap(drawBox.BottomRight).Position,
            _eyeManager.ScreenToMap(drawBox.TopLeft).Position,
            _eyeManager.ScreenToMap(drawBox.TopRight).Position
        };

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var c in corners)
        {
            if (c.X < minX)
                minX = c.X;
            if (c.Y < minY)
                minY = c.Y;
            if (c.X > maxX)
                maxX = c.X;
            if (c.Y > maxY)
                maxY = c.Y;
        }

        var mapCoordsBottomLeft = new MapCoordinates(new Vector2(minX, minY), mapId);
        var mapCoordsTopRight = new MapCoordinates(new Vector2(maxX, maxY), mapId);

        if (!_mapManager.TryFindGridAt(mapUid, mapCoordsBottomLeft.Position, out _, out var grid))
            return true;

        var tileBottomLeft = grid.TileIndicesFor(mapCoordsBottomLeft);
        var tileTopRight = grid.TileIndicesFor(mapCoordsTopRight);

        for (var x = tileBottomLeft.X - 1; x <= tileTopRight.X + 1; x++)
        {
            for (var y = tileBottomLeft.Y - 1; y <= tileTopRight.Y + 1; y++)
            {
                var tile = grid.GetTileRef(new Vector2i(x, y));
                var tileDef = (ContentTileDefinition)_tile[tile.Tile.TypeId];
                if (tileDef.ZTransparent || tile.Tile.IsEmpty)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the map for a depth offset, preferring linked-grid peers.
    /// </summary>
    private bool TryResolveZMap(EntityUid playerMapUid, EntityUid? playerGridUid, int depthOffset, out MapId mapId)
        => TryResolveZMap(playerMapUid, playerGridUid, depthOffset, out mapId, out _);

    private bool TryResolveZMap(EntityUid playerMapUid, EntityUid? playerGridUid, int depthOffset, out MapId mapId, out EntityUid? peerGridUid) // Pirate: multiz
    {
        if (TryResolveZMapEntity(playerMapUid, playerGridUid, depthOffset, out _, out mapId, out peerGridUid))
            return true;

        mapId = default;
        return false;
    }

    private bool TryResolveZMapEntity(EntityUid playerMapUid, EntityUid? playerGridUid, int depthOffset, out EntityUid mapUid, out MapId mapId, out EntityUid? peerGridUid) // Pirate: multiz
    {
        mapUid = default;
        mapId = default;
        peerGridUid = null; // Pirate: multiz

        // Prefer the linked structure the player is on.
        if (playerGridUid != null &&
            _entityManager.TryGetComponent<CEZLinkedGridComponent>(playerGridUid.Value, out var linked))
        {
            var targetDepth = linked.Depth + depthOffset;

            if (linked.PeerGrids.TryGetValue(targetDepth, out var peerGrid))
            {
                if (_xformQuery!.Value.TryComp(peerGrid, out var peerXform) &&
                    peerXform.MapUid != null &&
                    _mapQuery!.Value.TryComp(peerXform.MapUid.Value, out var peerMapComp))
                {
                    mapUid = peerXform.MapUid.Value;
                    mapId = peerMapComp.MapId;
                    peerGridUid = peerGrid; // Pirate: multiz
                    return true;
                }
            }

            // Linked grids should not fall back to unrelated map-level decks.
            return false;
        }

        // Fallback for players not on a linked grid.
        if (_zLevels!.TryMapOffset(playerMapUid, depthOffset, out var targetMap))
        {
            if (_mapQuery!.Value.TryComp(targetMap.Value, out var mapComp))
            {
                mapUid = targetMap.Value;
                mapId = mapComp.MapId;
                return true;
            }
        }

        return false;
    }

    private Vector2 GetRawEyePosition(TransformComponent playerXform)
    {
        _transform ??= _entityManager.System<SharedTransformSystem>();
        return _fallbackEye?.Position.Position ?? _eye?.Position.Position ?? _transform.GetWorldPosition(playerXform);
    }

    /// <summary>
    /// Resolves the linked grid whose frame the eye should be reprojected through.
    /// On a grid we use it directly; over gridless open space we fall back to the linked
    /// grid under the eye, then to the last linked grid we stood on. This keeps the decks
    /// below aligned instead of snapping to raw world coordinates the moment the eye
    /// leaves the grid (the two linked decks sit at different world offsets, and only the
    /// grid-frame reprojection compensates for that).
    /// </summary>
    private EntityUid? ResolveEffectiveGrid(TransformComponent playerXform)
    {
        // On a grid: use it directly and remember it for later gridless frames.
        if (playerXform.GridUid is { } gridUid)
        {
            if (_entityManager.HasComponent<CEZLinkedGridComponent>(gridUid))
                _lastLinkedGrid = gridUid;

            return gridUid;
        }

        // Gridless: reproject through a linked grid directly under the eye, if any.
        if (playerXform.MapUid is { } mapUid)
        {
            var eyeWorld = GetRawEyePosition(playerXform);
            if (_mapManager.TryFindGridAt(mapUid, eyeWorld, out var foundGridUid, out _) &&
                _entityManager.HasComponent<CEZLinkedGridComponent>(foundGridUid))
            {
                _lastLinkedGrid = foundGridUid;
                return foundGridUid;
            }
        }

        // Over open space: reuse the last linked grid we stood on so the transition off
        // the edge stays continuous, as long as it still exists on the same map.
        if (_lastLinkedGrid is { } cached &&
            _entityManager.EntityExists(cached) &&
            _xformQuery!.Value.TryComp(cached, out var cachedXform) &&
            cachedXform.MapUid == playerXform.MapUid &&
            _entityManager.HasComponent<CEZLinkedGridComponent>(cached))
        {
            return cached;
        }

        _lastLinkedGrid = null;
        return null;
    }

    // Reproject the eye into the peer grid's world space.
    private MapCoordinates GetResolvedEyePosition(TransformComponent playerXform, EntityUid? currentGridUid, EntityUid? peerGridUid, MapId targetMapId)
    {
        _transform ??= _entityManager.System<SharedTransformSystem>();
        var rawEyePosition = GetRawEyePosition(playerXform);

        if (peerGridUid is not { } peerGrid ||
            currentGridUid is not { } currentGrid)
        {
            return new MapCoordinates(rawEyePosition, targetMapId);
        }

        var currentGridMatrix = _transform.GetWorldMatrix(currentGrid);
        var peerGridMatrix = _transform.GetWorldMatrix(peerGrid);

        if (!Matrix3x2.Invert(currentGridMatrix, out var inverseCurrentGrid))
            return new MapCoordinates(rawEyePosition, targetMapId);

        var eyeLocalToCurrentGrid = Vector2.Transform(rawEyePosition, inverseCurrentGrid);
        var targetWorldPosition = Vector2.Transform(eyeLocalToCurrentGrid, peerGridMatrix);

        return new MapCoordinates(targetWorldPosition, targetMapId);
    }

    private void RenderZLevels(IClydeViewport viewport, DrawingHandleScreen screenHandle)
    {
        if (_eye is null)
        {
            viewport.Render();
            return;
        }

        _fallbackEye = _eye;

        _xformQuery ??= _entityManager.GetEntityQuery<TransformComponent>();
        _mapQuery ??= _entityManager.GetEntityQuery<MapComponent>();
        _zLevels ??= _entityManager.System<CEClientZLevelsSystem>();
        _mapSystem ??= _entityManager.System<SharedMapSystem>();

        if (_player.LocalEntity is null)
        {
            viewport.Render();
            return;
        }

        if (!_entityManager.TryGetComponent<CEZLevelViewerComponent>(_player.LocalEntity.Value, out var zLevelViewer))
        {
            viewport.Render();
            return;
        }

        // Remote eyes render z-levels from the eye's grid/map, not the viewer body's.
        var relayTarget = GetRelayViewTarget();
        var viewEntity = CEZLevelViewEntity ?? relayTarget ?? _player.LocalEntity.Value;
        if (!_xformQuery.Value.TryComp(viewEntity, out var playerXform))
        {
            viewEntity = _player.LocalEntity.Value;
            if (!_xformQuery.Value.TryComp(viewEntity, out playerXform))
            {
                viewport.Render();
                return;
            }
        }

        if (playerXform.MapUid is null)
        {
            viewport.Render();
            return;
        }

        // Grid frame to reproject the eye/decks through, tolerant of gridless open space.
        var effectiveGridUid = ResolveEffectiveGrid(playerXform);

        var visibleBelow = Math.Clamp(
            _cfg.GetCVar(CCVars.CEZLevelsVisibleBelow),
            0,
            CESharedZLevelsSystem.MaxZLevelsBelowRendering);
        var lookUp = zLevelViewer.LookUp ? 1 : 0;
        var highestDepth = lookUp > 0 &&
            TryResolveZMap(playerXform.MapUid.Value, effectiveGridUid, lookUp, out _)
                ? lookUp
                : 0;
        _zApertureValidTargets.Clear();
        _zApertureMapUids.Clear();
        _zApertureEyes.Clear();

        var lowestDepth = 0;
        for (var i = 0; i >= -visibleBelow; i--)
        {
            if (i != 0)
            {
                if (!TryResolveZMap(playerXform.MapUid.Value, effectiveGridUid, i, out _))
                    continue;
            }

            lowestDepth = i;
        }

        _zApertureCaptureThisFrame = HasZLevelAperturesInRenderedDepths(playerXform.MapUid.Value, effectiveGridUid, lowestDepth, highestDepth);

        if (_zApertureCaptureThisFrame)
        {
            EnsureZLevelApertureTargets(viewport.RenderTarget.Size, lowestDepth, highestDepth);
            EnsureZLevelApertureOverlay();
        }

        // Find the engine's PlacementOverlay once and cache it. Pirate: multiz
        // Hide it on secondary passes to avoid duplicate previews. Pirate: multiz
        _cachedPlacementOverlay ??= _overlayManager.AllOverlays // Pirate: multiz
            .FirstOrDefault(o => o.GetType().FullName == "Robust.Client.Placement.PlacementManager+PlacementOverlay"); // Pirate: multiz
        _cachedParallaxOverlay ??= _overlayManager.AllOverlays
            .FirstOrDefault(o => o is Content.Client.Parallax.ParallaxOverlay);

        var playerEye = _fallbackEye as Robust.Shared.Graphics.Eye;
        var playerEyePosition = playerEye?.Position ?? default;
        var playerEyeDrawFov = playerEye?.DrawFov ?? default;
        var playerEyeDrawLight = playerEye?.DrawLight ?? default;
        var playerEyeOffset = playerEye?.Offset ?? default;
        var playerEyeRotation = playerEye?.Rotation ?? default;
        var playerEyeScale = playerEye?.Scale ?? default;

        try
        {
            for (var depth = lowestDepth; depth <= highestDepth; depth++)
            {
                EntityUid renderedMapUid;
                MapCoordinates eyePosition;
                bool drawFov;
                Vector2 offset;

                if (depth == 0)
                {
                    renderedMapUid = playerXform.MapUid.Value;
                    eyePosition = _fallbackEye.Position;
                    drawFov = _fallbackEye.DrawFov;
                    offset = _fallbackEye.Offset;
                }
                else
                {
                    if (!TryResolveZMapEntity(playerXform.MapUid.Value, effectiveGridUid, depth, out renderedMapUid, out var targetMapId, out var peerGridUid))
                        continue;

                    var rotation = -_fallbackEye.Rotation;
                    offset = _fallbackEye.Offset +
                        rotation.ToWorldVec() * CEClientZLevelsSystem.ZLevelOffset * depth;
                    // Peer-aware eye position keeps linked decks aligned, even off-grid.
                    eyePosition = GetResolvedEyePosition(playerXform, effectiveGridUid, peerGridUid, targetMapId);
                    drawFov = _fallbackEye.DrawFov && depth >= 0;
                }

                IEye eye;
                if (depth == highestDepth && playerEye != null)
                {
                    // Player-only overlays compare eye identity. Use the real eye only on the final
                    // compositing pass so flash, blindness and vision shaders affect the whole frame once.
                    playerEye.Position = eyePosition;
                    playerEye.DrawFov = drawFov;
                    playerEye.DrawLight = _fallbackEye.DrawLight;
                    playerEye.Offset = offset;
                    playerEye.Rotation = _fallbackEye.Rotation;
                    playerEye.Scale = _fallbackEye.Scale;
                    eye = playerEye;
                }
                else
                {
                    eye = new ZEye(lowestDepth, depth, highestDepth)
                    {
                        Position = eyePosition,
                        DrawFov = drawFov,
                        DrawLight = _fallbackEye.DrawLight,
                        Offset = offset,
                        Rotation = _fallbackEye.Rotation,
                        Scale = _fallbackEye.Scale,
                    };
                }

                viewport.Eye = eye;
                _zApertureEyes[depth] = eye;
                _zApertureMapUids[depth] = renderedMapUid;
                viewport.ClearColor = depth == lowestDepth ? Color.Black : null;

                #region Pirate: multiz
                // Hide duplicate previews, plus unsafe mid-z-move placement snaps.
                var hidePlacement = _cachedPlacementOverlay != null
                    && (depth != 0 || PlacementOverlayWouldDesync(renderedMapUid));
                if (hidePlacement)
                    _overlayManager.RemoveOverlay(_cachedPlacementOverlay!);

                // Higher deck parallax would cover the already composited lower decks.
                var hideParallax = depth != lowestDepth && _cachedParallaxOverlay != null;
                if (hideParallax)
                    _overlayManager.RemoveOverlay(_cachedParallaxOverlay!);

                viewport.Render();

                if (_zApertureCaptureThisFrame && depth < highestDepth)
                    CaptureZLevelApertureTexture(screenHandle, viewport, depth);

                if (hideParallax)
                    _overlayManager.AddOverlay(_cachedParallaxOverlay!);

                if (hidePlacement)
                    _overlayManager.AddOverlay(_cachedPlacementOverlay!);
                #endregion Pirate: multiz
            }
        }
        finally
        {
            if (playerEye != null)
            {
                playerEye.Position = playerEyePosition;
                playerEye.DrawFov = playerEyeDrawFov;
                playerEye.DrawLight = playerEyeDrawLight;
                playerEye.Offset = playerEyeOffset;
                playerEye.Rotation = playerEyeRotation;
                playerEye.Scale = playerEyeScale;
            }

            Eye = _fallbackEye;
            viewport.Eye = Eye;
        }
    }

    // Returns the remote eye currently viewed by the local player, if any.
    private EntityUid? GetRelayViewTarget()
    {
        if (_player.LocalEntity is not { } local)
            return null;

        return _entityManager.TryGetComponent<EyeComponent>(local, out var eye) ? eye.Target : null;
    }

    // True when an active placement preview targets a map other than the one being rendered.
    private bool PlacementOverlayWouldDesync(EntityUid renderedMapUid)
    {
        if (!_placement.IsActive || _placement.CurrentMode is not { } mode)
            return false;
        if (_xformQuery is not { } query || !query.TryComp(mode.MouseCoords.EntityId, out var coordsXform))
            return false;
        return coordsXform.MapUid != renderedMapUid;
    }

    private bool HasZLevelApertures(EntityUid mapUid)
    {
        var query = _entityManager.EntityQueryEnumerator<CEZLevelApertureComponent, TransformComponent>();
        while (query.MoveNext(out _, out var aperture, out var xform))
        {
            if (aperture.TargetDepth == -1 && xform.MapUid == mapUid)
                return true;
        }

        return false;
    }

    private bool HasZLevelAperturesInRenderedDepths(EntityUid playerMapUid, EntityUid? playerGridUid, int lowestDepth, int lookUp)
    {
        for (var depth = lowestDepth + 1; depth <= lookUp; depth++)
        {
            EntityUid mapUid;

            if (depth == 0)
            {
                mapUid = playerMapUid;
            }
            else if (!TryResolveZMapEntity(playerMapUid, playerGridUid, depth, out mapUid, out _, out _))
            {
                continue;
            }

            if (HasZLevelApertures(mapUid))
                return true;
        }

        return false;
    }

    private void EnsureZLevelApertureTargets(Vector2i size, int lowestDepth, int lookUp)
    {
        for (var depth = lowestDepth; depth < lookUp; depth++)
        {
            if (_zApertureTargets.TryGetValue(depth, out var existing) && existing.Size == size)
                continue;

            existing?.Dispose();
            _zApertureTargets[depth] = _clyde.CreateRenderTarget(
                size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                new TextureSampleParameters { Filter = false },
                $"z-level-aperture-depth-{depth}");
        }
    }

    private void CaptureZLevelApertureTexture(DrawingHandleScreen screenHandle, IClydeViewport viewport, int depth)
    {
        if (!_zApertureTargets.TryGetValue(depth, out var target))
            return;

        var targetBox = UIBox2.FromDimensions(Vector2.Zero, viewport.RenderTarget.Size);
        screenHandle.RenderInRenderTarget(target, () =>
        {
            screenHandle.DrawTextureRect(viewport.RenderTarget.Texture, targetBox);
        }, Color.Transparent);

        _zApertureValidTargets.Add(depth);
    }

    private void DrawZLevelAperturesWorld(DrawingHandleWorld worldHandle, IClydeViewport viewport)
    {
        if (_fallbackEye is null ||
            _viewport is null ||
            !ReferenceEquals(viewport, _viewport))
        {
            return;
        }

        _transform ??= _entityManager.System<SharedTransformSystem>();

        var query = _entityManager.EntityQueryEnumerator<CEZLevelApertureComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var aperture, out var xform))
        {
            if (aperture.TargetDepth != -1 ||
                aperture.SpritePixelSize <= 0 ||
                aperture.PixelSize.X <= 0 ||
                aperture.PixelSize.Y <= 0)
            {
                continue;
            }

            if (!TryFindApertureDepth(xform.MapUid, out var apertureDepth))
                continue;

            var sourceDepth = apertureDepth - 1;
            if (!_zApertureValidTargets.Contains(sourceDepth) ||
                !_zApertureTargets.TryGetValue(sourceDepth, out var sourceTarget) ||
                !_zApertureEyes.TryGetValue(apertureDepth, out var apertureEye))
            {
                continue;
            }

            var destinationViewportQuad = GetApertureViewportQuad(uid, aperture, xform, apertureEye);
            var destinationViewportBox = destinationViewportQuad.Bounds;
            if (!destinationViewportBox.Intersects(UIBox2.FromDimensions(Vector2.Zero, _viewport.Size)))
                continue;

            // Same-screen-position sampling works because z-level grids are fixed relative to each other.
            var sourceViewportQuad = destinationViewportQuad;
            var destinationWorldQuad = GetApertureWorldQuad(uid, aperture, xform, apertureEye);

            DrawZLevelApertureQuad(worldHandle, sourceTarget.Texture, sourceTarget.Size, destinationWorldQuad, sourceViewportQuad);
        }
    }

    private static void DrawZLevelApertureQuad(
        DrawingHandleWorld worldHandle,
        Texture texture,
        Vector2i textureSize,
        ApertureQuad destinationWorldQuad,
        ApertureQuad sourceViewportQuad)
    {
        var vertices = new DrawVertexUV2D[6];

        vertices[0] = new DrawVertexUV2D(destinationWorldQuad.TopLeft, ViewportPointToTextureUv(sourceViewportQuad.TopLeft, textureSize));
        vertices[1] = new DrawVertexUV2D(destinationWorldQuad.TopRight, ViewportPointToTextureUv(sourceViewportQuad.TopRight, textureSize));
        vertices[2] = new DrawVertexUV2D(destinationWorldQuad.BottomLeft, ViewportPointToTextureUv(sourceViewportQuad.BottomLeft, textureSize));
        vertices[3] = new DrawVertexUV2D(destinationWorldQuad.TopRight, ViewportPointToTextureUv(sourceViewportQuad.TopRight, textureSize));
        vertices[4] = new DrawVertexUV2D(destinationWorldQuad.BottomRight, ViewportPointToTextureUv(sourceViewportQuad.BottomRight, textureSize));
        vertices[5] = new DrawVertexUV2D(destinationWorldQuad.BottomLeft, ViewportPointToTextureUv(sourceViewportQuad.BottomLeft, textureSize));

        worldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, texture, vertices);
    }

    private static Vector2 ViewportPointToTextureUv(Vector2 point, Vector2i textureSize)
    {
        return new Vector2(
            point.X / textureSize.X,
            1f - point.Y / textureSize.Y);
    }

    private bool TryFindApertureDepth(EntityUid? mapUid, out int depth)
    {
        if (mapUid is null)
        {
            depth = default;
            return false;
        }

        foreach (var (candidateDepth, candidateMapUid) in _zApertureMapUids)
        {
            if (candidateMapUid != mapUid.Value)
                continue;

            depth = candidateDepth;
            return true;
        }

        depth = default;
        return false;
    }

    private ApertureQuad GetApertureViewportQuad(EntityUid uid, CEZLevelApertureComponent aperture, TransformComponent xform, IEye eye)
    {
        var matrix = GetApertureDrawMatrix(uid, xform, eye);
        var localQuad = GetPixelLocalQuad(aperture.PixelOffset, aperture.PixelSize, aperture.SpritePixelSize);

        return new ApertureQuad(
            LocalToViewport(localQuad.TopLeft, matrix, eye),
            LocalToViewport(localQuad.TopRight, matrix, eye),
            LocalToViewport(localQuad.BottomLeft, matrix, eye),
            LocalToViewport(localQuad.BottomRight, matrix, eye));
    }

    private ApertureQuad GetApertureWorldQuad(EntityUid uid, CEZLevelApertureComponent aperture, TransformComponent xform, IEye eye)
    {
        var matrix = GetApertureDrawMatrix(uid, xform, eye);
        var localQuad = GetPixelLocalQuad(aperture.PixelOffset, aperture.PixelSize, aperture.SpritePixelSize);

        return new ApertureQuad(
            Vector2.Transform(localQuad.TopLeft, matrix),
            Vector2.Transform(localQuad.TopRight, matrix),
            Vector2.Transform(localQuad.BottomLeft, matrix),
            Vector2.Transform(localQuad.BottomRight, matrix));
    }

    private Matrix3x2 GetApertureDrawMatrix(EntityUid uid, TransformComponent xform, IEye eye)
    {
        if (!_entityManager.TryGetComponent<SpriteComponent>(uid, out var sprite))
            return _transform!.GetWorldMatrix(xform);

        var (worldPosition, worldRotation) = _transform!.GetWorldPositionRotation(xform);
        var angle = (worldRotation + eye.Rotation).Reduced().FlipPositive();
        var cardinal = Angle.Zero;

        if (sprite is { NoRotation: false, SnapCardinals: true })
            cardinal = angle.RoundToCardinalAngle();

        var entityMatrix = Matrix3Helpers.CreateTransform(worldPosition, sprite.NoRotation ? -eye.Rotation : worldRotation - cardinal);
        return Matrix3x2.Multiply(sprite.LocalMatrix, entityMatrix);
    }

    private static ApertureQuad GetPixelLocalQuad(Vector2i pixelOffset, Vector2i pixelSize, int spritePixelSize)
    {
        var spriteSize = spritePixelSize;
        var half = spriteSize / 2f;
        var left = (pixelOffset.X - half) / spriteSize;
        var right = (pixelOffset.X + pixelSize.X - half) / spriteSize;
        var top = (half - pixelOffset.Y) / spriteSize;
        var bottom = (half - pixelOffset.Y - pixelSize.Y) / spriteSize;

        return new ApertureQuad(
            new Vector2(left, top),
            new Vector2(right, top),
            new Vector2(left, bottom),
            new Vector2(right, bottom));
    }

    private Vector2 LocalToViewport(Vector2 localPoint, Matrix3x2 matrix, IEye eye)
    {
        var worldPoint = Vector2.Transform(localPoint, matrix);
        return _viewport!.RenderTarget.WorldToLocal(worldPoint, eye, _viewport.RenderScale);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_zApertureOverlay != null)
        {
            _overlayManager.RemoveOverlay(_zApertureOverlay);
            _zApertureOverlay = null;
        }

        foreach (var target in _zApertureTargets.Values)
            target.Dispose();
        _zApertureTargets.Clear();
    }

    private void EnsureZLevelApertureOverlay()
    {
        if (_zApertureOverlay != null)
            return;

        _zApertureOverlay = new ZLevelApertureOverlay(this);
        _overlayManager.AddOverlay(_zApertureOverlay);
    }

    private sealed class ZLevelApertureOverlay : Overlay
    {
        private readonly ScalingViewport _viewport;

        public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

        public ZLevelApertureOverlay(ScalingViewport viewport)
        {
            _viewport = viewport;
            ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.FloorTiles - 1;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            _viewport.DrawZLevelAperturesWorld(args.WorldHandle, args.Viewport);
        }
    }

    private readonly record struct ApertureQuad(Vector2 TopLeft, Vector2 TopRight, Vector2 BottomLeft, Vector2 BottomRight)
    {
        public UIBox2 Bounds
        {
            get
            {
                var min = Vector2.Min(Vector2.Min(TopLeft, TopRight), Vector2.Min(BottomLeft, BottomRight));
                var max = Vector2.Max(Vector2.Max(TopLeft, TopRight), Vector2.Max(BottomLeft, BottomRight));
                return new UIBox2(min, max);
            }
        }
    }

    public sealed class ZEye(int lowest, int depth, int high) : Robust.Shared.Graphics.Eye
    {
        public int LowestDepth = lowest;
        public int Depth = depth;
        public int HighestDepth = high;
    }
}
