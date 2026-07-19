using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Stunnable;

public sealed partial class KnockedDownComponent
{
    /// <summary>
    /// Earliest game time at which a failed automatic stand attempt may retry.
    /// Kept separate from <see cref="NextUpdate"/> so knockdown duration is unaffected.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextStandAttempt;
}
