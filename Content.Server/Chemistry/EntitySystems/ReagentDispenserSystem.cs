// SPDX-FileCopyrightText: 2021 20kdc <asdd2808@gmail.com>
// SPDX-FileCopyrightText: 2021 Clyybber <darkmine956@gmail.com>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <gradientvera@outlook.com>
// SPDX-FileCopyrightText: 2021 Ygg01 <y.laughing.man.y@gmail.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <metalgearsloth@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 ElectroJr <leonsfriedrich@gmail.com>
// SPDX-FileCopyrightText: 2023 Emisse <99158783+Emisse@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Pieter-Jan Briers <pieterjan.briers@gmail.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2023 deltanedas <deltanedas@laptop>
// SPDX-FileCopyrightText: 2023 deltanedas <user@zenith>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 AWF <you@example.com>
// SPDX-FileCopyrightText: 2024 Brandon Li <48413902+aspiringLich@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Cojoke <83733158+Cojoke-dot@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 GitHubUser53123 <110841413+GitHubUser53123@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Jake Huxell <JakeHuxell@pm.me>
// SPDX-FileCopyrightText: 2024 Kevin Zheng <kevinz5000@gmail.com>
// SPDX-FileCopyrightText: 2024 Kira Bridgeton <161087999+Verbalase@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Diagnostics.CodeAnalysis; // Pirate: chem recipes
using Content.Shared.Chemistry.Reagent; // Pirate: chem recipes
using Content.Server.Chemistry.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage.EntitySystems;
using JetBrains.Annotations;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Labels.Components;
using Content.Shared.Storage;
using Content.Server.Hands.Systems;

