using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Visuals;

[Serializable, NetSerializable]
public enum PirateClientTextureVisionMode : byte
{
    Xeno = 0,
    Skeleton = 1,
    Food = 2,
}
