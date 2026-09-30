// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration;
using Content.Server.Spawners.Components;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.Administration;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._Pirate.ZLevels.Profiling;

/// <summary>
/// Profiling helper: spreads every attached player over the station's spawn points, round-robin across
/// z-levels, so load tests see players on every floor instead of one pile at latejoin.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ZProfSpreadCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override string Command => "zprof_spread";
    public override string Description => "Spreads all players over station spawn points, round-robin across z-levels.";
    public override string Help => "zprof_spread [seed] [username=depth ...]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var seed = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 42;
        var rng = new System.Random(seed);
        var pinned = new Dictionary<string, int>();
        foreach (var arg in args.Skip(1))
        {
            var eq = arg.IndexOf('=');
            if (eq > 0 && int.TryParse(arg[(eq + 1)..], out var pinDepth))
                pinned[arg[..eq]] = pinDepth;
        }

        var byMap = new Dictionary<EntityUid, List<EntityCoordinates>>();
        var query = EntityManager.EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid is not { } mapUid || xform.GridUid is not { } gridUid)
                continue;

            if (!EntityManager.HasComponent<StationMemberComponent>(gridUid) &&
                !EntityManager.HasComponent<CEZLevelMapComponent>(mapUid))
                continue;

            if (!byMap.TryGetValue(mapUid, out var list))
                byMap[mapUid] = list = new List<EntityCoordinates>();

            list.Add(xform.Coordinates);
        }

        if (byMap.Count == 0)
        {
            shell.WriteError("No station spawn points found.");
            return;
        }

        var maps = byMap.Keys
            .OrderBy(Depth)
            .ToList();
        var sessions = _players.Sessions
            .Where(p => p.AttachedEntity != null)
            .OrderBy(p => p.Name)
            .ToList();
        var counts = new int[maps.Count];
        var next = 0;

        foreach (var session in sessions)
        {
            var mapIndex = pinned.TryGetValue(session.Name, out var want) && maps.FindIndex(m => Depth(m) == want) is >= 0 and var found
                ? found
                : next++ % maps.Count;
            var body = session.AttachedEntity!.Value;
            var spots = byMap[maps[mapIndex]];
            _transform.SetCoordinates(body, spots[rng.Next(spots.Count)]);
            _transform.AttachToGridOrMap(body);
            counts[mapIndex]++;
        }

        var summary = string.Join(", ", maps.Select((m, i) => $"depth {Depth(m)}: {counts[i]} players / {byMap[m].Count} spawns"));
        shell.WriteLine($"ZPROF_SPREAD {sessions.Count} players over {maps.Count} maps ({summary})");
    }

    private int Depth(EntityUid map)
    {
        return EntityManager.TryGetComponent<CEZLevelMapComponent>(map, out var z) ? z.Depth : 0;
    }
}