namespace Content.Server.Chemistry.EntitySystems
{
    /// <summary>
    /// Contains all the server-side logic for reagent dispensers.
    /// <seealso cref="ReagentDispenserComponent"/>
    /// </summary>
    [UsedImplicitly]
    public sealed class ReagentDispenserSystem : EntitySystem
    {
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly SolutionTransferSystem _solutionTransferSystem = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly OpenableSystem _openable = default!;
        [Dependency] private readonly HandsSystem _handsSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ReagentDispenserComponent, ComponentStartup>(SubscribeUpdateUiState);
            SubscribeLocalEvent<ReagentDispenserComponent, SolutionContainerChangedEvent>(SubscribeUpdateUiState);
            SubscribeLocalEvent<ReagentDispenserComponent, EntInsertedIntoContainerMessage>(SubscribeUpdateUiState, after: [typeof(SharedStorageSystem)]);
            SubscribeLocalEvent<ReagentDispenserComponent, EntRemovedFromContainerMessage>(SubscribeUpdateUiState, after: [typeof(SharedStorageSystem)]);
            SubscribeLocalEvent<ReagentDispenserComponent, BoundUIOpenedEvent>(SubscribeUpdateUiState);

            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserSetDispenseAmountMessage>(OnSetDispenseAmountMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserDispenseReagentMessage>(OnDispenseReagentMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserEjectContainerMessage>(OnEjectReagentMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserClearContainerSolutionMessage>(OnClearContainerSolutionMessage);
            RegisterPirateRecipeEvents(); // Pirate: chem recipes

            SubscribeLocalEvent<ReagentDispenserComponent, MapInitEvent>(OnMapInit, before: new[] { typeof(ItemSlotsSystem) });
        }

        private void SubscribeUpdateUiState<T>(Entity<ReagentDispenserComponent> ent, ref T ev)
        {
            UpdateUiState(ent);
            #region Pirate: chem recipes
            if (ev is EntRemovedFromContainerMessage removed &&
                removed.Container.ID == SharedReagentDispenser.RecipeDiskSlotName)
                ClickSound(ent);
            #endregion
        }

        private void UpdateUiState(Entity<ReagentDispenserComponent> reagentDispenser)
        {
            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
            var outputContainerInfo = BuildOutputContainerInfo(outputContainer);

            var inventory = GetInventory(reagentDispenser);
            #region Pirate: chem recipes
            var savedRecipes = reagentDispenser.Comp.SavedRecipes
                .OrderBy(x => x.Key)
                .Select(x => new ReagentDispenserRecipeItem(x.Key, GetRecipeColor(x.Value)))
                .ToList();
            var hasRecipeDisk = TryGetRecipeDisk(reagentDispenser, out _, out var recipeDisk);
            var diskRecipes = recipeDisk?.SavedRecipes
                .OrderBy(x => x.Key)
                .Select(x => new ReagentDispenserRecipeItem(x.Key, GetRecipeColor(x.Value)))
                .ToList() ?? [];
            var recordingReagents = BuildRecordingRecipeReagents(reagentDispenser.Comp.RecordingRecipe);

            var state = new ReagentDispenserBoundUserInterfaceState(
                outputContainerInfo,
                GetNetEntity(outputContainer),
                inventory,
                reagentDispenser.Comp.DispenseAmount,
                savedRecipes,
                hasRecipeDisk,
                diskRecipes,
                reagentDispenser.Comp.RecordingRecipe != null,
                recordingReagents);
            #endregion
            _userInterfaceSystem.SetUiState(reagentDispenser.Owner, ReagentDispenserUiKey.Key, state);
        }

        private ContainerInfo? BuildOutputContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            if (_solutionContainerSystem.TryGetFitsInDispenser(container.Value, out _, out var solution))
            {
                return new ContainerInfo(Name(container.Value), solution.Volume, solution.MaxVolume)
                {
                    Reagents = solution.Contents
                };
            }

            return null;
        }

        private List<ReagentInventoryItem> GetInventory(Entity<ReagentDispenserComponent> reagentDispenser)
        {
            if (!TryComp<StorageComponent>(reagentDispenser.Owner, out var storage))
            {
                return [];
            }

            var inventory = new List<ReagentInventoryItem>();

            foreach (var (storedContainer, storageLocation) in storage.StoredItems)
            {
                string reagentLabel;
                if (TryComp<LabelComponent>(storedContainer, out var label) && !string.IsNullOrEmpty(label.CurrentLabel))
                    reagentLabel = label.CurrentLabel;
                else
                    reagentLabel = Name(storedContainer);

                // Get volume remaining and color of solution
                FixedPoint2 quantity = 0f;
                var reagentColor = Color.White;
                if (_solutionContainerSystem.TryGetDrainableSolution(storedContainer, out _, out var sol))
                {
                    quantity = sol.Volume;
                    reagentColor = sol.GetColor(_prototypeManager);
                }

                inventory.Add(new ReagentInventoryItem(storageLocation, reagentLabel, quantity, reagentColor));
            }

            return inventory;
        }

        private void OnSetDispenseAmountMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserSetDispenseAmountMessage message)
        {
            reagentDispenser.Comp.DispenseAmount = message.ReagentDispenserDispenseAmount;
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnDispenseReagentMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserDispenseReagentMessage message)
        {
            if (!TryComp<StorageComponent>(reagentDispenser.Owner, out var storage))
            {
                return;
            }

            // Ensure that the reagent is something this reagent dispenser can dispense.
            var storageLocation = message.StorageLocation;
            var storedContainer = storage.StoredItems.FirstOrDefault(kvp => kvp.Value == storageLocation).Key;
            if (storedContainer == EntityUid.Invalid)
                return;

            #region Pirate: chem recipes
            if (reagentDispenser.Comp.RecordingRecipe != null)
            {
                var amount = FixedPoint2.New((int)reagentDispenser.Comp.DispenseAmount);
                if (TryRecordDispensedMixture(storedContainer, amount, reagentDispenser.Comp.RecordingRecipe))
                {
                    UpdateUiState(reagentDispenser);
                    ClickSound(reagentDispenser);
                }

                return;
            }
            #endregion

            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
            if (outputContainer is not { Valid: true } || !_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution, out _))
                return;

