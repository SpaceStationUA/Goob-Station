using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;

namespace Content.Server._Pirate.Chemistry;

public static class PirateChemRecipeServerHelper
{
    public static bool TryGetRecipeDisk<TComponent>(
        Entity<TComponent> dispenser,
        ItemSlotsSystem itemSlotsSystem,
        IEntityManager entityManager,
        string slotName,
        [NotNullWhen(true)] out EntityUid? diskUid,
        [NotNullWhen(true)] out ChemRecipeDiskComponent? diskComp)
        where TComponent : IComponent
    {
        diskUid = itemSlotsSystem.GetItemOrNull(dispenser, slotName);
        if (diskUid is not { Valid: true } || !entityManager.TryGetComponent(diskUid.Value, out diskComp))
        {
            diskUid = null;
            diskComp = null;
            return false;
        }

        return true;
    }

    public static bool TryBuildSavedRecipeCopy(
        Dictionary<string, FixedPoint2>? recordingRecipe,
        [NotNullWhen(true)] out Dictionary<string, FixedPoint2>? savedRecipe)
    {
        if (recordingRecipe == null || recordingRecipe.Count == 0)
        {
            savedRecipe = null;
            return false;
        }

        savedRecipe = new Dictionary<string, FixedPoint2>(recordingRecipe);
        return true;
    }

    public static bool MergeRecipeIntoRecording(
        Dictionary<string, FixedPoint2>? recordingRecipe,
        IReadOnlyDictionary<string, FixedPoint2> recipe)
    {
        if (recordingRecipe == null)
            return false;

        foreach (var (reagentId, quantity) in recipe)
        {
            if (recordingRecipe.TryGetValue(reagentId, out var existing))
                recordingRecipe[reagentId] = existing + quantity;
            else
                recordingRecipe.Add(reagentId, quantity);
        }

        return true;
    }

    public static bool SaveRecipeToDisk(
        IReadOnlyDictionary<string, Dictionary<string, FixedPoint2>> savedRecipes,
        string name,
        ChemRecipeDiskComponent recipeDisk)
    {
        if (!savedRecipes.TryGetValue(name, out var recipe))
            return false;

        recipeDisk.SavedRecipes[name] = new Dictionary<string, FixedPoint2>(recipe);
        return true;
    }

    public static bool CopyRecipeFromDisk(
        Dictionary<string, Dictionary<string, FixedPoint2>> savedRecipes,
        string name,
        ChemRecipeDiskComponent recipeDisk)
    {
        if (!recipeDisk.SavedRecipes.TryGetValue(name, out var recipe))
            return false;

        savedRecipes[name] = new Dictionary<string, FixedPoint2>(recipe);
        return true;
    }
}
