// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration;
using Content.Shared._Pirate.ZLevels.Apertures.Components;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Pirate.ZLevels.Profiling;

/// <summary>
/// Profiling helper: holds the spots picked by <see cref="ZProfSpotsCommand"/> for <see cref="ZProfGotoCommand"/>.
/// </summary>
public sealed class ZProfSpotsSystem : EntitySystem
{
    public readonly List<(string Label, EntityCoordinates Coordinates)> Spots = new();
}

/// <summary>
/// Profiling helper: picks viewing spots on one floor of the station by how many sight openings
/// (empty or ZSightPermeable tiles) and lower-deck apertures fall inside a viewport-sized window around them.
/// Either one keeps the client drawing the deck below, so "none" needs both to be absent.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ZProfSpotsCommand : LocalizedEntityCommands
{
    // Viewport half-extents in tiles at default zoom, plus the wider "nothing nearby" radius.
    private const int ViewX = 11;
    private const int ViewY = 8;
    private const int NoneX = 13;
    private const int NoneY = 10;
    private const int MinSpotDistance = 15;

    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ZProfSpotsSystem _spots = default!;

    public override string Command => "zprof_spots";
    public override string Description => "Picks glass-heavy, space-heavy, few-opening, aperture and no-opening spots on a floor for zprof_goto.";
    public override string Help => "zprof_spots [depth]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var depth = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 0;
        if (!TryGetStationGrid(depth, out var gridUid, out var grid))
        {
            shell.WriteError($"No station grid at depth {depth}.");
            return;
        }

        // Opening/glass maps over the grid bounds plus a margin, as 2D prefix sums for O(1) window counts.
        var bounds = grid.LocalAABB;
        var minX = (int) MathF.Floor(bounds.Left) - NoneX - 1;
        var minY = (int) MathF.Floor(bounds.Bottom) - NoneY - 1;
        var width = (int) MathF.Ceiling(bounds.Right) + NoneX + 1 - minX;
        var height = (int) MathF.Ceiling(bounds.Top) + NoneY + 1 - minY;
        var open = new int[width + 1, height + 1];
        var glass = new int[width + 1, height + 1];
        var apertures = new int[width + 1, height + 1];
        var apertureQuery = EntityManager.EntityQueryEnumerator<CEZLevelApertureComponent, TransformComponent>();
        while (apertureQuery.MoveNext(out _, out var aperture, out var apertureXform))
        {
            if (apertureXform.GridUid != gridUid || aperture.TargetDepth != -1)
                continue;

            var at = _map.TileIndicesFor(gridUid, grid, apertureXform.Coordinates);
            if (at.X - minX is >= 0 and var ax && ax < width && at.Y - minY is >= 0 and var ay && ay < height)
                apertures[ax + 1, ay + 1]++;
        }

        var floors = new List<Vector2i>();

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var idx = new Vector2i(minX + x, minY + y);
                var isOpen = true;
                var isGlass = false;
                if (_map.TryGetTileRef(gridUid, grid, idx, out var tileRef) && !tileRef.Tile.IsEmpty)
                {
                    var def = (ContentTileDefinition) _tileDefs[tileRef.Tile.TypeId];
                    isGlass = def.ZTransparent || def.ZSightPermeable; // what the renderer draws the deck below through
                    isOpen = isGlass;
                    if (!isGlass)
                        floors.Add(idx);
                }

                open[x + 1, y + 1] = (isOpen ? 1 : 0) + open[x, y + 1] + open[x + 1, y] - open[x, y];
                glass[x + 1, y + 1] = (isGlass ? 1 : 0) + glass[x, y + 1] + glass[x + 1, y] - glass[x, y];
                apertures[x + 1, y + 1] += apertures[x, y + 1] + apertures[x + 1, y] - apertures[x, y];
            }
        }

        int Sum(int[,] p, Vector2i c, int rx, int ry)
        {
            var x0 = Math.Clamp(c.X - rx - minX, 0, width);
            var y0 = Math.Clamp(c.Y - ry - minY, 0, height);
            var x1 = Math.Clamp(c.X + rx + 1 - minX, 0, width);
            var y1 = Math.Clamp(c.Y + ry + 1 - minY, 0, height);
            return p[x1, y1] - p[x0, y1] - p[x1, y0] + p[x0, y0];
        }

        var center = bounds.Center.Floored();
        var scored = floors
            .Select(t => (Tile: t,
                Glass: Sum(glass, t, ViewX, ViewY),
                Open: Sum(open, t, ViewX, ViewY),
                Wide: Sum(open, t, NoneX, NoneY) + Sum(apertures, t, NoneX, NoneY),
                Apertures: Sum(apertures, t, ViewX, ViewY),
                Center: (t - center).Length))
            .ToList();

        var picks = new List<(string Label, Vector2i Tile, int Glass, int Open, int Apertures)>();

        void Pick(string label, IEnumerable<(Vector2i Tile, int Glass, int Open, int Wide, int Apertures, float Center)> ordered)
        {
            foreach (var c in ordered)
            {
                if (picks.Any(p => (p.Tile - c.Tile).Length < MinSpotDistance) ||
                    _turf.IsTileBlocked(gridUid, c.Tile, CollisionGroup.Impassable, grid))
                    continue;

                picks.Add((label, c.Tile, c.Glass, c.Open, c.Apertures));
                return;
            }
        }

        Pick("glass", scored.Where(c => c.Glass > 0).OrderByDescending(c => c.Glass));
        Pick("space", scored.OrderByDescending(c => c.Open - c.Glass));
        Pick("few", scored.Where(c => c.Open is > 0 and <= 15).OrderBy(c => Math.Abs(c.Open - 6)).ThenBy(c => c.Center));
        Pick("aperture", scored.Where(c => c.Apertures > 0 && c.Open == 0).OrderBy(c => c.Center));
        Pick("none", scored.Where(c => c.Wide == 0).OrderBy(c => c.Center));

        _spots.Spots.Clear();
        for (var i = 0; i < picks.Count; i++)
        {
            var (label, tile, glassCount, openCount, apertureCount) = picks[i];
            _spots.Spots.Add((label, _map.GridTileToLocal(gridUid, grid, tile)));
            shell.WriteLine($"ZPROF_SPOT {i} {label} tile={tile.X},{tile.Y} glass={glassCount} open={openCount} apertures={apertureCount}");
        }
    }

    private bool TryGetStationGrid(int depth, out EntityUid gridUid, out MapGridComponent grid)
    {
        gridUid = default;
        grid = default!;
        var best = -1;
        var query = EntityManager.EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapUid is not { } mapUid)
                continue;

            var isZ = EntityManager.TryGetComponent<CEZLevelMapComponent>(mapUid, out var zMap);
            if ((isZ ? zMap!.Depth : 0) != depth ||
                !isZ && !EntityManager.HasComponent<StationMemberComponent>(uid))
                continue;

            var tiles = _map.GetAllTiles(uid, comp).Count();
            if (tiles <= best)
                continue;

            best = tiles;
            gridUid = uid;
            grid = comp;
        }

        return best >= 0;
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class ZProfGotoCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ZProfSpotsSystem _spots = default!;

    public override string Command => "zprof_goto";
    public override string Description => "Teleports a player to a spot picked by zprof_spots.";
    public override string Help => "zprof_goto <index> [username]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var index) || index < 0 || index >= _spots.Spots.Count)
        {
            shell.WriteError($"Usage: {Help} ({_spots.Spots.Count} spots)");
            return;
        }

        var session = args.Length > 1 && _players.TryGetSessionByUsername(args[1], out var named) ? named : shell.Player;
        if (session?.AttachedEntity is not { } body)
        {
            shell.WriteError("No attached player entity.");
            return;
        }

        var (label, coords) = _spots.Spots[index];
        _transform.SetCoordinates(body, coords);
        _transform.AttachToGridOrMap(body);
        shell.WriteLine($"ZPROF_GOTO {index} {label}");
    }
}
