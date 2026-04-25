namespace Content.Shared._Pirate.Coffeemaker.Components;

[RegisterComponent]
public sealed partial class CoffeeCupVisualsComponent : Component
{
    [DataField]
    public string Layer = "coffee_cup_base";

    [DataField]
    public string ClosedState = "coffee";

    [DataField]
    public string OpenedEmptyState = "coffee_empty";

    [DataField]
    public string OpenedFilledState = "coffee_full";

    [DataField]
    public float FilledThreshold = 0.001f;
}
