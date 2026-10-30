// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Medical.CrewMonitoring;

[RegisterComponent, Access(typeof(CrewMonitoringDepartmentFilterSystem))]
public sealed partial class CrewMonitoringDepartmentFilterComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<DepartmentPrototype>> ShownDepartments = new();

    [DataField]
    public bool MatchJobTitles = true;

    [DataField]
    public bool IncludeTrackingImplants;

    [DataField]
    public List<EntProtoId> SensorPrototypes = new();

    [DataField]
    public List<ProtoId<JobIconPrototype>> JobIcons = new();

    [DataField]
    public List<LocId> ExtraJobTitles = new();

    [ViewVariables]
    public List<string>? CachedJobTitles;
}