            if (_solutionContainerSystem.TryGetDrainableSolution(storedContainer, out var src, out _) &&
                _solutionContainerSystem.TryGetRefillableSolution(outputContainer.Value, out var dst, out _))
            {
                // force open container, if applicable, to avoid confusing people on why it doesn't dispense
                _openable.SetOpen(storedContainer, true);
                _solutionTransferSystem.Transfer(reagentDispenser,
                        storedContainer, src.Value,
                        outputContainer.Value, dst.Value,
                        (int)reagentDispenser.Comp.DispenseAmount);
            }

            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnEjectReagentMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserEjectContainerMessage message)
        {
            if (!TryComp<StorageComponent>(reagentDispenser.Owner, out var storage))
            {
                return;
            }

            var storageLocation = message.StorageLocation;
            var storedContainer = storage.StoredItems.FirstOrDefault(kvp => kvp.Value == storageLocation).Key;
            if (storedContainer == EntityUid.Invalid)
                return;

            _handsSystem.TryPickupAnyHand(message.Actor, storedContainer);
        }

        private void OnClearContainerSolutionMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserClearContainerSolutionMessage message)
        {
            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
            if (outputContainer is not { Valid: true } || !_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution, out _))
                return;

            _solutionContainerSystem.RemoveAllSolution(solution.Value);
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void ClickSound(Entity<ReagentDispenserComponent> reagentDispenser)
        {
            _audioSystem.PlayPvs(reagentDispenser.Comp.ClickSound, reagentDispenser, AudioParams.Default.WithVolume(-2f));
        }

        /// <summary>
        /// Initializes the beaker slot
        /// </summary>
        private void OnMapInit(Entity<ReagentDispenserComponent> ent, ref MapInitEvent args)
        {
            _itemSlotsSystem.AddItemSlot(ent.Owner, SharedReagentDispenser.OutputSlotName, ent.Comp.BeakerSlot);
            _itemSlotsSystem.AddItemSlot(ent.Owner, SharedReagentDispenser.RecipeDiskSlotName, ent.Comp.RecipeDiskSlot); // Pirate: chem recipes
        }

        #region Pirate: chem recipes
        private void RegisterPirateRecipeEvents()
        {
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserStartRecipeRecordingMessage>(OnStartRecipeRecordingMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserCancelRecipeRecordingMessage>(OnCancelRecipeRecordingMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserSaveRecipeMessage>(OnSaveRecipeMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserDispenseRecipeMessage>(OnDispenseRecipeMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserDeleteRecipeMessage>(OnDeleteRecipeMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserClearRecipesMessage>(OnClearRecipesMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserSaveRecipeToDiskMessage>(OnSaveRecipeToDiskMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserCopyDiskRecipeMessage>(OnCopyDiskRecipeMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserDispenseDiskRecipeMessage>(OnDispenseDiskRecipeMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserDeleteDiskRecipeMessage>(OnDeleteDiskRecipeMessage);
        }
        private Color GetRecipeColor(Dictionary<string, FixedPoint2> recipe)
        {
            if (recipe.Count == 0)
                return Color.Transparent;

            var runningTotalQuantity = FixedPoint2.Zero;
            var first = true;
            Color mixColor = default;

            foreach (var (reagentId, quantity) in recipe.OrderBy(x => x.Key))
            {
                runningTotalQuantity += quantity;

                if (!_prototypeManager.TryIndex(reagentId, out ReagentPrototype? proto))
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

        private void OnStartRecipeRecordingMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserStartRecipeRecordingMessage message)
        {
            reagentDispenser.Comp.RecordingRecipe = new Dictionary<string, FixedPoint2>();
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnCancelRecipeRecordingMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserCancelRecipeRecordingMessage message)
        {
            reagentDispenser.Comp.RecordingRecipe = null;
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnSaveRecipeMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserSaveRecipeMessage message)
        {
            if (!TryGetValidatedRecipeName(message.Name, out var name) ||
                reagentDispenser.Comp.RecordingRecipe == null ||
                reagentDispenser.Comp.RecordingRecipe.Count == 0)
            {
                ErrorSound(reagentDispenser); // Pirate: chem recipes
                return;
            }

            // Validate that each reagent is currently dispensable by this machine.
            foreach (var reagent in reagentDispenser.Comp.RecordingRecipe.Keys)
            {
                if (!TryGetStoredContainerForReagentId(reagentDispenser.Owner, reagent, out _))
                {
                    ErrorSound(reagentDispenser); // Pirate: chem recipes
                    return;
                }
            }

            reagentDispenser.Comp.SavedRecipes[name] = new Dictionary<string, FixedPoint2>(reagentDispenser.Comp.RecordingRecipe);
            reagentDispenser.Comp.RecordingRecipe = null;
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnDispenseRecipeMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserDispenseRecipeMessage message)
        {
            if (!TryGetValidatedRecipeName(message.Name, out var name))
                return;

            if (!reagentDispenser.Comp.SavedRecipes.TryGetValue(name, out var recipe))
            {
                ErrorSound(reagentDispenser);
                return;
            }

            if (!TryDispenseRecipe(reagentDispenser, recipe, out var reason, out var failedReagentId))
            {
                ErrorSound(reagentDispenser);
                return;
            }

            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnDispenseDiskRecipeMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserDispenseDiskRecipeMessage message)
        {
            if (!TryGetValidatedRecipeName(message.Name, out var name))
                return;

            if (!TryGetRecipeDisk(reagentDispenser, out _, out var recipeDisk))
            {
                ErrorSound(reagentDispenser);
                return;
            }

            if (!recipeDisk.SavedRecipes.TryGetValue(name, out var recipe))
            {
                ErrorSound(reagentDispenser);
                return;
            }

            if (!TryDispenseRecipe(reagentDispenser, recipe, out var reason, out var failedReagentId))
            {
                ErrorSound(reagentDispenser);
                return;
            }

            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private bool TryDispenseRecipe(
            Entity<ReagentDispenserComponent> reagentDispenser,
            Dictionary<string, FixedPoint2> recipe,
            out RecipeDispenseFailureReason reason,
            out string? failedReagentId)
        {
            reason = RecipeDispenseFailureReason.None;
            failedReagentId = null;

            if (reagentDispenser.Comp.RecordingRecipe != null)
            {
                foreach (var (reagentId, quantity) in recipe)
                {
                    if (reagentDispenser.Comp.RecordingRecipe.TryGetValue(reagentId, out var existing))
                        reagentDispenser.Comp.RecordingRecipe[reagentId] = existing + quantity;
                    else
                        reagentDispenser.Comp.RecordingRecipe.Add(reagentId, quantity);
                }

                return true;
            }

            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser, SharedReagentDispenser.OutputSlotName);
            if (outputContainer is not { Valid: true } || !_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out _, out _))
            {
                reason = RecipeDispenseFailureReason.MissingOutputContainer;
                return false;
            }

            if (!_solutionContainerSystem.TryGetRefillableSolution(outputContainer.Value, out var dstRefillable, out var dstSolution))
            {
                reason = RecipeDispenseFailureReason.MissingOutputContainer;
                return false;
            }

            var totalRequiredQuantity = recipe.Values.Aggregate(FixedPoint2.Zero, (current, quantity) => current + quantity);
            if (totalRequiredQuantity > dstSolution.AvailableVolume)
            {
                reason = RecipeDispenseFailureReason.TransferFailed;
                return false;
            }

            var cachedSources = new Dictionary<string, (EntityUid Container, Entity<Content.Shared.Chemistry.Components.SolutionComponent>? Drainable)>(recipe.Count);

            // Pre-check recipe contents so recipe usage either succeeds as a whole or aborts with a precise error.
            foreach (var (reagentId, quantity) in recipe)
            {
                if (!TryGetStoredContainerForReagentId(reagentDispenser.Owner, reagentId, out var srcContainer))
                {
                    reason = RecipeDispenseFailureReason.ReagentNotFound;
                    failedReagentId = reagentId;
                    return false;
                }

                if (!_solutionContainerSystem.TryGetDrainableSolution(srcContainer, out var srcDrainable, out var srcSoln))
                {
                    reason = RecipeDispenseFailureReason.ReagentNotFound;
                    failedReagentId = reagentId;
                    return false;
                }

                var available = srcSoln.GetReagentQuantity(new ReagentId(reagentId, null));
                if (available < quantity)
                {
                    reason = RecipeDispenseFailureReason.NotEnoughReagent;
                    failedReagentId = reagentId;
                    return false;
                }

                // Pirate: chem recipes - recipe quantities are per reagent, so mixed sources are invalid.
                if (srcSoln.Volume - available > FixedPoint2.Zero)
                {
                    reason = RecipeDispenseFailureReason.TransferFailed;
                    failedReagentId = reagentId;
                    return false;
                }

                cachedSources[reagentId] = (srcContainer, srcDrainable);
            }

            foreach (var (reagentId, quantity) in recipe)
            {
                if (!cachedSources.TryGetValue(reagentId, out var source))
                {
                    reason = RecipeDispenseFailureReason.ReagentNotFound;
                    failedReagentId = reagentId;
                    return false;
                }

                if (source.Drainable is not { } srcDrainable)
                {
                    reason = RecipeDispenseFailureReason.ReagentNotFound;
                    failedReagentId = reagentId;
                    return false;
                }

                _openable.SetOpen(source.Container, true);
                var transferred = _solutionTransferSystem.Transfer(
                    reagentDispenser,
                    source.Container,
                    srcDrainable,
                    outputContainer.Value,
                    dstRefillable.Value,
                    quantity);

                if (transferred < quantity)
                {
                    reason = RecipeDispenseFailureReason.TransferFailed;
                    failedReagentId = reagentId;
                    return false;
                }
            }
            return true;
        }

        private void OnDeleteRecipeMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserDeleteRecipeMessage message)
        {
            if (!TryGetValidatedRecipeName(message.Name, out var name))
                return;

            if (reagentDispenser.Comp.SavedRecipes.Remove(name))
            {
                UpdateUiState(reagentDispenser);
                ClickSound(reagentDispenser);
            }
        }

        private void OnClearRecipesMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserClearRecipesMessage message)
        {
            if (reagentDispenser.Comp.SavedRecipes.Count == 0)
                return;

            reagentDispenser.Comp.SavedRecipes.Clear();
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnSaveRecipeToDiskMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserSaveRecipeToDiskMessage message)
        {
            if (!TryGetValidatedRecipeName(message.Name, out var name))
                return;

            if (!TryGetRecipeDisk(reagentDispenser, out _, out var recipeDisk))
                return;

            if (!reagentDispenser.Comp.SavedRecipes.TryGetValue(name, out var recipe))
                return;

            recipeDisk.SavedRecipes[name] = new Dictionary<string, FixedPoint2>(recipe);
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnCopyDiskRecipeMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserCopyDiskRecipeMessage message)
        {
            if (!TryGetValidatedRecipeName(message.Name, out var name))
                return;

            if (!TryGetRecipeDisk(reagentDispenser, out _, out var recipeDisk))
                return;

            if (!recipeDisk.SavedRecipes.TryGetValue(name, out var recipe))
                return;

            reagentDispenser.Comp.SavedRecipes[name] = new Dictionary<string, FixedPoint2>(recipe);
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnDeleteDiskRecipeMessage(Entity<ReagentDispenserComponent> reagentDispenser, ref ReagentDispenserDeleteDiskRecipeMessage message)
        {
            if (!TryGetValidatedRecipeName(message.Name, out var name))
                return;

            if (!TryGetRecipeDisk(reagentDispenser, out _, out var recipeDisk))
                return;

            if (!recipeDisk.SavedRecipes.Remove(name))
                return;

            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private bool TryGetValidatedRecipeName(string? rawName, [NotNullWhen(true)] out string? validatedName)
        {
            validatedName = rawName?.Trim();
            return !string.IsNullOrWhiteSpace(validatedName) &&
                   validatedName.Length <= SharedReagentDispenser.RecipeNameMaxLength;
        }


        private bool TryRecordDispensedMixture(EntityUid container, FixedPoint2 requestedAmount, Dictionary<string, FixedPoint2> recordingRecipe)
        {
            if (!_solutionContainerSystem.TryGetDrainableSolution(container, out _, out var sol) ||
                sol.Volume <= FixedPoint2.Zero)
            {
                return false;
            }

            var transferredAmount = FixedPoint2.Min(requestedAmount, sol.Volume);
            if (transferredAmount <= FixedPoint2.Zero)
                return false;

            // Pirate: chem recipes - preserve exact dispense amount for pure sources (avoid 10 -> 9 rounding).
            if (sol.Contents.Count == 1 && sol.Contents[0].Reagent.Prototype is { } onlyReagentId)
            {
                if (recordingRecipe.TryGetValue(onlyReagentId, out var existing))
                    recordingRecipe[onlyReagentId] = existing + transferredAmount;
                else
                    recordingRecipe.Add(onlyReagentId, transferredAmount);

                return true;
            }

            var transferRatio = transferredAmount / sol.Volume;
            var recordedAny = false;

            foreach (var reagent in sol.Contents)
            {
                if (reagent.Reagent.Prototype is not { } reagentId)
                    continue;

                var recordedAmount = reagent.Quantity * transferRatio;
                if (recordedAmount <= FixedPoint2.Zero)
                    continue;

                if (recordingRecipe.TryGetValue(reagentId, out var existing))
                    recordingRecipe[reagentId] = existing + recordedAmount;
                else
                    recordingRecipe.Add(reagentId, recordedAmount);

                recordedAny = true;
            }

            return recordedAny;
        }


        private bool TryGetStoredContainerForReagentId(EntityUid dispenser, string reagentId, [NotNullWhen(true)] out EntityUid container)
        {
            container = EntityUid.Invalid;
            if (!TryComp<StorageComponent>(dispenser, out var storage))
                return false;

            foreach (var (stored, _) in storage.StoredItems)
            {
                if (!_solutionContainerSystem.TryGetDrainableSolution(stored, out _, out var sol))
                    continue;

                if (sol.GetReagentQuantity(new ReagentId(reagentId, null)) <= FixedPoint2.Zero)
                    continue;

                container = stored;
                return true;
            }

            return false;
        }

        private List<ReagentQuantity> BuildRecordingRecipeReagents(Dictionary<string, FixedPoint2>? recordingRecipe)
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

        private bool TryGetRecipeDisk(Entity<ReagentDispenserComponent> dispenser, [NotNullWhen(true)] out EntityUid? diskUid, [NotNullWhen(true)] out ChemRecipeDiskComponent? diskComp)
        {
            diskUid = _itemSlotsSystem.GetItemOrNull(dispenser, SharedReagentDispenser.RecipeDiskSlotName);
            if (diskUid is not { Valid: true } || !TryComp(diskUid.Value, out diskComp))
            {
                diskUid = null;
                diskComp = null;
                return false;
            }

            return true;
        }

        private enum RecipeDispenseFailureReason
        {
            None,
            MissingOutputContainer,
            ReagentNotFound,
            NotEnoughReagent,
            TransferFailed,
        }
                private void ErrorSound(Entity<ReagentDispenserComponent> reagentDispenser) // Pirate: chem recipes
        {
            _audioSystem.PlayPvs(reagentDispenser.Comp.ErrorSound, reagentDispenser, AudioParams.Default.WithVolume(-2f));
        }

        #endregion
    }
}
