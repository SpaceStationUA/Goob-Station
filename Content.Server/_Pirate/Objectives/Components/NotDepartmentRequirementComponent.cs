using Content.Server._Pirate.Objectives.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Objectives.Components;

/// <summary>
/// Requires that the player is not in a certain department to have this objective.
/// </summary>
[RegisterComponent, Access(typeof(NotDepartmentRequirementSystem))]
public sealed partial class NotDepartmentRequirementComponent : Component
{
    /// <summary>
    /// ID of the department to ban from having this objective.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DepartmentPrototype> Department;
}
