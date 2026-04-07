using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Coffeemaker;

[Serializable, NetSerializable]
public enum CoffeemakerVisuals : byte
{
    PotState,
    HasCartridge,
    CupsState,
    HasSugar,
    HasCreamer,
    HasSweetener,
    GrinderState,
}

[Serializable, NetSerializable]
public enum CoffeemakerPotVisualState : byte
{
    None,
    Empty,
    Full,
}

[Serializable, NetSerializable]
public enum CoffeemakerCupsVisualState : byte
{
    None,
    Low,
    Medium,
    High,
}

[Serializable, NetSerializable]
public enum CoffeemakerGrinderVisualState : byte
{
    None,
    Half,
    Full,
}

[Serializable, NetSerializable]
public enum CoffeeCartridgeVisuals : byte
{
    Variant,
}

[Serializable, NetSerializable]
public enum CoffeeCartridgeVisualLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum CoffeeCartridgeVariant : byte
{
    Basic,
    Blend,
    BlueMountain,
    Kilimanjaro,
    Mocha,
    Decaf,
    Bootleg,
    Blank,
}
