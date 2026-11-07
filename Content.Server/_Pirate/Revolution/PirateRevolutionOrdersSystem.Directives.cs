// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.CriminalRecords.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate.Origin.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles.Jobs;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Revolution;

public enum PirateRevolutionDirectiveKind : byte
{
    Tax,
    Termination,
    Passport,
    Crime,
}

public sealed partial class PirateRevolutionOrdersSystem
{
    [Dependency] private readonly CriminalRecordsConsoleSystem _recordsConsole = default!;
    [Dependency] private readonly CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // A new arrival gets time to settle in before an order can name them.
    private static readonly TimeSpan NewArrivalTime = TimeSpan.FromMinutes(5);

    private static readonly HashSet<string> ExemptDepartments = new() { "Command", "Security", "Silicon", "CentralCommand" };
    private const string Passenger = "Passenger";
    private const string BieselCitizenship = "Bieselite";

    // Space law charges of about 10 minutes, without the fine option.
    // Each charge has numbered reasons in the locale: pirate-rev-charge-<id>-1, -2, ...
    private static readonly string[] Charges =
    {
        "vandalism", "theft", "negligence", "hazard", "burglary", "brawl",
        "harassment", "narcotics", "assets", "weapons", "disloyalty",
    };

    private readonly Dictionary<EntityUid, TimeSpan> _arrivals = new();

    private sealed class DirectiveTarget(EntityUid mob, EntityUid mind, StationRecordKey key, string job)
    {
        public readonly EntityUid Mob = mob;
        public readonly EntityUid Mind = mind;
        public readonly StationRecordKey Key = key;
        public readonly string Job = job;
        public string JobTitle = string.Empty;
    }

