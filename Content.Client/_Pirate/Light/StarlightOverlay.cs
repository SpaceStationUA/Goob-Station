// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Light;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Pirate.Light;

public sealed class StarlightOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    [Dependency] private readonly IOverlayManager _overlay = default!;

    private readonly IEntityManager _entManager;
    private readonly StarlightSystem _starlight;
    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _xform;

    private List<Entity<MapGridComponent>> _grids = new();

    public StarlightOverlay(IEntityManager entManager, StarlightSystem starlight)
    {
        IoCManager.InjectDependencies(this);

        _entManager = entManager;
        _starlight = starlight;
        _maps = entManager.System<SharedMapSystem>();
        _xform = entManager.System<SharedTransformSystem>();

        // Render before tile emissions and blur.
        ZIndex = RoofOverlay.ContentZIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye || !_starlight.IsStarlit(args.MapUid))
            return;

        var (color, strength) = _starlight.GetStarlight(args.MapUid);

        if (strength <= 0f)
            return;

        var viewport = args.Viewport;
        var handle = args.WorldHandle;
        var lightOverlay = _overlay.GetOverlay<BeforeLightTargetOverlay>();
        var bounds = lightOverlay.EnlargedBounds;
        var target = lightOverlay.GetCachedForViewport(viewport).EnlargedLightTarget;
        var starlight = new Color(color.R * strength, color.G * strength, color.B * strength);

        _grids.Clear();
        _maps.FindGridsIntersecting(args.MapId, bounds, ref _grids, approx: true);

        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);

        handle.RenderInRenderTarget(target,
            () =>
            {
                var invMatrix = target.GetWorldToLocalMatrix(eye, scale);

                handle.SetTransform(invMatrix);
                handle.DrawRect(bounds, starlight);

                foreach (var grid in _grids)
                {
                    handle.SetTransform(Matrix3x2.Multiply(_xform.GetWorldMatrix(grid.Owner), invMatrix));

                    var localBounds = _xform.GetInvWorldMatrix(grid.Owner).TransformBox(bounds);
                    var tileSize = grid.Comp.TileSize;
                    var tiles = _maps.GetLocalTilesIntersecting(grid.Owner, grid.Comp, localBounds);

                    while (tiles.MoveNext(out var tile))
                    {
                        var light = _starlight.GetTileLight(grid, tile, starlight, strength);

                        if (light == starlight)
                            continue;

                        var box = Box2.FromDimensions((Vector2) tile.GridIndices * tileSize, new Vector2(tileSize, tileSize));
                        handle.DrawRect(box, light ?? Color.Black);
                    }
                }

                handle.SetTransform(Matrix3x2.Identity);
            }, null);
    }
}
