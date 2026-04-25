using Content.Shared._Shitmed.Targeting;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Movement.Pulling.Events;

[Serializable, NetSerializable]
public sealed partial class BoneCrushDoAfterEvent(TargetBodyPart targetPart) : SimpleDoAfterEvent
{
    public TargetBodyPart TargetPart = targetPart;

    public BoneCrushDoAfterEvent() : this(TargetBodyPart.LeftLeg)
    {
    }

    public override DoAfterEvent Clone() => this;
}