    private void InitializeDirectives()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(ev => _arrivals[ev.Mob] = Timing.CurTime);
    }

    private bool TryIssueDirective(PirateRevolutionOrdersComponent comp, PirateRevolutionDirectiveKind kind)
    {
        // Everyone counts for the percentage, but only some may be named.
        var crew = new Dictionary<EntityUid, int>();
        var candidates = new List<DirectiveTarget>();
        var query = EntityQueryEnumerator<ActorComponent, HumanoidAppearanceComponent, MobStateComponent>();
        while (query.MoveNext(out var mob, out _, out _, out var mobState))
        {
            if (!_mind.TryGetMind(mob, out var mindId, out _) || !_job.MindTryGetJobId(mindId, out var jobId)
                || jobId is not { } job || !TryGetRecordKey(mob, out var key))
            {
                continue;
            }

            crew[key.Value.OriginStation] = crew.GetValueOrDefault(key.Value.OriginStation) + 1;
            if (!_mobState.IsAlive(mob, mobState) || comp.Targeted.Contains(mindId) || IsExempt(job)
                || _arrivals.TryGetValue(mob, out var arrived) && Timing.CurTime - arrived < NewArrivalTime)
            {
                continue;
            }

            var target = new DirectiveTarget(mob, mindId, key.Value, job);
            if (CanTarget(kind, target))
                candidates.Add(target);
        }

        if (candidates.Count == 0)
            return false;

        // One station per directive, so the order names people of the station that gets it.
        var station = RobustRandom.Pick(candidates).Key.OriginStation;
        candidates.RemoveAll(c => c.Key.OriginStation != station);
        RobustRandom.Shuffle(candidates);
        var percent = RobustRandom.Next(1, 6);
        var count = Math.Clamp((int) Math.Round(crew[station] * percent / 100d), 1, candidates.Count);

        var sender = Loc.GetString($"pirate-rev-directive-{KindId(kind)}-sender");
        var lines = new List<string>();
        var jobs = new List<string>();
        foreach (var target in candidates.Take(count))
        {
            if (!TryApply(kind, target, sender, out var line))
                continue;

            comp.Targeted.Add(target.Mind);
            lines.Add(line);
            jobs.Add(target.Job);
        }

        if (lines.Count == 0)
            return false;

        var message = Loc.GetString($"pirate-rev-directive-{KindId(kind)}", ("names", string.Join(", ", lines)));
        Announce(message, sender);
        SendCommandFax(sender, sender, message, JobDepartments(jobs));
        return true;
    }

    private bool TryGetRecordKey(EntityUid mob, [NotNullWhen(true)] out StationRecordKey? key)
    {
        key = null;
        var name = Name(mob);

        // A stolen ID card names somebody else.
        if (_idCard.TryFindIdCard(mob, out var idCard)
            && TryComp<StationRecordKeyStorageComponent>(idCard, out var storage)
            && storage.Key is { } cardKey
            && _records.TryGetRecord<GeneralStationRecord>(cardKey, out var general)
            && general.Name == name)
        {
            key = cardKey;
            return true;
        }

        var query = EntityQueryEnumerator<StationRecordsComponent>();
        while (query.MoveNext(out var station, out var records))
        {
            if (_records.GetRecordByName(station, name, records) is not { } id)
                continue;

            key = new StationRecordKey(id, station);
            return true;
        }

        return false;
    }

    private bool IsExempt(string job)
    {
        return _job.TryGetAllDepartments(job, out var departments)
            && departments.Any(department => ExemptDepartments.Contains(department.ID));
    }

    private bool CanTarget(PirateRevolutionDirectiveKind kind, DirectiveTarget target)
    {
        if (!_records.TryGetRecord<GeneralStationRecord>(target.Key, out var general)
            || !_records.TryGetRecord<CriminalRecord>(target.Key, out var criminal))
        {
            return false;
        }

        target.JobTitle = general.JobTitle;
        switch (kind)
        {
            case PirateRevolutionDirectiveKind.Tax:
                return true;
            case PirateRevolutionDirectiveKind.Termination:
                return target.Job != Passenger && criminal.Status == SecurityStatus.None;
            case PirateRevolutionDirectiveKind.Crime:
                return criminal.Status == SecurityStatus.None;
            case PirateRevolutionDirectiveKind.Passport:
                if (general.Citizenship == BieselCitizenship)
                    return false;

                // Only a passport that the person has now can be confiscated.
                var passports = EntityQueryEnumerator<PassportComponent>();
                while (passports.MoveNext(out var passport, out var passportComp))
                {
                    if (passportComp.OwnerProfile?.Name == general.Name && _transform.ContainsEntity(target.Mob, passport))
                        return true;
                }
                return false;
            default:
                return false;
        }
    }

    private bool TryApply(PirateRevolutionDirectiveKind kind, DirectiveTarget target, string sender,
        [NotNullWhen(true)] out string? line)
    {
        line = null;
        var name = _records.RecordName(target.Key);
        switch (kind)
        {
            case PirateRevolutionDirectiveKind.Tax:
                var fine = RobustRandom.Next(10, 21) * 50;
                if (_recordsConsole.IssueCitation(target.Key, Loc.GetString("pirate-rev-directive-tax-reason"),
                        Loc.GetString("pirate-rev-directive-tax-details"), fine, sender, true) == null)
                {
                    return false;
                }

                line = Loc.GetString("pirate-rev-directive-tax-target", ("name", name), ("job", target.JobTitle), ("fine", fine));
                return true;
            case PirateRevolutionDirectiveKind.Termination:
                if (!_criminalRecords.TryChangeStatus(target.Key, SecurityStatus.Demote,
                        Loc.GetString("pirate-rev-directive-termination-reason"), sender))
                {
                    return false;
                }
                break;
            case PirateRevolutionDirectiveKind.Crime:
                var charge = PickCharge();
                if (!_criminalRecords.TryChangeStatus(target.Key, SecurityStatus.Wanted,
                        Loc.GetString("pirate-rev-directive-crime-reason", ("charge", charge)), sender))
                {
                    return false;
                }

                line = Loc.GetString("pirate-rev-directive-crime-target", ("name", name), ("job", target.JobTitle), ("charge", charge));
                return true;
        }

        line = Loc.GetString("pirate-rev-directive-target", ("name", name), ("job", target.JobTitle));
        return true;
    }

    private string PickCharge()
    {
        var charge = RobustRandom.Pick(Charges);
        var reasons = new List<string>();
        while (Loc.TryGetString($"pirate-rev-charge-{charge}-{reasons.Count + 1}", out var reason))
        {
            reasons.Add(reason);
        }

        var articles = Loc.GetString($"pirate-rev-charge-{charge}");
        return reasons.Count == 0
            ? articles
            : Loc.GetString("pirate-rev-charge", ("articles", articles), ("reason", RobustRandom.Pick(reasons)));
    }

    private static string KindId(PirateRevolutionDirectiveKind kind)
    {
        return kind.ToString().ToLowerInvariant();
    }
}
