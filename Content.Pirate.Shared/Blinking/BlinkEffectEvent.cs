using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Blinking;

/// <summary>
/// Network event for blink emote visuals.
/// </summary>
[Serializable, NetSerializable]
public sealed class BlinkEffectEvent : EntityEventArgs
{
    public NetEntity Target;
    public bool Rapid;

    public BlinkEffectEvent(NetEntity target, bool rapid = false)
    {
        Target = target;
        Rapid = rapid;
    }
}
