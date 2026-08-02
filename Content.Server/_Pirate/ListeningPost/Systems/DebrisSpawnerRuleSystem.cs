// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.ListeningPost.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents.Events;
using Content.Shared.CCVar;
using Content.Shared.Salvage;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class DebrisSpawnerRuleSystem : StationEventSystem<DebrisSpawnerRuleComponent>
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DebrisSpawnerRuleComponent, RuleLoadedGridsEvent>(OnLoadedGrids);
    }

    private void OnLoadedGrids(Entity<DebrisSpawnerRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        if (_config.GetCVar<bool>(CCVars.WorldgenEnabled))
            return;

        var boxes = new List<Box2>(args.Grids.Count);
        foreach (var gridId in args.Grids)
        {
            var grid = Comp<MapGridComponent>(gridId);
            boxes.Add(Transform(gridId).WorldMatrix.TransformBox(grid.LocalAABB));
        }

        var salvageMaps = _proto.EnumeratePrototypes<SalvageMapPrototype>().ToList();
        if (salvageMaps.Count == 0)
            return;

        var spawnCount = Math.Min(ent.Comp.Count, salvageMaps.Count);
        for (var i = 0; i < spawnCount; i++)
        {
            var aabb = RobustRandom.Pick(boxes);
            var dist = MathF.Max(aabb.Height / 2f, aabb.Width / 2f) * ent.Comp.DistanceModifier;
            var offset = RobustRandom.NextVector2(dist, dist * 2.5f);
            var randomer = RobustRandom.NextVector2(dist, dist * 5f);
            var salvage = RobustRandom.PickAndTake(salvageMaps);
            if (!_mapLoader.TryLoadGrid(args.Map, salvage.MapPath, out _, offset: aabb.Center + offset + randomer))
                Log.Error($"{ToPrettyString(ent):rule} failed to load debris grid {salvage.MapPath}.");
        }
    }
}
