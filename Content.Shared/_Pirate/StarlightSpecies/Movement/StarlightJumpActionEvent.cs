// Pirate: Starlight jumps, renamed to coexist with the native xenomorph jump.
using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared._Pirate.StarlightSpecies.Movement;

public sealed partial class StarlightJumpActionEvent : WorldTargetActionEvent
{
    [DataField]
    public float Distance = 5f;

    [DataField]
    public bool ToPointer = true;

    [DataField]
    public bool FromGrid = true;

    [DataField]
    public float Speed = 15F;

    [DataField]
    public SoundSpecifier? Sound = default;

}
