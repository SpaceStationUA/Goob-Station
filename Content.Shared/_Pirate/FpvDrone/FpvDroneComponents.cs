using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.FpvDrone;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedFpvDroneSystem))]
[AutoGenerateComponentState]
public sealed partial class FpvDroneComponent : Component
{
    /// <summary>
    ///     Drone flying sound specifier. This must loop.
    /// </summary>
    [DataField, ViewVariables]
    public SoundSpecifier? AudioSpecifier = null;

    [DataField, ViewVariables]
    public float FlybySoundProbability = 0.65f;

    /// <summary>
    ///     UID of the audio entity used for the drone flying sound.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? AudioUid = null;

    [DataField(required: true), ViewVariables]
    public ProtoId<SinkPortPrototype> DropStoragePort = "FpvDroneTrigger";

    /// <summary>
    ///     ID of the container to be emptied upon the necessary signal.
    /// </summary>
    [DataField, ViewVariables]
    public string EmptiedContainerId = "storagebase";

    /// <summary>
    ///     Is the drone currently flying and using power?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = false;
}

/// <summary>
///     Added to a drone controller that is linked to an FPV drone.
///         This is used to store battery state of the FPV drone, without
///         having to override PVS just for the drone to be networked to everyone.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedFpvDroneSystem))]
[AutoGenerateComponentState]
public sealed partial class FpvDroneControllerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool HasSufficientCharge = false;
}

[Serializable, NetSerializable]
public enum FpvDroneVisuals : byte { Active }

[Serializable, NetSerializable]
public sealed class FpvDroneDropPayloadRequest : EntityEventArgs;
