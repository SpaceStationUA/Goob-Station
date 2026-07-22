using Content.Shared._Pirate.Grappling.EntitySystems;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Grappling.Components;

/// <summary>
/// Marks this entity as having been grappled.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGrapplingSystem))]
public sealed partial class GrappledComponent : Component
{
    /// <summary>
    /// The entity which is performing the grapple.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Grappler = EntityUid.Invalid;

    /// <summary>
    /// The alert shown for this grapple. Stored on the victim so cleanup does not depend on the grappler surviving.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> GrappledAlert = "Grappled";

    /// <summary>
    /// How much time is required to escape.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EscapeTime = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The in-progress DoAfter, if any.
    /// Used to cancel the doAfter if the grappler manually releases their victim.
    /// </summary>
    [DataField]
    public DoAfterId? DoAfterId = null;

    /// <summary>
    /// A list of all hands, if any, that have been disabled as part of the grapple
    /// via a virtual item.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> DisabledHands = new();

    /// <summary>
    /// Whether the grapple has fully taken effect and the grappled entity should be prone'd.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool GrappleActivated = false;

    /// <summary>
    /// How much this entity should be slowed, before the grapple fully activates.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? MovementSpeedModifier = null;
}

/// <summary>
/// Raised when a player manually clicks the grappled icon to begin attempting to escape.
/// </summary>
public sealed partial class EscapeGrappleAlertEvent : BaseAlertEvent;
