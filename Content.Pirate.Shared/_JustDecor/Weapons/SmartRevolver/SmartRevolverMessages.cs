using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver;

/// <summary>
/// Client sends this to server to request setting a target.
/// </summary>
[Serializable, NetSerializable]
public sealed class SmartRevolverSetTargetMessage : EntityEventArgs
{
    public NetEntity Target;

    public SmartRevolverSetTargetMessage(NetEntity target)
    {
        Target = target;
    }
}
