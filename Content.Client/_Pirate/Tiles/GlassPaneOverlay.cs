// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Tiles;

public sealed class GlassPaneOverlay : GridOverlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    private readonly IEntityManager _entManager;
    private readonly IResourceCache _resources;
    private readonly ITileDefinitionManager _tileDefs;
    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _xform;
    private readonly ShaderInstance _shader;

    private readonly Dictionary<int, (Texture[] Variants, float Alpha)?> _panes = new();

    public GlassPaneOverlay(IEntityManager entManager, IPrototypeManager protoMan, IResourceCache resources,
        ITileDefinitionManager tileDefs)
    {
        _entManager = entManager;
        _resources = resources;
        _tileDefs = tileDefs;
        _maps = entManager.System<SharedMapSystem>();
        _xform = entManager.System<SharedTransformSystem>();
        _shader = protoMan.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var grid = Grid;

        if (!_entManager.TryGetComponent(grid.Owner, out TransformComponent? xform) || xform.MapID != args.MapId)
            return;

        var handle = args.WorldHandle;
        var localBounds = _xform.GetInvWorldMatrix(xform).TransformBox(args.WorldBounds.Enlarged(1f));
        var tiles = _maps.GetLocalTilesIntersecting(grid.Owner, grid.Comp, localBounds);
        var drawing = false;

        while (tiles.MoveNext(out var tile))
        {
            if (GetPane(tile.Tile.TypeId) is not { } pane || tile.Tile.Variant >= pane.Variants.Length)
                continue;

            if (!drawing)
            {
                handle.SetTransform(_xform.GetWorldMatrix(xform));
                handle.UseShader(_shader);
                drawing = true;
            }

            handle.DrawTexture(pane.Variants[tile.Tile.Variant],
                (Vector2) tile.GridIndices * grid.Comp.TileSize,
                Color.White.WithAlpha(pane.Alpha));
        }

        if (!drawing)
            return;

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private (Texture[] Variants, float Alpha)? GetPane(int typeId)
    {
        if (_panes.TryGetValue(typeId, out var cached))
            return cached;

        (Texture[], float)? pane = null;

        if (_tileDefs[typeId] is ContentTileDefinition { PaneOverlay: { } path } def)
        {
            var texture = _resources.GetResource<TextureResource>(path).Texture;
            var size = texture.Height;
            var variants = new Texture[texture.Width / size];

            for (var i = 0; i < variants.Length; i++)
            {
                variants[i] = new AtlasTexture(texture, UIBox2.FromDimensions(new Vector2(i * size, 0), new Vector2(size, size)));
            }

            pane = (variants, def.PaneOverlayAlpha);
        }

        _panes[typeId] = pane;
        return pane;
    }
}
