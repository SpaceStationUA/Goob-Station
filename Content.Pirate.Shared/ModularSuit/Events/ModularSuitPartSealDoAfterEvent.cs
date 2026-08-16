using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.ModularSuit;

[Serializable, NetSerializable]
public sealed partial class ModularSuitPartSealDoAfterEvent : SimpleDoAfterEvent
{
    public bool Activate { get; }
    public bool ActivateSuit { get; }

    public bool DeactivateSuit { get; }

    public ModularSuitPartSealDoAfterEvent(bool activate, bool activateSuit = false, bool deactivateSuit = false)
    {
        Activate = activate;
        ActivateSuit = activateSuit;
        DeactivateSuit = deactivateSuit;
    }
}
