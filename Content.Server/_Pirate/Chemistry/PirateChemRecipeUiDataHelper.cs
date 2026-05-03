using System.Linq;
using Content.Shared._Pirate.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Chemistry;

public readonly record struct PirateRecipeUiData(
    List<ReagentDispenserRecipeItem> SavedRecipes,
    bool HasRecipeDisk,
    List<ReagentDispenserRecipeItem> DiskRecipes,
    bool IsRecordingRecipe,
    List<ReagentQuantity> RecordingReagents);

public static class PirateChemRecipeUiDataHelper
{
    public static PirateRecipeUiData BuildRecipeUiData<TComp>(
        Entity<TComp> dispenser,
        string recipeDiskSlotName,
        IPrototypeManager prototypeManager,
        ItemSlotsSystem itemSlotsSystem,
        IEntityManager entityManager)
        where TComp : IComponent, IPirateRecipeDispenserComponent
    {
        var savedRecipes = dispenser.Comp.SavedRecipes
            .OrderBy(x => x.Key)
            .Select(x => new ReagentDispenserRecipeItem(x.Key, PirateChemRecipeSharedHelper.GetRecipeColor(x.Value, prototypeManager)))
            .ToList();

        var hasRecipeDisk = PirateChemRecipeServerHelper.TryGetRecipeDisk(
            dispenser,
            itemSlotsSystem,
            entityManager,
            recipeDiskSlotName,
            out _,
            out var recipeDisk);

        var diskRecipes = recipeDisk?.SavedRecipes
            .OrderBy(x => x.Key)
            .Select(x => new ReagentDispenserRecipeItem(x.Key, PirateChemRecipeSharedHelper.GetRecipeColor(x.Value, prototypeManager)))
            .ToList() ?? [];

        var recordingReagents = PirateChemRecipeSharedHelper.BuildRecordingRecipeReagents(dispenser.Comp.RecordingRecipe);

        return new PirateRecipeUiData(
            savedRecipes,
            hasRecipeDisk,
            diskRecipes,
            dispenser.Comp.RecordingRecipe != null,
            recordingReagents);
    }
}
