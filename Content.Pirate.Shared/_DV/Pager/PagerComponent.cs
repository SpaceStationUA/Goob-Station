using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Pager;

/// <summary>
/// DeltaV pager metadata used by ported prototypes.
/// </summary>
[RegisterComponent]
public sealed partial class PagerComponent : Component
{
    // Pirate: data-only compatibility; the full DeltaV pager device-network UI is not ported.
    [DataField]
    public Dictionary<string, string> Devices = new();

    [DataField]
    public HashSet<ProtoId<DepartmentPrototype>> AutoLinkDepartments = new();

    [DataField]
    public HashSet<ProtoId<JobPrototype>> AutoLinkJobs = new();
}
