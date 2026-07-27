// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.ModularSuit;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Pirate.Client.ModularSuit;

/// <summary>
/// Draws terrain and occluding structures after lighting and FOV without revealing other entities.
/// </summary>
public sealed class MesonVisionOverlay : Overlay
{
    private static readonly Color TerrainFill = Color.FromHex("#103b2c").WithAlpha(0.10f);
    private static readonly Color TerrainOutline = Color.FromHex("#68d99a").WithAlpha(0.24f);
    private static readonly Color StructureColor = Color.FromHex("#75ffad").WithAlpha(0.68f);

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _map;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly EntityQuery<TransformComponent> _transformQuery;
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<Box2> _tileBounds = new();
    private readonly HashSet<Entity<OccluderComponent>> _structures = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MesonVisionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _lookup = _entityManager.System<EntityLookupSystem>();
        _map = _entityManager.System<SharedMapSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();

        _spriteQuery = _entityManager.GetEntityQuery<SpriteComponent>();
        _transformQuery = _entityManager.GetEntityQuery<TransformComponent>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eyeManager.CurrentEye
            && _player.LocalEntity is { Valid: true } player
            && _entityManager.HasComponent<MesonVisionComponent>(player);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (eye == null)
            return;

        DrawTerrain(args);
        DrawStructures(args, eye.Rotation);

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        args.WorldHandle.UseShader(null);
    }

    private void DrawTerrain(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId, args.WorldBounds, ref _grids, approx: true);

        foreach (var grid in _grids)
        {
            handle.SetTransform(_transform.GetWorldMatrix(grid.Owner));
            _tileBounds.Clear();
            var tiles = _map.GetTilesEnumerator(grid.Owner, grid.Comp, args.WorldBounds);

            while (tiles.MoveNext(out var tile))
            {
                if (tile.Tile.IsEmpty)
                    continue;

                _tileBounds.Add(_lookup.GetLocalBounds(tile, grid.Comp.TileSize));
            }

            foreach (var bounds in _tileBounds)
                handle.DrawRect(bounds, TerrainFill);

            foreach (var bounds in _tileBounds)
                handle.DrawRect(bounds, TerrainOutline, filled: false);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawStructures(in OverlayDrawArgs args, Angle eyeRotation)
    {
        _structures.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldBounds, _structures);

        foreach (var structure in _structures)
        {
            var uid = structure.Owner;
            if (!_spriteQuery.TryComp(uid, out var sprite)
                || !sprite.Visible
                || !_transformQuery.TryComp(uid, out var xform)
                || xform.MapID != args.MapId)
            {
                continue;
            }

            var originalColor = sprite.Color;
            try
            {
                _sprite.SetColor((uid, sprite), StructureColor);
                _sprite.RenderSprite(
                    (uid, sprite),
                    args.WorldHandle,
                    eyeRotation,
                    _transform.GetWorldRotation(xform),
                    _transform.GetWorldPosition(xform));
            }
            finally
            {
                _sprite.SetColor((uid, sprite), originalColor);
            }
        }
    }
}
