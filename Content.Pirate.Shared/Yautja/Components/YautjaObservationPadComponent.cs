using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Yautja.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaObservationPadComponent : Component
{
    [DataField]
    public EntProtoId ProjectionPrototype = "GoobYautjaObservationProjection";

    /// <summary>Action granted while projected, for returning to the body.</summary>
    [DataField]
    public EntProtoId ReturnActionPrototype = "ActionYautjaObservationReturn";

    /// <summary>Cooldown before return is available after projecting.</summary>
    [DataField]
    public TimeSpan ReturnDelay = TimeSpan.FromSeconds(3);
}

public sealed partial class YautjaObservationProjectionEvent : InstantActionEvent;
