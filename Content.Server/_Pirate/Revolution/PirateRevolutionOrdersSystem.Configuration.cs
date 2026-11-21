// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using Content.Shared._Pirate.Revolution;
using Content.Shared.Cargo.Components;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using static Content.Shared._Pirate.Revolution.RevOrderAnnouncement;

namespace Content.Server._Pirate.Revolution;

public sealed partial class PirateRevolutionOrdersSystem
{
    private const int MaxManualSalaryCut = 100;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    internal sealed record OrderDraft(EntityUid Station, RevOrderPreview Preview,
        Func<PirateRevolutionOrdersComponent, string, OrderOrigin, bool> Apply);

    internal List<RevOrderOption> GetOrderOptions()
    {
        var station = GetPrimaryStation();
        var noStation = station == null ? Loc.GetString("pirate-rev-ui-no-primary-station") : null;
        var salary = station is { } salaryStation ? GetSalaryTargets(salaryStation) : new List<SalaryTarget>();
        var groups = salary.Where(target => target.CurrentCut < MaxManualSalaryCut)
            .Select(target =>
            {
                var department = Loc.GetString($"department-{target.Department}");
                var remaining = MaxManualSalaryCut - target.CurrentCut;
                var name = Loc.GetString("pirate-rev-ui-salary-group", ("target", department), ("total", target.CurrentCut));
                return new RevOrderGroup(target.Department, name,
                    Math.Min(SalaryCutPercent, remaining), remaining, new());
            }).OrderBy(group => group.Name).ToList();
        var salaryUnavailable = noStation ?? (groups.Count > 0 ? null
            : salary.Count > 0 ? Loc.GetString("pirate-rev-ui-salary-at-cap", ("max", MaxManualSalaryCut))
            : !HasComp<StationBankAccountComponent>(station)
                ? Loc.GetString("pirate-rev-ui-salary-no-bank")
                : Loc.GetString("pirate-rev-ui-salary-no-staff"));

        var options = new List<RevOrderOption>
        {
            new("salary", Loc.GetString("pirate-rev-ui-salary"), salaryUnavailable, groups),
        };
        var roster = station is { } crewStation ? GetDirectiveRoster(CurrentOrders(GetActiveOrderRules()), crewStation)
            : new DirectiveRoster(0, new());
        foreach (var kind in Enum.GetValues<PirateRevolutionDirectiveKind>())
        {
            var id = KindId(kind);
            var crew = GetDirectiveTargets(kind, roster)
                .Select(target => new RevOrderCrew(GetNetEntity(target.Mob),
                    Loc.GetString("pirate-rev-directive-target", ("name", target.Name), ("job", target.JobTitle))))
                .OrderBy(target => target.Name).ToList();
            var targets = new List<RevOrderGroup>();
            if (crew.Count > 0)
                targets.Add(new RevOrderGroup(null, string.Empty, MaxDirectivePercent,
                    MaxDirectivePercent, crew));

            var reason = noStation ?? (crew.Count == 0 ? Loc.GetString($"pirate-rev-ui-{id}-unavailable") : null);
            options.Add(new RevOrderOption(id, Loc.GetString($"pirate-rev-ui-{id}"), reason, targets,
                HasFine: kind == PirateRevolutionDirectiveKind.Tax));
        }

        var jobs = station is { } jobStation ? GetPresentJobs(jobStation) : new HashSet<string>();
        foreach (var order in Orders)
        {
            var target = GetFixedTarget(order);
            var reason = noStation ?? (IsEligible(order, jobs) ? null : Loc.GetString("pirate-rev-ui-required-jobs", ("jobs", target)));
            options.Add(new RevOrderOption(order.Id, Loc.GetString(order.DescriptionKey), reason, new(), target));
        }

        return options;
    }

    internal OrderDraft? PrepareOrder(RevOrderOption option, RevOrderGroup? group, int amount, int[] selectedCrew, int fine)
    {
        if (GetPrimaryStation() is not { } station || option.Unavailable != null
            || (option.HasFine ? !RevOrderTaxFine.IsValid(fine) : fine != 0))
            return null;

        if (Array.Find(Orders, order => order.Id == option.Id) is { } order)
        {
            return group == null && amount == 0 && selectedCrew.Length == 0 && IsEligible(order, GetPresentJobs(station))
                ? PrepareFixedOrder(station, order)
                : null;
        }

        if (group == null)
            return null;

        if (option.Id == "salary")
        {
            return selectedCrew.Length == 0 ? PrepareSalaryOrder(station, group.Department, amount)
                : null;
        }

        if (Enum.TryParse<PirateRevolutionDirectiveKind>(option.Id, true, out var kind))
            return PrepareDirectiveOrder(station, kind, group, amount, selectedCrew, fine);

        return null;
    }

    internal bool TryApplyOrder(OrderDraft draft, string message, ICommonSession admin)
    {
        if (GetPrimaryStation() != draft.Station)
            return false;

        var rules = GetActiveOrderRules();
        if (!draft.Apply(CurrentOrders(rules), message, OrderOrigin.Manual(admin)))
            return false;

        foreach (var rule in rules)
            Schedule(rule);

        return true;
    }

