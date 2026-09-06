// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Backrooms;
using Content.Server.Respawn;
using Content.Server.Station.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Backrooms;

/// <summary>
/// Teleports the eater to a random station tile when magic pizza is fully eaten.
/// </summary>
public sealed class TeleportToStationOnFullyEatenSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SpecialRespawnSystem _respawn = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TeleportToStationOnFullyEatenComponent, FullyEatenEvent>(OnFullyEaten);
    }

    private void OnFullyEaten(Entity<TeleportToStationOnFullyEatenComponent> ent, ref FullyEatenEvent args)
    {
        var eater = args.User;
        if (!Exists(eater) || TerminatingOrDeleted(eater))
            return;

        var stations = _station.GetStations();
        if (stations.Count == 0)
            return;

        var station = _random.Pick(stations);
        if (!TryComp<StationDataComponent>(station, out var data))
            return;

        var grid = _station.GetLargestGrid((station, data));
        if (grid == null || !TryComp<TransformComponent>(grid.Value, out var gridXform) || gridXform.MapUid == null)
            return;

        if (!_respawn.TryFindRandomTile(grid.Value, gridXform.MapUid.Value, 40, out var coords))
            return;

        _transform.SetCoordinates(eater, coords);
    }
}
