using Content.Shared._Shitmed.Targeting;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Movement.Pulling.Events;

[Serializable, NetSerializable]
public sealed partial class BoneCrushDoAfterEvent : SimpleDoAfterEvent
{
    public TargetBodyPart TargetPart;

    public BoneCrushDoAfterEvent(TargetBodyPart targetPart)
    {
        TargetPart = targetPart;
    }

    public BoneCrushDoAfterEvent() : this(TargetBodyPart.LeftLeg)
    {
    }

    public override DoAfterEvent Clone() => this;
}
