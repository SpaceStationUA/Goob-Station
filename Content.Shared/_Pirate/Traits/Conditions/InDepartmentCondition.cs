using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Conditions;

/// <summary>
/// Condition that checks if the player's job is in a specific department.
/// Use Invert = true to check if the player is NOT in the department.
/// </summary>
[DataDefinition]
public sealed partial class InDepartmentCondition : BaseTraitCondition
{
    /// <summary>
    /// The department prototype IDs to check for.
    /// </summary>
    [DataField("departments", required: true)]
    public List<ProtoId<DepartmentPrototype>> Departments = new();

    protected override bool EvaluateImplementation(TraitConditionContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.JobId))
            return false;

        foreach (var deptId in Departments)
        {
            if (ctx.Proto.TryIndex(deptId, out var department) && department.Roles.Contains(ctx.JobId))
                return true;
        }

        return false;
    }

    public override string GetTooltip(IPrototypeManager proto, ILocalizationManager loc)
    {
        var deptNames = new List<string>();
        var firstDeptColor = "#ffffff";

        foreach (var dept in Departments)
        {
            if (proto.TryIndex(dept, out var deptProto))
            {
                deptNames.Add(loc.GetString($"department-{deptProto.ID}"));
                if (firstDeptColor == "#ffffff")
                    firstDeptColor = deptProto.Color.ToHex();
            }
            else
            {
                deptNames.Add(dept.Id);
            }
        }

        var deptList = string.Join(", ", deptNames);

        return Invert
            ? loc.GetString("trait-condition-department-not", ("department", deptList), ("color", firstDeptColor))
            : loc.GetString("trait-condition-department-is", ("department", deptList), ("color", firstDeptColor));
    }
}


