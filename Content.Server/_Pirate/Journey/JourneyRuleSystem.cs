// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Shuttles.Warp;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.StationEvents;
using Content.Shared._Pirate.Journey;
using Content.Shared._Pirate.Shuttles.Warp;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Journey;

/// <summary>
///     The mode side of a sector jump. The rule tracks the sector the station is in, applies the sector's
///     event filters, and sends the warp to every client.
/// </summary>
/// <remarks>
///     The server cannot draw parallax. On a jump it starts the event filters, broadcasts
///     <see cref="JourneySectorJumpEvent"/>, and each client plays the warp. A sector's name and
///     description are localized through its locale keys.
/// </remarks>
public sealed class JourneyRuleSystem : GameRuleSystem<JourneyRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EventManagerSystem _events = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly WarpDriveSystem _warp = default!;

    private static readonly TimeSpan HoldMargin = TimeSpan.FromSeconds(15);

    public override void Initialize()
    {
        base.Initialize();

        // Players who spawn after the round starts need the sky that is already up.
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);

        // Restart cleanup bypasses Ended for deleted rule entities.
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearFilters());
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var rules = EntityQueryEnumerator<JourneyRuleComponent, GameRuleComponent>();
        while (rules.MoveNext(out var uid, out var journey, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            BroadcastSky(journey);
            return;
        }
    }

    /// <summary>
    ///     Tells every client which sky the ship is under: a silent fade, not a jump. Clients that already
    ///     show this sky skip it.
    /// </summary>
    private void BroadcastSky(JourneyRuleComponent component)
    {
        // A mid-warp arrival comes out under the destination.
        var sectorId = component.PendingSector != string.Empty ? component.PendingSector : component.Sector;
        if (sectorId == string.Empty || !_proto.TryIndex<JourneySectorPrototype>(sectorId, out var sector))
            return;

        RaiseNetworkEvent(new JourneySectorSetEvent(sector.Parallax));
    }

    protected override void Started(EntityUid uid, JourneyRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        // The ship already sits in its opening sector, so no warp is broadcast. The rules apply from minute one.
        // When the gate allows, the opening prefers a quiet sector.
        if (!TryPickSector(component, out var picked, force: false, preferQuiet: true))
        {
            Log.Error($"journey cannot start: no sector is allowed ({string.Join(", ", component.AllowedSectors)})");
            return;
        }

        var sector = picked!;
        BuildMajorUnion();
        component.PendingSector = string.Empty;
        component.ApplyAt = TimeSpan.Zero;
        ApplySector(component, sector, journey: false);
        BroadcastSky(component);
    }

    /// <summary>
    ///     The union of every sector's majors. A major that fired in the previous sector still holds back
    ///     this sector's majors. Prototypes do not change mid-round, so this is built once per rule start.
    /// </summary>
    private void BuildMajorUnion()
    {
        _events.SectorAllMajorEvents.Clear();
        foreach (var proto in _proto.EnumeratePrototypes<JourneySectorPrototype>())
        {
            foreach (var major in proto.MajorEvents)
                _events.SectorAllMajorEvents.Add(major.Id);
        }
    }

    protected override void Ended(EntityUid uid, JourneyRuleComponent component, GameRuleComponent gameRule,
        GameRuleEndedEvent args)
    {
        ClearFilters();
        component.Sector = string.Empty;
        component.PendingSector = string.Empty;
        component.ApplyAt = TimeSpan.Zero;
    }

    private void ClearFilters()
    {
        _events.SectorBlockedEvents.Clear();
        _events.SectorAllowedEvents = null;
        _events.SectorMajorEvents.Clear();
        _events.SectorAllMajorEvents.Clear();
        _events.SectorEnteredAt = TimeSpan.Zero;
    }

    /// <summary>
    ///     Jumps the station into a sector. The warp fires everywhere now, and the sector's rules apply
    ///     when the ship comes out of it.
    /// </summary>
    /// <returns>Null on success, otherwise the reason it was refused.</returns>
    public string? Jump(EntityUid uid, JourneyRuleComponent component, string sectorId, bool force = false)
    {
        if (!GameTicker.IsGameRuleActive(uid, CompOrNull<GameRuleComponent>(uid)))
            return "the journey rule is not active; run `journey start` first";

        if (!_proto.HasIndex<JourneySectorPrototype>(sectorId))
            return $"no journey sector called '{sectorId}'";

        var destination = component.PendingSector != string.Empty ? component.PendingSector : component.Sector;
        if (sectorId == destination)
            return $"the station already sits in {sectorId}";

        if (!force && component.AllowedSectors.Count > 0 && !component.AllowedSectors.Contains(sectorId))
            return $"{sectorId} is not in the round's allowed sector list";

        StartJump(component, sectorId);
        return null;
    }

    private void StartJump(JourneyRuleComponent component, string sectorId)
    {
        var sector = _proto.Index<JourneySectorPrototype>(sectorId);

        // Whatever held in the previous sector stops the moment the engines fire. Nothing lands on the
        // crew in flight: an empty allow-list (not null) holds the schedulers shut while the
        // destination's rules wait for the warp to exit.
        _events.SectorBlockedEvents.Clear();
        _events.SectorAllowedEvents = new HashSet<string>();
        _events.SectorMajorEvents.Clear();

        component.Sector = sectorId;
        component.PendingSector = sectorId;
        component.ApplyAt = GameTicker.RoundDuration()
            + TimeSpan.FromSeconds(component.WarpRunUpSeconds + component.WarpDuration);

        RaiseNetworkEvent(new JourneySectorJumpEvent(sector.Parallax, component.WarpDuration, component.WarpRunUpSeconds));

        var depart = Loc.GetString("journey-departure-announcement",
            ("sector", Loc.GetString(sector.Name)),
            ("seconds", (int) component.WarpRunUpSeconds));
        foreach (var station in _station.GetStations())
        {
            _chat.DispatchStationAnnouncement(station, depart,
                sender: Loc.GetString("comms-console-announcement-title-centcom"));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = GameTicker.RoundDuration();
        var query = EntityQueryEnumerator<JourneyRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var component, out var ruleComponent))
        {
            if (!GameTicker.IsGameRuleActive(uid, ruleComponent))
                continue;

            if (component.PendingSector == string.Empty
                || now < component.ApplyAt
                || !_proto.TryIndex<JourneySectorPrototype>(component.PendingSector, out var pending))
            {
                continue;
            }

            component.PendingSector = string.Empty;
            ApplySector(component, pending, journey: true);

            // Clients already faded on the warp. This covers anyone who joined mid-flight.
            BroadcastSky(component);
        }
    }

    /// <summary>
    ///     Crew-driven jumps: an engaged drive holding a course commits the jump the moment the field is up.
    ///     One drive per hull is assumed. A warp already in flight blocks a second commitment.
    /// </summary>
    /// <remarks>
    ///     Every refusal lands on the drive's console, never silently.
    /// </remarks>
    public void CommitDrive(Entity<WarpDriveComponent> drive)
    {
        foreach (var journey in EntityManager.EntityQuery<JourneyRuleComponent>(true))
        {
            var ruleUid = journey.Owner;
            if (!EntityManager.TryGetComponent<GameRuleComponent>(ruleUid, out var gameRule)
                || !GameTicker.IsGameRuleActive(ruleUid, gameRule))
            {
                continue;
            }

            CommitDrive(journey, drive);
            return;
        }

        _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-no-journey"));
    }

    private void CommitDrive(JourneyRuleComponent component, Entity<WarpDriveComponent> drive)
    {
        if (component.PendingSector != string.Empty)
        {
            _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-already-flying"));
            return;
        }

        // A late press that arrives after the field has dropped is refused rather than flown half-fielded.
        if (drive.Comp.DriveState != WarpDriveState.Engaged)
        {
            _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-field-down"));
            return;
        }

        if (drive.Comp.CourseSector == string.Empty)
        {
            _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-no-course"));
            return;
        }

        if (!_proto.TryIndex<JourneySectorPrototype>(drive.Comp.CourseSector, out var target))
        {
            _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-no-such-system"));
            return;
        }

        // The gate can change after the course is laid in, so it is checked again here.
        if (drive.Comp.CourseSector == component.Sector)
        {
            _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-already-there"));
            return;
        }

        if (component.AllowedSectors.Count > 0 && !component.AllowedSectors.Contains(drive.Comp.CourseSector))
        {
            _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-gated"));
            return;
        }

        // A core that cannot survive the crossing waits to cool. Firing anyway would schedule a detonation
        // in transit. The hold covers the run-up, the warp and a margin.
        var hold = TimeSpan.FromSeconds(component.WarpRunUpSeconds + component.WarpDuration) + HoldMargin;
        if (drive.Comp.Heat + (float) hold.TotalSeconds * drive.Comp.HeatRiseRate >= 1f)
        {
            _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-too-hot"));
            return;
        }

        StartJump(component, target.ID);

        _warp.SetNotice(drive, Loc.GetString("warp-drive-notice-committed", ("sector", Loc.GetString(target.Name))));
        Log.Info($"journey: warp drive committed to {target.ID}");
        _warp.SetInTransit(drive);
    }

    /// <summary>
    ///     Applies a sector's rules: the event filters and the majors' grace window. Runs at round start,
    ///     for the sector the ship already sits in, and again when the ship leaves a warp.
    /// </summary>
    private void ApplySector(JourneyRuleComponent component, JourneySectorPrototype sector, bool journey)
    {
        component.Sector = sector.ID;
        component.SectorEnteredAt = GameTicker.RoundDuration();

        // The schedulers read these filters before any weight is rolled, so both run under the same sector.
        _events.SectorBlockedEvents.Clear();
        _events.SectorAllowedEvents = sector.AllowedEvents.Count > 0
            ? new HashSet<string>(sector.AllowedEvents.Select(x => x.Id))
            : null;
        foreach (var blocked in sector.BlockedEvents)
            _events.SectorBlockedEvents.Add(blocked.Id);

        _events.SectorMajorEvents.Clear();
        foreach (var major in sector.MajorEvents)
            _events.SectorMajorEvents.Add(major.Id);
        _events.SectorEnteredAt = component.SectorEnteredAt;
        _events.SectorMajorDelayMinutes = sector.MajorDelayMinutes;

        Log.Info($"journey: entered {sector.ID} ({sector.Name}): " +
                 $"{sector.BlockedEvents.Count} blocked, " +
                 $"{sector.AllowedEvents.Count} allowlisted, " +
                 $"{sector.MajorEvents.Count} majors after {sector.MajorDelayMinutes:0.#} min");

        if (!journey)
            return;

        // The crew hears where they came out, at the exit rather than at the start.
        var message = Loc.GetString("journey-arrival-announcement", ("sector", Loc.GetString(sector.Name)));
        var stations = _station.GetStations();
        Log.Info($"journey: announcing arrival in {sector.ID} to {stations.Count} station(s)");
        foreach (var station in stations)
        {
            _chat.DispatchStationAnnouncement(station, message,
                sender: Loc.GetString("comms-console-announcement-title-centcom"));
        }

        // The drive that flew the jump shuts down onto its transit cooldown. Heat is kept.
        var transits = EntityManager.EntityQuery<WarpDriveComponent>();
        foreach (var drive in transits)
        {
            if (drive.DriveState == WarpDriveState.InTransit)
                _warp.CompleteTransit((drive.Owner, drive));
        }
    }

    private bool TryPickSector(JourneyRuleComponent component, out JourneySectorPrototype? sector, bool force, bool preferQuiet = false)
    {
        sector = null;

        var candidates = _proto.EnumeratePrototypes<JourneySectorPrototype>()
            .Where(x => force
                || component.AllowedSectors.Count == 0
                || component.AllowedSectors.Contains(x.ID))
            .Where(x => x.ID != component.Sector)
            .ToList();

        if (candidates.Count == 0)
            return false;

        // The opening only: when a quiet sector is available, loud ones sit out. Sectors that do not
        // allow a start sit out too, unless nothing else is left.
        if (preferQuiet && candidates.Any(x => x.MajorEvents.Count == 0))
        {
            var quiet = candidates.Where(x => x.MajorEvents.Count == 0).ToList();
            var openers = quiet.Where(x => x.AllowStart).ToList();
            candidates = openers.Count > 0 ? openers : quiet;
        }

        // A weighted roll over the positive weights. If none is positive, or only one sector is on the
        // table, fall back to a flat pick so the mode can always find a destination.
        var pool = candidates.Where(x => x.Weight > 0).ToList();
        if (pool.Count == 0)
            pool = candidates;
        else if (pool.Count == 1)
        {
            sector = pool[0];
            return true;
        }

        var total = pool.Sum(x => x.Weight);
        var roll = _random.NextFloat(total);
        foreach (var candidate in pool)
        {
            roll -= candidate.Weight;
            if (roll <= 0)
            {
                sector = candidate;
                return true;
            }
        }

        sector = pool[^1];
        return true;
    }
}
