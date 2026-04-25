using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Pirate.Coffeemaker;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Coffeemaker.Components;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class CoffeeCartridgeFancyVariant
{
    [DataField(required: true)]
    public CoffeeCartridgeVariant Variant;

    [DataField(required: true)]
    public string Name = string.Empty;
}

[RegisterComponent]
public sealed partial class CoffeeCartridgeComponent : Component
{
    [DataField]
    public int Charges = 4;

    [DataField]
    public string BrewReagent = "Coffee";

    [DataField]
    public FixedPoint2 BrewAmount = FixedPoint2.New(120);

    [DataField]
    public List<ReagentQuantity> ExtraReagents = new();

    [DataField]
    public CoffeeCartridgeVariant Variant = CoffeeCartridgeVariant.Basic;

    [DataField]
    public List<CoffeeCartridgeFancyVariant> FancyVariants = new();

    [DataField]
    public bool Randomized;
}
