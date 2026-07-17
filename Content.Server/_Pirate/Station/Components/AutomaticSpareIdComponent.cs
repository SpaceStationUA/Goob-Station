using Content.Server._Pirate.Station.Systems;
using Content.Shared.Access;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.Station.Components;

public enum AutomaticSpareIdState
{
    RoundStart,
    Alerted,
    AwaitingUnlock,
    Unlocked,
    CaptainPresent,
    WarOps
}

[RegisterComponent, Access(typeof(AutomaticSpareIdSystem)), AutoGenerateComponentPause]
public sealed partial class AutomaticSpareIdComponent : Component
{
    /// <summary>
    /// The current state of the automatic spare ID system.
    /// </summary>
    [DataField]
    public AutomaticSpareIdState State = AutomaticSpareIdState.RoundStart;

    /// <summary>
    /// Timeout before an action is taken if the state does not change.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? Timeout;

    /// <summary>
    /// The job considered to be the captain by the automatic spare ID system.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype> CaptainJob = "Captain";

    /// <summary>
    /// Access granted to the spare ID safe when it is automatically unlocked without a captain.
    /// </summary>
    [DataField]
    public ProtoId<AccessLevelPrototype> GrantAccessToCommand = "Command";

    /// <summary>
    /// Access granted to the spare ID safe during war operations when a captain is present.
    /// </summary>
    [DataField]
    public ProtoId<AccessLevelPrototype> GrantAccessToCaptain = "Captain";

    [DataField]
    public LocId CaptainPresentAfterAlertsMessage = "captain-arrived-revoke-aco-announcement";

    [DataField]
    public LocId AlertedMessage = "no-captain-request-aco-vote-announcement";

    [DataField]
    public LocId AwaitingUnlockMessage = "no-captain-request-aco-vote-with-aa-announcement";

    [DataField]
    public LocId UnlockedMessage = "no-captain-aa-unlocked-announcement";

    /// <summary>
    /// How long after nuclear operatives declare war the spare ID safe is unlocked.
    /// </summary>
    public TimeSpan WarOpsUnlockDelay = TimeSpan.FromSeconds(15);

    public LocId WarOpsUnlockedMessageACO = "spare-id-warops-no-captain";

    public LocId WarOpsUnlockedMessageCaptain = "spare-id-warops-captain";
}
