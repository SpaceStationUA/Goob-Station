// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Banking;
using Content.Shared.Cargo.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Revolution;

public sealed partial class PirateRevolutionOrdersSystem
{
    [Dependency] private readonly BankCardSystem _bankCards = default!;

    private sealed record SalaryTarget(EntityUid Station, string Department, int CurrentCut);

    private List<SalaryTarget> GetSalaryTargets(EntityUid station)
    {
        if (!HasComp<StationBankAccountComponent>(station))
            return new();

        var present = new HashSet<string>();
        var crew = EntityQueryEnumerator<ActorComponent, MobStateComponent>();
        while (crew.MoveNext(out var mob, out _, out var mobState))
        {
            if (!_mobState.IsAlive(mob, mobState) || !_mind.TryGetMind(mob, out var mind, out _)
                || !_job.MindTryGetJobId(mind, out var job) || job is not { } jobId
                || _bankCards.GetSalaryStation(mind, mob) != station
                || !_job.TryGetAllDepartments(jobId, out var departments))
            {
                continue;
            }

            foreach (var department in departments)
            {
                if (Departments.Contains(department.ID))
                    present.Add(department.ID);
            }
        }

        var cuts = CompOrNull<PirateRevolutionSalaryCutsComponent>(station);
        return present.Select(department => new SalaryTarget(station, department, cuts?.Cuts.GetValueOrDefault(department) ?? 0)).ToList();
    }

    private bool CutSalary(EntityUid station, OrderOrigin origin)
    {
        var available = GetSalaryTargets(station)
            .Where(target => target.CurrentCut + SalaryCutPercent <= MaxSalaryCut).ToList();
        if (available.Count == 0)
            return false;

        DispatchSalaryCut(RobustRandom.Pick(available), SalaryCutPercent, origin);
        return true;
    }

    private void DispatchSalaryCut(SalaryTarget target, int percent, OrderOrigin origin, string? announcement = null)
    {
        var total = target.CurrentCut + percent;
        EnsureComp<PirateRevolutionSalaryCutsComponent>(target.Station).Cuts[target.Department] = total;
        var sender = Loc.GetString("pirate-rev-order-sender");
        var message = announcement ?? Loc.GetString("pirate-rev-salary-cut",
            ("department", Loc.GetString($"department-{target.Department}")), ("percent", percent), ("total", total));
        Announce(target.Station, message, sender, origin, Loc.GetString("pirate-rev-ui-salary"));
        SendCommandFax(target.Station, Loc.GetString("pirate-rev-order-fax-name"), sender, message, new[] { target.Department });
    }
}
