// SPDX-License-Identifier: AGPL-3.0-or-later
// Attribution: AdventureTimeSS14/space_station_ADT PR #3338 (MIT).

using Content.Pirate.Shared.AlertLevel;
using Content.Server.AlertLevel;
using Content.Server.GameTicking;
using Content.Shared.Station;
using Robust.Shared.Player;

namespace Content.Pirate.Server.AlertLevel;

public sealed partial class StationAlertLevelCacheSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    // Use the default map so CentComm's alert state cannot become the main station's level.
    public EntityUid? GetMainStation()
    {
        var station = _station.GetStationInMap(_gameTicker.DefaultMap);
        return station != null && HasComp<AlertLevelComponent>(station) ? station : null;
    }

    public string GetLevel()
    {
        return GetMainStation() is { } station && TryComp<AlertLevelComponent>(station, out var alert)
            ? alert.CurrentLevel
            : string.Empty;
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        if (args.Station != GetMainStation())
            return;

        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var player, out _))
        {
            SetLevel(player, args.AlertLevel);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        SetLevel(args.Entity, GetLevel());
    }

    private void SetLevel(EntityUid player, string level)
    {
        var cache = EnsureComp<StationAlertLevelCacheComponent>(player);
        if (cache.CurrentLevel == level)
            return;

        cache.CurrentLevel = level;
        Dirty(player, cache);
    }
}
