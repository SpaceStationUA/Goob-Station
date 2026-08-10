// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class LoadFarGridRuleSystem : StationEventSystem<LoadFarGridRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void Added(EntityUid uid, LoadFarGridRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, rule, args);

        if (!TryGetRandomStation(out var station) || !TryComp<StationDataComponent>(station, out var data) || data.Grids.Count == 0)
        {
            Log.Error($"{ToPrettyString(uid):rule} failed to find a station grid.");
            ForceEndSelf(uid, rule);
            return;
        }

        var aabb = new Box2();
        var map = MapId.Nullspace;
        foreach (var gridId in data.Grids)
        {
            if (map == MapId.Nullspace)
                map = Transform(gridId).MapID;

            var gridComp = Comp<MapGridComponent>(gridId);
            var gridAabb = _transform.GetWorldMatrix(gridId).TransformBox(gridComp.LocalAABB);
            aabb = aabb.Union(gridAabb);
        }

        var scale = comp.ReferenceWidth / aabb.Width;
        var modifier = comp.DistanceModifier * scale;
        var dist = MathF.Max(aabb.Height / 2f, aabb.Width / 2f) * modifier;
        var offset = RobustRandom.NextVector2(dist, dist * 2.5f);

        if (!_mapLoader.TryLoadGrid(map, comp.Path, out var grid, offset: aabb.Center + offset))
        {
            Log.Error($"{ToPrettyString(uid):rule} failed to load grid {comp.Path}.");
            ForceEndSelf(uid, rule);
            return;
        }

        var ev = new RuleLoadedGridsEvent(map, [grid.Value]);
        RaiseLocalEvent(uid, ref ev);
    }
}
