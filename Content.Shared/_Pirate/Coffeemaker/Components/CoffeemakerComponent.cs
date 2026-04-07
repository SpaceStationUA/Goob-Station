using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Shared._Pirate.Coffeemaker.Components;

[RegisterComponent]
public sealed partial class CoffeemakerComponent : Component
{
    [DataField]
    public string PotSlotId = "potSlot";

    [DataField]
    public string? CartridgeSlotId = "cartridgeSlot";

    [DataField]
    public string BeanContainerId = "beanContainer";

    [DataField]
    public bool UsesBeans;

    [DataField]
    public int BeanCapacity = 10;

    [DataField]
    public float BrewTime = 20f;

    [DataField]
    public FixedPoint2 BrewVolume = FixedPoint2.New(120);

    [DataField]
    public EntProtoId CupPrototype = "DrinkCoffeeCupPirate";

    [DataField]
    public EntProtoId SugarPacketPrototype = "FoodCondimentPacketSugarPirate";

    [DataField]
    public EntProtoId SweetenerPacketPrototype = "FoodCondimentPacketAstrotamePirate";

    [DataField]
    public EntProtoId FallbackSugarPacketPrototype = "FoodCondimentPacketSugar";

    [DataField]
    public EntProtoId FallbackSweetenerPacketPrototype = "FoodCondimentPacketAstrotame";

    [DataField]
    public EntProtoId CreamerPacketPrototype = "FoodCondimentPacketCreamerPirate";

    [DataField]
    public int Cups = 15;

    [DataField]
    public int MaxCups = 15;

    [DataField]
    public int SugarPacks = 10;

    [DataField]
    public int MaxSugarPacks = 10;

    [DataField]
    public int SweetenerPacks = 10;

    [DataField]
    public int MaxSweetenerPacks = 10;

    [DataField]
    public int CreamerPacks = 10;

    [DataField]
    public int MaxCreamerPacks = 10;

    [DataField]
    public SoundSpecifier BrewSound = new SoundPathSpecifier("/Audio/_Pirate/Machines/coffeemaker_brew.ogg", AudioParams.Default.WithVariation(0.125f));

    [DataField]
    public EntProtoId SteamEffectPrototype = "EffectCoffeemakerSteamPirate";

    [DataField]
    public EntProtoId SteamPuffPrototype = "EffectCoffeemakerSteamPuffPirate";

    [DataField]
    public Vector2 SteamOffset = new(0f, 0.35f);

    [DataField]
    public float SteamPuffInterval = 0.18f;

    [ViewVariables]
    public bool Brewing;

    [ViewVariables]
    public TimeSpan BrewCompleteAt;

    [ViewVariables]
    public EntityUid? ActiveSteamEffect;

    [ViewVariables]
    public TimeSpan NextSteamPuffAt;
}
