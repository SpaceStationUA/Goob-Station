using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Chemistry.EntitySystems;

namespace Content.Server.Chemistry.Components;

[RegisterComponent]
[Access(typeof(ReagentDispenserSystem))]
public sealed partial class ChemRecipeDiskComponent : Component
{
    [ViewVariables]
    [DataField]
    public Dictionary<string, Dictionary<string, FixedPoint2>> SavedRecipes = new();
}

