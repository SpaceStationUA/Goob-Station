using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Tools;

/// <summary>
/// Refines part of a stack with a tool, leaving the remainder alone. The vanilla ToolRefinableComponent
/// deletes the whole entity, which would eat an entire stack of rods to produce a single sheet.
/// Mirrors SS13's rods.dm welder_act: 2 rods -> 1 steel sheet, repeatable down the stack.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StackRefinableComponent : Component
{
    /// <summary>
    /// What a single refine produces.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId RefineResult;

    /// <summary>
    /// How many units of this stack one refine consumes.
    /// </summary>
    [DataField]
    public int Cost = 2;

    /// <summary>
    /// How many <see cref="RefineResult"/> a single refine spawns.
    /// </summary>
    [DataField]
    public int ResultAmount = 1;

    [DataField]
    public float RefineTime = 1f;

    [DataField]
    public float RefineFuel = 1f;

    [DataField]
    public ProtoId<ToolQualityPrototype> QualityNeeded = "Welding";
}

[Serializable, NetSerializable]
public sealed partial class StackRefineDoAfterEvent : SimpleDoAfterEvent
{
}
