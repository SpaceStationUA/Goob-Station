using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Chemistry.Components;
using Content.Shared._Pirate.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Server.GameObjects;

namespace Content.Server._Pirate.Chemistry;

public abstract class PirateRecipeDispenserSystemBase<TComp> : EntitySystem
    where TComp : Component, IPirateRecipeDispenserComponent
{
    [Dependency] private readonly AudioSystem _audioSystem = default!;

    protected abstract ItemSlotsSystem ItemSlotsSystem { get; }
    protected abstract string RecipeDiskSlotName { get; }
    protected abstract int RecipeNameMaxLength { get; }
    protected virtual bool AllowRestartRecording => false;
    protected virtual bool AllowCancelWithoutRecording => false;
    protected virtual bool ErrorOnSaveFailure => false;

    protected void RegisterPirateRecipeEvents()
    {
        SubscribeLocalEvent<TComp, ReagentDispenserStartRecipeRecordingMessage>(OnStartRecipeRecordingMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserCancelRecipeRecordingMessage>(OnCancelRecipeRecordingMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserSaveRecipeMessage>(OnSaveRecipeMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserDispenseRecipeMessage>(OnDispenseRecipeMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserDeleteRecipeMessage>(OnDeleteRecipeMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserClearRecipesMessage>(OnClearRecipesMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserSaveRecipeToDiskMessage>(OnSaveRecipeToDiskMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserCopyDiskRecipeMessage>(OnCopyDiskRecipeMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserDispenseDiskRecipeMessage>(OnDispenseDiskRecipeMessage);
        SubscribeLocalEvent<TComp, ReagentDispenserDeleteDiskRecipeMessage>(OnDeleteDiskRecipeMessage);
    }

    protected void EnsureItemSlot(EntityUid uid, string slotId, ItemSlot slot)
    {
        if (!ItemSlotsSystem.TryGetSlot(uid, slotId, out _))
            ItemSlotsSystem.AddItemSlot(uid, slotId, slot);
    }

    protected bool TryGetRecipeDisk(
        Entity<TComp> dispenser,
        [NotNullWhen(true)] out EntityUid? diskUid,
        [NotNullWhen(true)] out ChemRecipeDiskComponent? diskComp)
    {
        return PirateChemRecipeServerHelper.TryGetRecipeDisk(
            dispenser,
            ItemSlotsSystem,
            EntityManager,
            RecipeDiskSlotName,
            out diskUid,
            out diskComp);
    }

    protected abstract bool TryDispenseRecipe(Entity<TComp> dispenser, Dictionary<string, FixedPoint2> recipe);
    protected abstract void UpdateRecipeUiState(Entity<TComp> dispenser);

    protected virtual void OnRecipeStateUpdated(Entity<TComp> dispenser)
    {
        UpdateRecipeUiState(dispenser);
        PlayClickSound(dispenser);
    }

    protected virtual void OnRecipeError(Entity<TComp> dispenser)
    {
        PlayErrorSound(dispenser);
    }

    protected void PlayClickSound(Entity<TComp> dispenser)
    {
        _audioSystem.PlayPvs(dispenser.Comp.ClickSound, dispenser, AudioParams.Default.WithVolume(-2f));
    }

    protected void PlayErrorSound(Entity<TComp> dispenser)
    {
        _audioSystem.PlayPvs(dispenser.Comp.ErrorSound, dispenser, AudioParams.Default.WithVolume(-2f));
    }

    protected virtual bool ValidateRecipeForSave(Entity<TComp> dispenser, Dictionary<string, FixedPoint2> recipe) => true;

    private void OnStartRecipeRecordingMessage(Entity<TComp> dispenser, ref ReagentDispenserStartRecipeRecordingMessage message)
    {
        if (!AllowRestartRecording && dispenser.Comp.RecordingRecipe != null)
            return;

        dispenser.Comp.RecordingRecipe = new Dictionary<string, FixedPoint2>();
        OnRecipeStateUpdated(dispenser);
    }

    private void OnCancelRecipeRecordingMessage(Entity<TComp> dispenser, ref ReagentDispenserCancelRecipeRecordingMessage message)
    {
        if (!AllowCancelWithoutRecording && dispenser.Comp.RecordingRecipe == null)
            return;

        dispenser.Comp.RecordingRecipe = null;
        OnRecipeStateUpdated(dispenser);
    }

    private void OnSaveRecipeMessage(Entity<TComp> dispenser, ref ReagentDispenserSaveRecipeMessage message)
    {
        if (!PirateChemRecipeSharedHelper.TryGetValidatedRecipeName(message.Name, RecipeNameMaxLength, out var name))
        {
            if (ErrorOnSaveFailure)
                OnRecipeError(dispenser);
            return;
        }

        if (!PirateChemRecipeServerHelper.TryBuildSavedRecipeCopy(dispenser.Comp.RecordingRecipe, out var savedRecipe))
        {
            if (ErrorOnSaveFailure)
                OnRecipeError(dispenser);
            return;
        }

        if (!ValidateRecipeForSave(dispenser, savedRecipe))
        {
            if (ErrorOnSaveFailure)
                OnRecipeError(dispenser);
            return;
        }

        dispenser.Comp.SavedRecipes[name] = savedRecipe;
        dispenser.Comp.RecordingRecipe = null;
        OnRecipeStateUpdated(dispenser);
    }

    private void OnDispenseRecipeMessage(Entity<TComp> dispenser, ref ReagentDispenserDispenseRecipeMessage message)
    {
        if (!PirateChemRecipeSharedHelper.TryGetValidatedRecipeName(message.Name, RecipeNameMaxLength, out var name))
            return;

        if (!dispenser.Comp.SavedRecipes.TryGetValue(name, out var recipe))
        {
            OnRecipeError(dispenser);
            return;
        }

        if (!TryDispenseRecipe(dispenser, recipe))
        {
            OnRecipeError(dispenser);
            return;
        }

        OnRecipeStateUpdated(dispenser);
    }

    private void OnDispenseDiskRecipeMessage(Entity<TComp> dispenser, ref ReagentDispenserDispenseDiskRecipeMessage message)
    {
        if (!PirateChemRecipeSharedHelper.TryGetValidatedRecipeName(message.Name, RecipeNameMaxLength, out var name))
            return;

        if (!TryGetRecipeDisk(dispenser, out _, out var recipeDisk))
        {
            OnRecipeError(dispenser);
            return;
        }

        if (!recipeDisk.SavedRecipes.TryGetValue(name, out var recipe))
        {
            OnRecipeError(dispenser);
            return;
        }

        if (!TryDispenseRecipe(dispenser, recipe))
        {
            OnRecipeError(dispenser);
            return;
        }

        OnRecipeStateUpdated(dispenser);
    }

    private void OnDeleteRecipeMessage(Entity<TComp> dispenser, ref ReagentDispenserDeleteRecipeMessage message)
    {
        if (!PirateChemRecipeSharedHelper.TryGetValidatedRecipeName(message.Name, RecipeNameMaxLength, out var name))
            return;

        if (dispenser.Comp.SavedRecipes.Remove(name))
            OnRecipeStateUpdated(dispenser);
    }

    private void OnClearRecipesMessage(Entity<TComp> dispenser, ref ReagentDispenserClearRecipesMessage message)
    {
        if (dispenser.Comp.SavedRecipes.Count == 0)
            return;

        dispenser.Comp.SavedRecipes.Clear();
        OnRecipeStateUpdated(dispenser);
    }

    private void OnSaveRecipeToDiskMessage(Entity<TComp> dispenser, ref ReagentDispenserSaveRecipeToDiskMessage message)
    {
        if (!PirateChemRecipeSharedHelper.TryGetValidatedRecipeName(message.Name, RecipeNameMaxLength, out var name))
            return;

        if (!TryGetRecipeDisk(dispenser, out _, out var recipeDisk))
            return;

        if (!PirateChemRecipeServerHelper.SaveRecipeToDisk(dispenser.Comp.SavedRecipes, name, recipeDisk))
            return;

        OnRecipeStateUpdated(dispenser);
    }

    private void OnCopyDiskRecipeMessage(Entity<TComp> dispenser, ref ReagentDispenserCopyDiskRecipeMessage message)
    {
        if (!PirateChemRecipeSharedHelper.TryGetValidatedRecipeName(message.Name, RecipeNameMaxLength, out var name))
            return;

        if (!TryGetRecipeDisk(dispenser, out _, out var recipeDisk))
            return;

        if (!PirateChemRecipeServerHelper.CopyRecipeFromDisk(dispenser.Comp.SavedRecipes, name, recipeDisk))
            return;

        OnRecipeStateUpdated(dispenser);
    }

    private void OnDeleteDiskRecipeMessage(Entity<TComp> dispenser, ref ReagentDispenserDeleteDiskRecipeMessage message)
    {
        if (!PirateChemRecipeSharedHelper.TryGetValidatedRecipeName(message.Name, RecipeNameMaxLength, out var name))
            return;

        if (!TryGetRecipeDisk(dispenser, out _, out var recipeDisk))
            return;

        if (!recipeDisk.SavedRecipes.Remove(name))
            return;

        OnRecipeStateUpdated(dispenser);
    }
}
