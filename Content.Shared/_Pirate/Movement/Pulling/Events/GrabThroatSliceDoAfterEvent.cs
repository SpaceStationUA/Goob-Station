using Content.Shared._Shitmed.Targeting;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Movement.Pulling.Events;

[Serializable, NetSerializable]
public sealed partial class ThroatSliceDoAfterEvent(TargetBodyPart targetPart) : SimpleDoAfterEvent
{
    public TargetBodyPart TargetPart = targetPart;

    public ThroatSliceDoAfterEvent() : this(TargetBodyPart.Head)
    {
    }

    public override DoAfterEvent Clone() => this;
}
