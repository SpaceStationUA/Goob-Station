// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.CriminalRecords.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Pirate.Revolution;
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
    private const int MaxDirectivePercent = 5;

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

    private sealed record DirectiveTarget(EntityUid Mob, EntityUid Mind, StationRecordKey Key,
        string Job, string Name, string JobTitle);

    private sealed record DirectiveRoster(int CrewCount, List<DirectiveTarget> Eligible);

    private sealed record DirectiveAction(DirectiveTarget Target, string Line, int Fine = 0, string? Charge = null);

    private void InitializeDirectives()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(ev => _arrivals[ev.Mob] = Timing.CurTime);
    }

    private DirectiveRoster GetDirectiveRoster(PirateRevolutionOrdersComponent comp, EntityUid station)
    {
        // Everyone counts for the percentage, but only some may be named.
        var crewCount = 0;
        var eligible = new List<DirectiveTarget>();
        var query = EntityQueryEnumerator<ActorComponent, HumanoidAppearanceComponent, MobStateComponent>();
        while (query.MoveNext(out var mob, out _, out _, out var mobState))
        {
            if (!_mind.TryGetMind(mob, out var mindId, out _) || !_job.MindTryGetJobId(mindId, out var jobId)
                || jobId is not { } job || _bankCards.GetSalaryStation(mindId, mob) != station
                || !TryGetRecordKey(mob, station, out var key))
            {
                continue;
            }

            crewCount++;
            if (!_mobState.IsAlive(mob, mobState) || comp.Targeted.Contains(mindId) || IsExempt(job)
                || _arrivals.TryGetValue(mob, out var arrived) && Timing.CurTime - arrived < NewArrivalTime
                || !_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general))
            {
                continue;
            }

            eligible.Add(new DirectiveTarget(mob, mindId, key.Value, job, general.Name, general.JobTitle));
        }

        return new DirectiveRoster(crewCount, eligible);
    }

    private List<DirectiveTarget> GetDirectiveTargets(PirateRevolutionDirectiveKind kind, DirectiveRoster roster)
    {
        return roster.Eligible
            .Where(target => CanTarget(kind, target))
            .DistinctBy(target => target.Key)
            .ToList();
    }

    private static int DirectiveCount(int crew, int percent, int candidates)
    {
        return Math.Clamp((int) Math.Round(crew * percent / 100d), 1, candidates);
    }

    private bool TryIssueDirective(PirateRevolutionOrdersComponent comp, EntityUid station,
        PirateRevolutionDirectiveKind kind, OrderOrigin origin)
    {
        var roster = GetDirectiveRoster(comp, station);
        var candidates = GetDirectiveTargets(kind, roster);
        if (candidates.Count == 0)
            return false;

        RobustRandom.Shuffle(candidates);
        var percent = RobustRandom.Next(1, MaxDirectivePercent + 1);
        var count = DirectiveCount(roster.CrewCount, percent, candidates.Count);
        return DispatchDirective(comp, station, kind,
            candidates.Take(count).Select(target => PrepareDirectiveAction(kind, target)).ToList(), origin);
    }

    private bool DispatchDirective(PirateRevolutionOrdersComponent comp, EntityUid station, PirateRevolutionDirectiveKind kind,
        List<DirectiveAction> actions, OrderOrigin origin, string? announcement = null)
    {
        var sender = Loc.GetString($"pirate-rev-directive-{KindId(kind)}-sender");
        var lines = new List<string>();
        var jobs = new List<string>();
        foreach (var action in actions)
        {
            if (!TryApplyDirectiveAction(kind, action, sender))
                continue;

            comp.Targeted.Add(action.Target.Mind);
            lines.Add(action.Line);
            jobs.Add(action.Target.Job);
        }

        if (lines.Count == 0 || (announcement != null && lines.Count != actions.Count))
            return false;

        var message = announcement ?? Loc.GetString($"pirate-rev-directive-{KindId(kind)}", ("names", string.Join(", ", lines)));
        Announce(station, message, sender, origin, Loc.GetString($"pirate-rev-ui-{KindId(kind)}"));
        SendCommandFax(station, sender, sender, message, JobDepartments(jobs));
        return true;
    }

    private bool TryGetRecordKey(EntityUid mob, EntityUid station, [NotNullWhen(true)] out StationRecordKey? key)
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
            if (cardKey.OriginStation != station)
                return false;

            key = cardKey;
            return true;
        }

        if (TryComp<StationRecordsComponent>(station, out var records)
            && _records.GetRecordByName(station, name, records) is { } id)
        {
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

    private DirectiveAction PrepareDirectiveAction(PirateRevolutionDirectiveKind kind, DirectiveTarget target,
        int configuredFine = 0)
    {
        if (kind == PirateRevolutionDirectiveKind.Tax)
        {
            var fine = configuredFine != 0 ? configuredFine
                : RobustRandom.Next(RevOrderTaxFine.RandomMin / RevOrderTaxFine.Step,
                    RevOrderTaxFine.RandomMax / RevOrderTaxFine.Step + 1) * RevOrderTaxFine.Step;
            var line = Loc.GetString("pirate-rev-directive-tax-target",
                ("name", target.Name), ("job", target.JobTitle), ("fine", fine));
            return new DirectiveAction(target, line, Fine: fine);
        }

        if (kind == PirateRevolutionDirectiveKind.Crime)
        {
            var charge = PickCharge();
            var line = Loc.GetString("pirate-rev-directive-crime-target",
                ("name", target.Name), ("job", target.JobTitle), ("charge", charge));
            return new DirectiveAction(target, line, Charge: charge);
        }

        return new DirectiveAction(target,
            Loc.GetString("pirate-rev-directive-target", ("name", target.Name), ("job", target.JobTitle)));
    }

    private bool TryApplyDirectiveAction(PirateRevolutionDirectiveKind kind, DirectiveAction action, string sender)
    {
        var key = action.Target.Key;
        switch (kind)
        {
            case PirateRevolutionDirectiveKind.Tax:
                return _recordsConsole.IssueCitation(key, Loc.GetString("pirate-rev-directive-tax-reason"),
                    Loc.GetString("pirate-rev-directive-tax-details"), action.Fine, sender, true) != null;
            case PirateRevolutionDirectiveKind.Termination:
                return _criminalRecords.TryChangeStatus(key, SecurityStatus.Demote,
                    Loc.GetString("pirate-rev-directive-termination-reason"), sender);
            case PirateRevolutionDirectiveKind.Crime:
                return _criminalRecords.TryChangeStatus(key, SecurityStatus.Wanted,
                    Loc.GetString("pirate-rev-directive-crime-reason", ("charge", action.Charge!)), sender);
            case PirateRevolutionDirectiveKind.Passport:
                return true;
            default:
                return false;
        }
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
