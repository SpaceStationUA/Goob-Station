using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.ModularSuit;

[Serializable, NetSerializable]
public sealed partial class ModularSuitPartSealDoAfterEvent : SimpleDoAfterEvent
{
    public bool Activate { get; }
    public bool ActivateSuit { get; }

    public ModularSuitPartSealDoAfterEvent(bool activate, bool activateSuit = false)
    {
        Activate = activate;
        ActivateSuit = activateSuit;
    }
}
