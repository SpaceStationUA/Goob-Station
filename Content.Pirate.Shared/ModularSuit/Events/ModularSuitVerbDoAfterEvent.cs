using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.ModularSuit;

/// <summary>
///     Which of the suit's equipment verbs a non-wearer is trying to use on it.
/// </summary>
[Serializable, NetSerializable]
public enum ModularSuitVerbType : byte
{
    ToggleDeploy,
    ToggleSeal,
}

[Serializable, NetSerializable]
public sealed partial class ModularSuitVerbDoAfterEvent : SimpleDoAfterEvent
{
    public ModularSuitVerbType Verb { get; }

    public ModularSuitVerbDoAfterEvent(ModularSuitVerbType verb)
    {
        Verb = verb;
    }
}
