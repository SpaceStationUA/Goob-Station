// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Server.Antag;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Skia;

/// <summary>
/// Prefers station ventilation outlets for Skia's midround spawn and falls back to a safe hidden tile.
/// </summary>
public sealed class SkiaSpawnRuleSystem : GameRuleSystem<SkiaSpawnRuleComponent>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AntagBetterRandomSpawnSystem _betterRandomSpawn = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkiaSpawnRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
    }

    protected override void Added(
        EntityUid uid,
        SkiaSpawnRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (TryGetRandomStation(out var station))
        {
            var ventLocations = new List<MapCoordinates>();
            var locations = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
            while (locations.MoveNext(out _, out _, out var transform))
            {
                if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station != station)
                    continue;

                ventLocations.Add(_transform.GetMapCoordinates(transform));
            }

            if (ventLocations.Count > 0)
            {
                component.Coordinates = [_random.Pick(ventLocations)];
                return;
            }
        }

        if (_betterRandomSpawn.TryFindSafeRandomLocation(out var coordinates))
        {
            component.Coordinates = [_transform.ToMapCoordinates(coordinates)];
            return;
        }

        ForceEndSelf(uid, gameRule);
    }

    private static void OnSelectLocation(Entity<SkiaSpawnRuleComponent> entity, ref AntagSelectLocationEvent args)
    {
        if (entity.Comp.Coordinates is { } coordinates)
            args.Coordinates.AddRange(coordinates);
    }
}
