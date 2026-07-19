using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.ModularSuit;

[Serializable, NetSerializable]
public enum ModularSuitPart : byte
{
    Module,
    Core,
    Part
}

[Serializable, NetSerializable]
public enum SuitPartType : byte
{
    Helmet,
    Torso,
    Gloves,
    Boots
}
