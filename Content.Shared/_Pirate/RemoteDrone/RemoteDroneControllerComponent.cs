using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.RemoteDrone;

/// <summary>
///     Component for things that control remote drones (computers, laptops, etc.),
///         not actually the person controlling the drone.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
[Access(typeof(RemoteDroneSystem))]
public sealed partial class RemoteDroneControllerComponent : Component
{
    /// <summary>
    ///     Drone linked to this controller.
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? LinkedDroneUid = null;

    /// <summary>
    ///     Whether the drone is currently being operated.
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Controlling = false;

    /// <summary>
    ///     Port to connect to drone.
    /// </summary>
    [DataField, ViewVariables]
    public ProtoId<SourcePortPrototype> SourcePort = "RemoteDroneSender";

    /// <summary>
    ///     Uid of the entity controlling the drone.
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? UserUid = null;

    /// <summary>
    ///     Session of the player controlling the drone.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public ICommonSession? UserSession = null;
}
