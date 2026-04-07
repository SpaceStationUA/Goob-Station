using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Chemistry;

public static class PirateChemRecipeSharedHelper
{
    public static bool TryGetValidatedRecipeName(string? rawName, int maxLength, [NotNullWhen(true)] out string? validatedName)
    {
        validatedName = rawName?.Trim();
        return !string.IsNullOrWhiteSpace(validatedName) &&
               validatedName.Length <= maxLength;
    }

    public static Color GetRecipeColor(Dictionary<string, FixedPoint2> recipe, IPrototypeManager prototypeManager)
    {
        if (recipe.Count == 0)
            return Color.Transparent;

        var runningTotalQuantity = FixedPoint2.Zero;
        var first = true;
        Color mixColor = default;

        foreach (var (reagentId, quantity) in recipe.OrderBy(x => x.Key))
        {
            runningTotalQuantity += quantity;

            if (!prototypeManager.TryIndex(reagentId, out ReagentPrototype? proto))
                continue;

            if (first)
            {
                first = false;
                mixColor = proto.SubstanceColor;
                continue;
            }

            var interpolateValue = quantity.Float() / runningTotalQuantity.Float();
            mixColor = Color.InterpolateBetween(mixColor, proto.SubstanceColor, interpolateValue);
        }

        return mixColor;
    }

    public static List<ReagentQuantity> BuildRecordingRecipeReagents(Dictionary<string, FixedPoint2>? recordingRecipe)
    {
        if (recordingRecipe == null || recordingRecipe.Count == 0)
            return [];

        var list = new List<ReagentQuantity>();
        foreach (var (reagentId, quantity) in recordingRecipe.OrderBy(x => x.Key))
        {
            list.Add(new ReagentQuantity(reagentId, quantity));
        }

        return list;
    }

    public static bool TryAddRecordedReagent(string reagentId, FixedPoint2 amount, Dictionary<string, FixedPoint2> recordingRecipe)
    {
        if (amount <= FixedPoint2.Zero)
            return false;

        if (recordingRecipe.TryGetValue(reagentId, out var existing))
            recordingRecipe[reagentId] = existing + amount;
        else
            recordingRecipe.Add(reagentId, amount);

        return true;
    }
}
