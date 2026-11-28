// SPDX-FileCopyrightText: 2025 Moffstation
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server._Pirate.GunGame.Components;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Pirate.GunGame;

public sealed partial class GunGameRuleSystem
{
    [Dependency] private readonly StationSystem _stations = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private EntityCoordinates? SelectSpawn(Entity<AvoidantSpawningComponent> gameRule, EntityUid? station)
    {
        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var possiblePositions = new List<EntityCoordinates>();
        var fallbackPositions = new List<EntityCoordinates>();

        while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (station != null && _stations.GetOwningStation(uid, xform) != station)
                continue;

            if (!BlacklistInRange(gameRule, (uid, xform)))
                possiblePositions.Add(xform.Coordinates);
            else
                fallbackPositions.Add(xform.Coordinates);
        }

        if (possiblePositions.Count == 0 && fallbackPositions.Count == 0)
            return null;

        return possiblePositions.Count > 0
            ? _random.Pick(possiblePositions)
            : _random.Pick(fallbackPositions);
    }

    private bool BlacklistInRange(Entity<AvoidantSpawningComponent> gameRule, Entity<TransformComponent> spawnPoint)
    {
        var entities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(spawnPoint.Comp.Coordinates, gameRule.Comp.Range, entities);
        return entities.Any(e => _whitelist.IsWhitelistPass(gameRule.Comp.Blacklist, e));
    }
}
