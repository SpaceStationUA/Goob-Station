using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Tools;

/// <summary>Refines part of a stack without consuming its remainder.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StackRefinableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId RefineResult;

    [DataField]
    public int Cost = 2;

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