    private OrderDraft PrepareFixedOrder(EntityUid station, PirateRevolutionOrder order)
    {
        var text = Loc.GetString("pirate-rev-order-announcement", ("order", Loc.GetString(order.Key)));
        var preview = new RevOrderPreview(Loc.GetString("pirate-rev-order-sender"), GetFixedTarget(order), null,
            text, new());
        return new OrderDraft(station, preview, (comp, message, origin) =>
        {
            if (!IsEligible(order, GetPresentJobs(station)))
                return false;

            DispatchOrder(comp, station, order, origin, message);
            return true;
        });
    }

    private string GetFixedTarget(PirateRevolutionOrder order) =>
        order.Jobs.Length == 0 ? Loc.GetString("pirate-rev-ui-all-crew") : string.Join(", ", order.Jobs.Select(JobName));

    private OrderDraft? PrepareSalaryOrder(EntityUid station, string? department, int amount)
    {
        var target = GetSalaryTargets(station).Find(target => target.Department == department);
        if (target == null || amount < 1 || amount > MaxManualSalaryCut - target.CurrentCut)
            return null;

        var departmentName = Loc.GetString($"department-{target.Department}");
        var total = target.CurrentCut + amount;
        var values = new Dictionary<string, string>
        {
            ["department"] = departmentName,
            ["number"] = amount.ToString(CultureInfo.InvariantCulture),
            ["total"] = total.ToString(CultureInfo.InvariantCulture),
        };
        var template = Loc.GetString("pirate-rev-salary-cut", ("department", Token("department")),
            ("percent", Token("number")), ("total", Token("total")));
        var effect = Loc.GetString("pirate-rev-ui-salary-effect", ("number", amount), ("total", total));
        var preview = new RevOrderPreview(Loc.GetString("pirate-rev-order-sender"), departmentName, effect, template, values);
        return new OrderDraft(station, preview, (_, message, origin) =>
        {
            if (!GetSalaryTargets(station).Contains(target))
                return false;

            DispatchSalaryCut(target, amount, origin, message);
            return true;
        });
    }

    private OrderDraft? PrepareDirectiveOrder(EntityUid station, PirateRevolutionDirectiveKind kind, RevOrderGroup group,
        int amount, int[] selectedCrew, int fine)
    {
        var roster = GetDirectiveRoster(CurrentOrders(GetActiveOrderRules()), station);
        var candidates = GetDirectiveTargets(kind, roster);
        if (candidates.Count == 0)
            return null;

        List<DirectiveTarget> selected;
        if (selectedCrew.Length == 0)
        {
            if (amount is < 1 or > MaxDirectivePercent)
                return null;

            RobustRandom.Shuffle(candidates);
            selected = candidates.Take(DirectiveCount(roster.CrewCount, amount, candidates.Count)).ToList();
        }
        else
        {
            if (amount != 0 || selectedCrew.Length > group.Crew.Count || selectedCrew.Distinct().Count() != selectedCrew.Length)
            {
                return null;
            }

            var candidatesByMob = candidates.ToDictionary(target => GetNetEntity(target.Mob));
            selected = new List<DirectiveTarget>();
            foreach (var index in selectedCrew)
            {
                if (index < 0 || index >= group.Crew.Count
                    || !candidatesByMob.TryGetValue(group.Crew[index].Mob, out var match))
                {
                    return null;
                }

                selected.Add(match);
            }
        }

        var id = KindId(kind);
        var actions = selected.Select(target => PrepareDirectiveAction(kind, target, fine)).ToList();
        var names = string.Join(", ", actions.Select(action => action.Line));
        var values = new Dictionary<string, string>
        {
            ["crew"] = names,
            ["number"] = actions.Count.ToString(CultureInfo.InvariantCulture),
        };
        if (kind == PirateRevolutionDirectiveKind.Tax)
            values["fine"] = fine.ToString(CultureInfo.InvariantCulture);

        var effect = Loc.GetString("pirate-rev-ui-directive-effect", ("count", actions.Count), ("crew", names));
        var preview = new RevOrderPreview(Loc.GetString($"pirate-rev-directive-{id}-sender"), null, effect,
            Loc.GetString($"pirate-rev-directive-{id}", ("names", Token("crew"))), values);
        return new OrderDraft(station, preview, (comp, message, origin) =>
        {
            var currentRoster = GetDirectiveRoster(comp, station);
            var current = GetDirectiveTargets(kind, currentRoster).ToHashSet();
            if (selected.Any(target => !current.Contains(target))
                || (amount != 0 && selected.Count > DirectiveCount(currentRoster.CrewCount, amount, current.Count)))
                return false;

            return DispatchDirective(comp, station, kind, actions, origin, message);
        });
    }

    private string JobName(string id) =>
        _prototypes.TryIndex<JobPrototype>(id, out var job) ? job.LocalizedName : id;
}
