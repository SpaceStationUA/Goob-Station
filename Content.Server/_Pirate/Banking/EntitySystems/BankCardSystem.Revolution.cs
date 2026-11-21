// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Revolution;
using Content.Shared.Mind;

namespace Content.Server._Pirate.Banking;

public sealed partial class BankCardSystem
{
    private readonly Dictionary<EntityUid, EntityUid> _salaryStations = new();

    internal EntityUid? GetSalaryStation(EntityUid mind, EntityUid mob)
    {
        return _salaryStations.TryGetValue(mind, out var station) ? station : _station.GetOwningStation(mob);
    }

    // All departments count, so a Command cut also reaches the Captain, who has no primary department.
    private int ApplyRevolutionSalaryCut(EntityUid mind, int salary)
    {
        if (!TryComp<MindComponent>(mind, out var mindComp) || mindComp.CurrentEntity is not { } mob ||
            !_job.MindTryGetJob(mind, out var job) || !_job.TryGetAllDepartments(job.ID, out var departments) ||
            !TryComp<PirateRevolutionSalaryCutsComponent>(
                GetSalaryStation(mind, mob), out var cuts))
            return salary;

        var percent = 0;
        foreach (var department in departments)
        {
            if (cuts.Cuts.TryGetValue(department.ID, out var cut))
                percent = Math.Max(percent, cut);
        }

        return (int) Math.Floor(salary * (100 - percent) / 100d);
    }
}
