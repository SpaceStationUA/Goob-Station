// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Medical.CrewMonitoring;

/// <summary>
/// Limits a crew monitor to personnel assigned to one of the configured departments.
/// </summary>
[RegisterComponent]
public sealed partial class CrewMonitoringDepartmentFilterComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<DepartmentPrototype>> ShownDepartments = new();
}
