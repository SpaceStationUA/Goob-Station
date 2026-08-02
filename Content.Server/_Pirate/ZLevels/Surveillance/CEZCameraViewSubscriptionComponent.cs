using Robust.Shared.Player;

namespace Content.Server._Pirate.ZLevels.Surveillance;

[RegisterComponent]
[Access(typeof(CEZCameraViewSubscriptionSystem))]
public sealed partial class CEZCameraViewSubscriptionComponent : Component
{
    public readonly Dictionary<ICommonSession, List<EntityUid>> SessionEyes = new();
}
