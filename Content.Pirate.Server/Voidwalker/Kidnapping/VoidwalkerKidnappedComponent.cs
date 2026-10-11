using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Server.Voidwalker.Kidnapping;

/// <summary>
/// do not apply this manually
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VoidwalkerKidnappedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExitVoidTime = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid OriginalMap;

    /// <summary>
    /// Return here when the origin has no station or no suitable station tile remains.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityCoordinates OriginalCoordinates;
}
