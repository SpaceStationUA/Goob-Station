// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Common.AlternativeJobs;
using Content.Shared.Implants.Components;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Medical.CrewMonitoring;

public sealed class CrewMonitoringDepartmentFilterSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public bool IsExtraMatch(Entity<CrewMonitoringDepartmentFilterComponent> ent, SuitSensorStatus sensor)
    {
        if (TryGetEntity(sensor.SuitSensorUid, out var sensorUid))
        {
            if (ent.Comp.IncludeTrackingImplants && HasComp<SubdermalImplantComponent>(sensorUid))
                return true;

            if (ent.Comp.SensorPrototypes.Count > 0 &&
                MetaData(sensorUid.Value).EntityPrototype is { } sensorProto &&
                ent.Comp.SensorPrototypes.Contains(sensorProto.ID))
            {
                return true;
            }
        }

        foreach (var icon in ent.Comp.JobIcons)
        {
            if (sensor.JobIcon == icon.Id)
                return true;
        }

        return ent.Comp.MatchJobTitles && JobTitleMatches(ent.Comp, sensor.Job);
    }

    private bool JobTitleMatches(CrewMonitoringDepartmentFilterComponent comp, string jobTitle)
    {
        if (string.IsNullOrWhiteSpace(jobTitle))
            return false;

        foreach (var title in GetJobTitles(comp))
        {
            if (jobTitle.Contains(title, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private List<string> GetJobTitles(CrewMonitoringDepartmentFilterComponent comp)
    {
        if (comp.CachedJobTitles != null)
            return comp.CachedJobTitles;

        var jobIds = new HashSet<string>();
        var titles = new HashSet<string>();
        foreach (var departmentId in comp.ShownDepartments)
        {
            if (!_proto.TryIndex(departmentId, out var department))
                continue;

            foreach (var jobId in department.Roles)
            {
                if (!_proto.TryIndex<JobPrototype>(jobId, out var job))
                    continue;

                jobIds.Add(job.ID);
                AddTitle(titles, job.LocalizedName);
            }
        }

        foreach (var altJob in _proto.EnumeratePrototypes<AlternativeJobPrototype>())
        {
            if (jobIds.Contains(altJob.ParentJobId))
                AddTitle(titles, altJob.LocalizedJobName);
        }

        foreach (var title in comp.ExtraJobTitles)
        {
            AddTitle(titles, Loc.GetString(title));
        }

        comp.CachedJobTitles = new List<string>(titles);
        return comp.CachedJobTitles;
    }

    private static void AddTitle(HashSet<string> titles, string name)
    {
        name = name.Trim();
        if (name.Length > 0)
            titles.Add(name);
    }
}
