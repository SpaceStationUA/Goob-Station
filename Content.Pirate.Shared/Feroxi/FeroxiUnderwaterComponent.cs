using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Feroxi;

/// <summary>
/// Lets a Feroxi dive under a tile with <see cref="Content.Pirate.Shared.Fluids.FloorWaterComponent"/>,
/// hiding their body (only their fin stays visible), swimming faster and hitting harder unarmed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FeroxiUnderwaterComponent : Component
{
    /// <summary>
    /// Shown while standing in water on foot. Both actions raise the same toggle event.
    /// </summary>
    [DataField]
    public EntProtoId DiveAction = "ActionFeroxiDive";

    [DataField, AutoNetworkedField]
    public EntityUid? DiveActionEntity;

    /// <summary>
    /// Replaces <see cref="DiveAction"/> while underwater, so the button always describes what it will
    /// actually do rather than being one ambiguous toggle.
    /// </summary>
    [DataField]
    public EntProtoId SurfaceAction = "ActionFeroxiSurface";

    [DataField, AutoNetworkedField]
    public EntityUid? SurfaceActionEntity;

    /// <summary>
    /// The prototype for the alert indicating the user that they are underwater.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> UnderwaterAlert = "FeroxiUnderwater";

    /// <summary>
    /// Is the entity currently underwater?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsUnderwater;

    /// <summary>
    /// The water tile entity currently keeping this mob eligible to dive/stay under.
    /// Null when the mob isn't standing in any water.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? WaterEntity;

    /// <summary>
    /// Walk/sprint speed multiplier applied while underwater.
    /// </summary>
    [DataField]
    public float SpeedModifier = 2f;

    /// <summary>
    /// Unarmed melee damage multiplier applied while underwater. Only applies to unarmed attacks -
    /// with a weapon in hand the hit event is raised on the weapon rather than on this mob.
    /// </summary>
    [DataField]
    public float UnarmedDamageModifier = 2f;

    /// <summary>
    /// Whether diving took the footstep-sound tag off this mob, so surfacing knows to put it back
    /// (and doesn't hand it to a mob that never had it).
    /// </summary>
    [ViewVariables]
    public bool RemovedFootstepTag;
}
