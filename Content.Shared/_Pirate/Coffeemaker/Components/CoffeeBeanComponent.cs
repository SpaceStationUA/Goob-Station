using Content.Shared.Chemistry.Reagent;

namespace Content.Shared._Pirate.Coffeemaker.Components;

[RegisterComponent]
public sealed partial class CoffeeBeanComponent : Component
{
    [DataField]
    public string BrewReagent = "Coffee";

    [DataField]
    public List<ReagentQuantity> ExtraReagents = new();
}
