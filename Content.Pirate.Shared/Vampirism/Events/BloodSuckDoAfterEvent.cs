using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Pirate.Shared.Vampirism.Events
{
    [Serializable, NetSerializable]
    public sealed partial class BloodSuckDoAfterEvent : SimpleDoAfterEvent
    {
    }
}
