// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Ingestion;
using Content.Pirate.Shared.Ranching;
using Content.Server.Power.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Ranching;

public sealed class RanchingEggSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly RanchingHappinessSystem _happiness = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private readonly List<Entity<RanchingEggLayerComponent>> _layersToUpdate = new();
    private readonly List<Entity<ActiveRanchingHatchComponent>> _eggsToHatch = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RanchingEggLayerComponent, MapInitEvent>(OnEggLayerMapInit);
        SubscribeLocalEvent<EggFertilizerComponent, AfterEatingEvent>(OnFertilizerFoodEaten);

        SubscribeLocalEvent<EggFertilizationTargetComponent, ActivateInWorldEvent>(OnEggActivated);
        SubscribeLocalEvent<RanchingHatchableComponent, FertilizeDoAfterEvent>(OnFertilized);

        SubscribeLocalEvent<EggIncubatorComponent, EntInsertedIntoContainerMessage>(OnEggInserted);
        SubscribeLocalEvent<EggIncubatorComponent, EntRemovedFromContainerMessage>(OnEggRemoved);
        SubscribeLocalEvent<EggIncubatorComponent, PowerChangedEvent>(OnIncubatorPowerChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _layersToUpdate.Clear();
        var layerQuery = EntityQueryEnumerator<RanchingEggLayerComponent>();
        while (layerQuery.MoveNext(out var uid, out var layer))
        {
            if (_mobState.IsDead(uid) || _mobState.IsCritical(uid) || _timing.CurTime < layer.NextGrowth)
                continue;

            if (layer.HungerRequired && !HasComp<HungerComponent>(uid))
                continue;

            layer.NextGrowth = _timing.CurTime +
                TimeSpan.FromSeconds(_random.NextFloat(layer.EggLayCooldownMin, layer.EggLayCooldownMax));
            _layersToUpdate.Add((uid, layer));
        }

        foreach (var layer in _layersToUpdate)
            TryLayEgg(layer);

        _eggsToHatch.Clear();
        var hatchQuery = EntityQueryEnumerator<ActiveRanchingHatchComponent>();
        while (hatchQuery.MoveNext(out var uid, out var active))
        {
            if (_timing.CurTime >= active.HatchAt)
                _eggsToHatch.Add((uid, active));
        }

        foreach (var egg in _eggsToHatch)
            HatchEgg(egg.Owner);
    }

    private void OnEggLayerMapInit(Entity<RanchingEggLayerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextGrowth = _timing.CurTime +
            TimeSpan.FromSeconds(_random.NextFloat(ent.Comp.EggLayCooldownMin, ent.Comp.EggLayCooldownMax));
    }

    private void OnFertilizerFoodEaten(Entity<EggFertilizerComponent> ent, ref AfterEatingEvent args)
    {
        foreach (var (tag, replacement) in ent.Comp.SpecialReplacementsByFoodTag)
        {
            if (!_tags.HasTag(args.Food, tag))
                continue;

            ent.Comp.SpecialReplacement = replacement;
            return;
        }
    }

    private void OnEggActivated(Entity<EggFertilizationTargetComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !TryComp<EggFertilizerComponent>(args.User, out var fertilizer))
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            fertilizer.DoAfter,
            new FertilizeDoAfterEvent(),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        });
    }

    private void OnFertilized(Entity<RanchingHatchableComponent> ent, ref FertilizeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target != ent.Owner ||
            !TryComp<EggFertilizerComponent>(args.User, out var fertilizer))
            return;

        args.Handled = true;

        if (fertilizer.SpecialReplacement is { } replacement)
        {
            if (fertilizer.SpecialReplacementRequiredEgg is { } required &&
                Prototype(ent.Owner)?.ID != required)
                return;

            ent.Comp.Entity = replacement;
            if (fertilizer.SpecialReplacementRequiredEgg == null)
                fertilizer.SpecialReplacement = null;

            if (TryComp<HappinessComponent>(args.User, out var happiness))
                _happiness.SetHappiness((args.User, happiness), 30f);
        }

        RemComp<EggFertilizationTargetComponent>(ent);
        StartHatching(ent);
    }

    private void OnEggInserted(Entity<EggIncubatorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Slot || !TryComp<RanchingHatchableComponent>(args.Entity, out var hatchable))
            return;

        _appearance.SetData(ent, EggIncubatorVisuals.Egg, true);
        if (IsIncubatorPowered(ent.Owner))
            StartHatching((args.Entity, hatchable));
    }

    private void OnEggRemoved(Entity<EggIncubatorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Slot || !HasComp<RanchingHatchableComponent>(args.Entity))
            return;

        if (HasComp<EggFertilizationTargetComponent>(args.Entity))
            RemComp<ActiveRanchingHatchComponent>(args.Entity);

        _appearance.SetData(ent, EggIncubatorVisuals.Egg, false);
    }

    private void OnIncubatorPowerChanged(Entity<EggIncubatorComponent> ent, ref PowerChangedEvent args)
    {
        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.Slot, out var slot) || slot.Item is not { } egg ||
            !TryComp<RanchingHatchableComponent>(egg, out var hatchable))
            return;

        if (args.Powered)
            StartHatching((egg, hatchable));
        else if (HasComp<EggFertilizationTargetComponent>(egg))
            RemComp<ActiveRanchingHatchComponent>(egg);
    }

    private bool IsIncubatorPowered(EntityUid uid)
    {
        return !TryComp<ApcPowerReceiverComponent>(uid, out var power) || power.Powered;
    }

    private void StartHatching(Entity<RanchingHatchableComponent> egg)
    {
        var active = EnsureComp<ActiveRanchingHatchComponent>(egg);
        active.HatchAt = _timing.CurTime + egg.Comp.Time;
    }

    private void HatchEgg(EntityUid uid)
    {
        if (!TryComp<RanchingHatchableComponent>(uid, out var hatchable))
        {
            RemComp<ActiveRanchingHatchComponent>(uid);
            return;
        }

        if (_containers.TryGetContainingContainer(uid, out var container))
            SpawnNextToOrDrop(hatchable.Entity, container.Owner);
        else
            SpawnNextToOrDrop(hatchable.Entity, uid);

        QueueDel(uid);
    }

    private void TryLayEgg(Entity<RanchingEggLayerComponent> ent)
    {
        HungerComponent? hunger = null;
        if (TryComp<HungerComponent>(ent.Owner, out hunger))
        {
            if (_hunger.GetHunger(hunger) < ent.Comp.HungerUsage ||
                _hunger.GetHungerThreshold(hunger) < ent.Comp.HungerThresholdRequired)
                return;
        }
        else if (ent.Comp.HungerRequired)
        {
            return;
        }

        if (!TryComp<MostRecentlyEatenFoodTagsComponent>(ent, out var foodTags) ||
            !TryComp<HappinessComponent>(ent, out var happiness) ||
            Prototype(ent.Owner) is not { } entityPrototype)
            return;

        var chickenId = new EntProtoId(entityPrototype.ID);
        var recipes = _prototypes.EnumeratePrototypes<EggRecipePrototype>()
            .OrderByDescending(recipe => recipe.ReagentsRequired is { Count: > 0 })
            .ThenByDescending(recipe => recipe.FoodTagsRequired is { Count: > 0 })
            .ThenByDescending(recipe => recipe.Weight)
            .ThenByDescending(recipe => recipe.HappinessRequired);

        foreach (var recipe in recipes)
        {
            if (!recipe.RequiredChicken.Contains(chickenId))
                continue;

            var requiredHappiness = recipe.ChickensRequireDifferentHappiness?.GetValueOrDefault(chickenId)
                ?? recipe.HappinessRequired;
            if (happiness.Current < requiredHappiness)
                continue;

            if (recipe.ComponentsRequired != null &&
                _whitelist.IsWhitelistFail(recipe.ComponentsRequired, ent.Owner))
                continue;

            if (!HasRequiredReagents(ent, recipe))
                continue;

            var needsSpecialFood = recipe.FoodTagsRequired is { Count: > 0 } &&
                !(recipe.NoSpecialFoodRequiredChickens?.Contains(chickenId) ?? false);
            if (needsSpecialFood && !foodTags.Tag.Overlaps(recipe.FoodTagsRequired!))
                continue;

            LayEgg(ent, recipe.Egg, hunger, foodTags);
            return;
        }
    }

    private bool HasRequiredReagents(Entity<RanchingEggLayerComponent> ent, EggRecipePrototype recipe)
    {
        if (recipe.ReagentsRequired is not { Count: > 0 })
            return true;

        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return false;

        foreach (var requirement in recipe.ReagentsRequired)
        {
            var found = false;
            foreach (var reagent in solution.Contents)
            {
                if (reagent.Reagent.Prototype != requirement.Key)
                    continue;

                found = reagent.Quantity >= requirement.Value;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private void LayEgg(
        Entity<RanchingEggLayerComponent> ent,
        EntProtoId egg,
        HungerComponent? hunger,
        MostRecentlyEatenFoodTagsComponent foodTags)
    {
        ent.Comp.EggSpawn = egg;
        SpawnNextToOrDrop(egg, ent.Owner);

        _audio.PlayPvs(ent.Comp.EggLaySound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("action-popup-lay-egg-user"), ent.Owner, ent.Owner);
        _popup.PopupEntity(
            Loc.GetString("action-popup-lay-egg-others", ("entity", ent.Owner)),
            ent.Owner,
            Filter.PvsExcept(ent.Owner, entityManager: EntityManager),
            true);

        if (hunger != null)
            _hunger.ModifyHunger(ent.Owner, -ent.Comp.HungerUsage, hunger);

        foodTags.Tag.Clear();
        var ev = new RanchingEggLaidEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
    }
}
