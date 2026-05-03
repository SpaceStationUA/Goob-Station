using Content.Goobstation.Maths.FixedPoint;

namespace Content.Server.Chemistry.Components;

[RegisterComponent]
public sealed partial class ChemRecipeDiskComponent : Component
{
    [ViewVariables]
    [DataField]
    public Dictionary<string, Dictionary<string, FixedPoint2>> SavedRecipes = new();
}

