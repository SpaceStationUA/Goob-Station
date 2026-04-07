using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared._Pirate.Coffeemaker;
using Content.Shared._Pirate.Coffeemaker.Components;
using Content.Shared._White.RadialSelector;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.UserInterface;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Coffeemaker.EntitySystems;

public sealed class CoffeemakerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const string BrewRadialAction = "coffeemaker-verb-brew";
    private const string TakeBeansRadialAction = "coffeemaker-verb-take-beans";
    private readonly SpriteSpecifier _brewIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Pirate/Interface/Radial/coffeemaker.rsi"), "radial_brew");
    private readonly SpriteSpecifier _cupIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Pirate/Objects/Consumable/Drinks/coffeemaker_drinks.rsi"), "coffee");
    private readonly SpriteSpecifier _sugarIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Consumable/Food/condiments.rsi"), "packet-sugar");
    private readonly SpriteSpecifier _sweetenerIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Consumable/Food/condiments.rsi"), "packet-astrotame");
    private readonly SpriteSpecifier _creamerIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Consumable/Food/condiments.rsi"), "packet-mixed");
    private readonly SpriteSpecifier _beansIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Pirate/Objects/Specific/Hydroponics/coffee_beans.rsi"), "coffee_arabica");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoffeemakerComponent, ComponentInit>(OnRefreshState);
        SubscribeLocalEvent<CoffeemakerComponent, MapInitEvent>(OnRefreshState);
        SubscribeLocalEvent<CoffeemakerComponent, EntInsertedIntoContainerMessage>(OnRefreshState);
        SubscribeLocalEvent<CoffeemakerComponent, EntRemovedFromContainerMessage>(OnRefreshState);
        SubscribeLocalEvent<CoffeemakerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<CoffeemakerComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<CoffeemakerComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<CoffeemakerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CoffeemakerComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpened);
        SubscribeLocalEvent<CoffeemakerComponent, RadialSelectorSelectedMessage>(OnRadialSelected);
        SubscribeLocalEvent<CoffeemakerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CoffeepotComponent, SolutionContainerChangedEvent>(OnCoffeepotSolutionChanged);
        SubscribeLocalEvent<CoffeepotComponent, GettingPickedUpAttemptEvent>(OnCoffeepotPickupAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var steamQuery = EntityQueryEnumerator<CoffeemakerSteamPuffComponent, TransformComponent>();
        while (steamQuery.MoveNext(out var steamUid, out var steamPuff, out var steamXform))
        {
            if (TerminatingOrDeleted(steamUid))
                continue;

            var coords = _transform.GetMapCoordinates(steamUid, steamXform);
            var target = new MapCoordinates(coords.Position + steamPuff.Velocity * frameTime, coords.MapId);
            _transform.SetMapCoordinates((steamUid, steamXform), target);
        }

        var query = EntityQueryEnumerator<CoffeemakerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.Brewing)
            {
                StopSteamEffect((uid, component));
                continue;
            }

            if (_timing.CurTime >= component.BrewCompleteAt)
            {
                FinishBrew((uid, component));
                continue;
            }

            EnsureSteamEffect((uid, component));
            TrySpawnSteamPuff((uid, component));
        }
    }

    private void OnRefreshState<T>(Entity<CoffeemakerComponent> ent, ref T args)
    {
        SyncSteamEffect(ent);
        RefreshAppearance(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<CoffeemakerComponent> ent, ref ComponentShutdown args)
    {
        StopSteamEffect(ent);
    }

    private void OnItemSlotInsertAttempt(Entity<CoffeemakerComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (ent.Comp.Brewing)
            args.Cancelled = true;
    }

    private void OnItemSlotEjectAttempt(Entity<CoffeemakerComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (ent.Comp.Brewing)
            args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<CoffeemakerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Brewing)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-busy"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (TryComp<CoffeepotComponent>(args.Used, out _))
        {
            args.Handled = true;
            TryInsertPot(ent, args.User, args.Used);
            return;
        }

        if (!ent.Comp.UsesBeans && TryComp<CoffeeCartridgeComponent>(args.Used, out _))
        {
            args.Handled = true;
            TryInsertCartridge(ent, args.User, args.Used);
            return;
        }

        if (ent.Comp.UsesBeans && TryComp<CoffeeBeanComponent>(args.Used, out _))
        {
            args.Handled = true;
            TryInsertBeans(ent, args.User, args.Used);
            return;
        }

        if (TryRestockCup(ent, args.User, args.Used))
        {
            args.Handled = true;
            return;
        }

        if (TryRestockSugar(ent, args.User, args.Used))
        {
            args.Handled = true;
            return;
        }

        if (TryRestockSweetener(ent, args.User, args.Used))
        {
            args.Handled = true;
            return;
        }

        if (TryRestockCreamer(ent, args.User, args.Used))
            args.Handled = true;
    }

    private void OnBeforeUiOpened(Entity<CoffeemakerComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        var entries = new List<RadialSelectorEntry>
        {
            new()
            {
                Prototype = BrewRadialAction,
                Icon = _brewIcon,
            }
        };

        if (ent.Comp.Cups > 0)
        {
            entries.Add(new RadialSelectorEntry
            {
                Prototype = ent.Comp.CupPrototype.Id,
                Icon = _cupIcon,
            });
        }

        if (ent.Comp.SugarPacks > 0)
        {
            entries.Add(new RadialSelectorEntry
            {
                Prototype = ent.Comp.SugarPacketPrototype.Id,
                Icon = _sugarIcon,
            });
        }

        if (ent.Comp.SweetenerPacks > 0)
        {
            entries.Add(new RadialSelectorEntry
            {
                Prototype = ent.Comp.SweetenerPacketPrototype.Id,
                Icon = _sweetenerIcon,
            });
        }

        if (ent.Comp.CreamerPacks > 0)
        {
            entries.Add(new RadialSelectorEntry
            {
                Prototype = ent.Comp.CreamerPacketPrototype.Id,
                Icon = _creamerIcon,
            });
        }

        if (ent.Comp.UsesBeans && TryPeekOneBean(ent.Owner, ent.Comp, out var bean))
        {
            var entry = new RadialSelectorEntry
            {
                Prototype = TakeBeansRadialAction,
                Icon = _beansIcon,
            };

            var beanPrototypeId = CompOrNull<MetaDataComponent>(bean.Value)?.EntityPrototype?.ID;
            if (beanPrototypeId != null)
                entry.Icon = new SpriteSpecifier.EntityPrototype(beanPrototypeId);

            entries.Add(entry);
        }

        _ui.SetUiState(ent.Owner, RadialSelectorUiKey.Key, new RadialSelectorState(entries));
    }

    private void OnRadialSelected(Entity<CoffeemakerComponent> ent, ref RadialSelectorSelectedMessage args)
    {
        switch (args.SelectedItem)
        {
            case BrewRadialAction:
                TryStartBrew(ent, args.Actor);
                break;
            case TakeBeansRadialAction:
                TryDispenseBean(ent, args.Actor);
                break;
            default:
                if (args.SelectedItem == ent.Comp.CupPrototype.Id)
                    TryDispenseCup(ent, args.Actor);
                else if (args.SelectedItem == ent.Comp.SugarPacketPrototype.Id)
                    TryDispenseSugar(ent, args.Actor);
                else if (args.SelectedItem == ent.Comp.SweetenerPacketPrototype.Id)
                    TryDispenseSweetener(ent, args.Actor);
                else if (args.SelectedItem == ent.Comp.CreamerPacketPrototype.Id)
                    TryDispenseCreamer(ent, args.Actor);
                break;
        }

        _ui.CloseUi(ent.Owner, RadialSelectorUiKey.Key, args.Actor);
    }

    private void OnExamined(Entity<CoffeemakerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("coffeemaker-examine-header"));
        args.PushMarkup(Loc.GetString("coffeemaker-examine-brewing", ("value", ent.Comp.Brewing ? Loc.GetString("coffeemaker-status-yes") : Loc.GetString("coffeemaker-status-no"))));

        var pot = _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.PotSlotId);
        args.PushMarkup(Loc.GetString("coffeemaker-examine-pot", ("value", pot is { Valid: true } ? Loc.GetString("coffeemaker-status-yes") : Loc.GetString("coffeemaker-status-no"))));

        if (ent.Comp.UsesBeans)
        {
            args.PushMarkup(Loc.GetString("coffeemaker-examine-beans", ("count", GetBeanCount(ent.Owner, ent.Comp)), ("capacity", ent.Comp.BeanCapacity)));
        }
        else if (ent.Comp.CartridgeSlotId is { } cartridgeSlotId)
        {
            var cartridge = _itemSlots.GetItemOrNull(ent.Owner, cartridgeSlotId);
            var charges = 0;
            if (cartridge is { Valid: true } && TryComp<CoffeeCartridgeComponent>(cartridge.Value, out var cartridgeComponent))
                charges = cartridgeComponent.Charges;

            args.PushMarkup(Loc.GetString("coffeemaker-examine-cartridge", ("charges", charges)));
        }

        args.PushMarkup(Loc.GetString("coffeemaker-examine-cups", ("count", ent.Comp.Cups)));
        args.PushMarkup(Loc.GetString("coffeemaker-examine-sugar", ("count", ent.Comp.SugarPacks)));
        args.PushMarkup(Loc.GetString("coffeemaker-examine-sweetener", ("count", ent.Comp.SweetenerPacks)));
        args.PushMarkup(Loc.GetString("coffeemaker-examine-creamer", ("count", ent.Comp.CreamerPacks)));
    }

    private void OnCoffeepotSolutionChanged(Entity<CoffeepotComponent> ent, ref SolutionContainerChangedEvent args)
    {
        var parent = Transform(ent.Owner).ParentUid;
        if (!TryComp<CoffeemakerComponent>(parent, out var coffeemaker))
            return;

        var insertedPot = _itemSlots.GetItemOrNull(parent, coffeemaker.PotSlotId);
        if (insertedPot is not { Valid: true } || insertedPot.Value != ent.Owner)
            return;

        RefreshAppearance(parent, coffeemaker);
    }

    private void OnCoffeepotPickupAttempt(Entity<CoffeepotComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        var parent = Transform(ent.Owner).ParentUid;
        if (!TryComp<CoffeemakerComponent>(parent, out var coffeemaker) || !coffeemaker.Brewing)
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-busy"), parent, args.User);
    }

    private bool TryStartBrew(Entity<CoffeemakerComponent> ent, EntityUid user)
    {
        if (ent.Comp.Brewing)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-busy"), ent, user);
            return false;
        }

        if (!TryGetRefillablePot(ent.Owner, ent.Comp, out _, out var potSolution))
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-pot"), ent, user);
            return false;
        }

        if (potSolution.AvailableVolume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-pot-full"), ent, user);
            return false;
        }

        if (!IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-unpowered"), ent, user);
            return false;
        }

        if (ent.Comp.UsesBeans)
        {
            if (GetBeanCount(ent.Owner, ent.Comp) <= 0)
            {
                _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-beans"), ent, user);
                return false;
            }
        }
        else
        {
            if (!TryGetCartridge(ent.Owner, ent.Comp, out var cartridgeComponent))
            {
                _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-cartridge"), ent, user);
                return false;
            }

            if (cartridgeComponent.Charges <= 0)
            {
                _popup.PopupEntity(Loc.GetString("coffeemaker-popup-cartridge-empty"), ent, user);
                return false;
            }
        }

        ent.Comp.Brewing = true;
        ent.Comp.BrewCompleteAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.BrewTime);
        EnsureSteamEffect(ent);
        _audio.PlayPvs(ent.Comp.BrewSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-brewing"), ent, user);
        return true;
    }

    private void FinishBrew(Entity<CoffeemakerComponent> ent)
    {
        ent.Comp.Brewing = false;
        StopSteamEffect(ent);
        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-brew-finished"), ent.Owner);

        if (!TryGetRefillablePot(ent.Owner, ent.Comp, out var potSolutionEntity, out var potSolution))
        {
            RefreshAppearance(ent.Owner, ent.Comp);
            return;
        }

        if (ent.Comp.UsesBeans)
        {
            if (!TryPeekOneBean(ent.Owner, ent.Comp, out var bean))
            {
                RefreshAppearance(ent.Owner, ent.Comp);
                return;
            }

            var brewReagent = "Coffee";
            if (TryComp<CoffeeBeanComponent>(bean.Value, out var beanComponent))
            {
                brewReagent = beanComponent.BrewReagent;
                AddReagents(potSolutionEntity.Value, beanComponent.ExtraReagents);
            }

            QueueDel(bean.Value);
            var amount = FixedPoint2.Min(ent.Comp.BrewVolume, potSolution.AvailableVolume);
            if (amount > FixedPoint2.Zero)
                _solutions.TryAddReagent(potSolutionEntity.Value, brewReagent, amount);
        }
        else
        {
            if (!TryGetCartridge(ent.Owner, ent.Comp, out var cartridgeComponent))
            {
                RefreshAppearance(ent.Owner, ent.Comp);
                return;
            }

            if (cartridgeComponent.Charges <= 0)
            {
                RefreshAppearance(ent.Owner, ent.Comp);
                return;
            }

            AddReagents(potSolutionEntity.Value, cartridgeComponent.ExtraReagents);
            var amount = FixedPoint2.Min(cartridgeComponent.BrewAmount, potSolution.AvailableVolume);
            if (amount > FixedPoint2.Zero)
                _solutions.TryAddReagent(potSolutionEntity.Value, cartridgeComponent.BrewReagent, amount);
            cartridgeComponent.Charges--;
        }

        RefreshAppearance(ent.Owner, ent.Comp);
    }

    private void SyncSteamEffect(Entity<CoffeemakerComponent> ent)
    {
        if (ent.Comp.Brewing)
            EnsureSteamEffect(ent);
        else
            StopSteamEffect(ent);
    }

    private void EnsureSteamEffect(Entity<CoffeemakerComponent> ent)
    {
        if (ent.Comp.ActiveSteamEffect is { } steam && Exists(steam))
            return;

        var spawned = Spawn(ent.Comp.SteamEffectPrototype, Transform(ent.Owner).Coordinates);
        _transform.SetParent(spawned, ent.Owner);
        _transform.SetLocalPosition(spawned, ent.Comp.SteamOffset);
        ent.Comp.ActiveSteamEffect = spawned;
        ent.Comp.NextSteamPuffAt = _timing.CurTime;
    }

    private void StopSteamEffect(Entity<CoffeemakerComponent> ent)
    {
        if (ent.Comp.ActiveSteamEffect is not { } steam)
            return;

        if (Exists(steam))
            QueueDel(steam);

        ent.Comp.ActiveSteamEffect = null;
    }

    private void TrySpawnSteamPuff(Entity<CoffeemakerComponent> ent)
    {
        if (ent.Comp.ActiveSteamEffect is not { } emitter || !Exists(emitter))
            return;

        if (_timing.CurTime < ent.Comp.NextSteamPuffAt)
            return;

        var emitterCoords = _transform.GetMapCoordinates(emitter);
        var spawned = Spawn(ent.Comp.SteamPuffPrototype, emitterCoords);

        if (TryComp<CoffeemakerSteamPuffComponent>(spawned, out var puff))
        {
            var speed = _random.NextFloat(0.48f, 0.72f);
            var upwardVelocity = new Vector2(0f, speed);
            var spread = Angle.FromDegrees(_random.NextFloat(-10f, 10f));
            var emitterRotation = _transform.GetWorldRotation(emitter);

            // Start from the coffeemaker's local "up", then add a small random spread for a cone-like puff.
            puff.Velocity = emitterRotation.RotateVec(spread.RotateVec(upwardVelocity));
        }

        ent.Comp.NextSteamPuffAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SteamPuffInterval);
    }

    private void AddReagents(Entity<SolutionComponent> solution, List<ReagentQuantity> reagents)
    {
        foreach (var reagent in reagents)
        {
            if (reagent.Quantity <= FixedPoint2.Zero)
                continue;

            _solutions.TryAddReagent(solution, reagent.Reagent, reagent.Quantity, out _);
        }
    }

    private void TryInsertPot(Entity<CoffeemakerComponent> ent, EntityUid user, EntityUid used)
    {
        if (_itemSlots.TryInsert(ent.Owner, ent.Comp.PotSlotId, used, user))
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-pot-inserted"), ent, user);
            RefreshAppearance(ent.Owner, ent.Comp);
            return;
        }

        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-cannot-insert-pot"), ent, user);
    }

    private void TryInsertCartridge(Entity<CoffeemakerComponent> ent, EntityUid user, EntityUid used)
    {
        if (ent.Comp.CartridgeSlotId is not { } cartridgeSlotId)
            return;

        if (!_itemSlots.TryGetSlot(ent.Owner, cartridgeSlotId, out var cartridgeSlot))
            return;

        EntityUid? previousCartridge = null;
        if (cartridgeSlot.HasItem)
        {
            if (!_itemSlots.TryEject(ent.Owner, cartridgeSlot, user, out previousCartridge))
            {
                _popup.PopupEntity(Loc.GetString("coffeemaker-popup-cannot-insert-cartridge"), ent, user);
                return;
            }

            if (previousCartridge is { } removedCartridge)
                _hands.PickupOrDrop(user, removedCartridge);
        }

        if (_itemSlots.TryInsert(ent.Owner, cartridgeSlot, used, user))
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-cartridge-inserted"), ent, user);
            RefreshAppearance(ent.Owner, ent.Comp);
            return;
        }

        if (previousCartridge is { } oldCartridge && Exists(oldCartridge))
            _itemSlots.TryInsert(ent.Owner, cartridgeSlot, oldCartridge, user);

        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-cannot-insert-cartridge"), ent, user);
    }

    private void TryInsertBeans(Entity<CoffeemakerComponent> ent, EntityUid user, EntityUid used)
    {
        if (!TryGetBeanContainer(ent.Owner, ent.Comp, out var beanContainer))
            return;

        if (beanContainer.ContainedEntities.Count >= ent.Comp.BeanCapacity)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-bean-capacity"), ent, user);
            return;
        }

        if (!_containers.Insert(used, beanContainer))
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-cannot-insert-beans"), ent, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-added-beans"), ent, user);
        RefreshAppearance(ent.Owner, ent.Comp);
    }

    private bool TryRestockCup(Entity<CoffeemakerComponent> ent, EntityUid user, EntityUid used)
    {
        if (!HasPrototype(used, ent.Comp.CupPrototype))
            return false;

        if (!_solutions.TryGetDrainableSolution(used, out _, out var cupSolution))
            return false;

        if (cupSolution.Volume > FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-cup-not-empty"), ent, user);
            return true;
        }

        if (ent.Comp.Cups >= ent.Comp.MaxCups)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-cups-max"), ent, user);
            return true;
        }

        QueueDel(used);
        ent.Comp.Cups++;
        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-added-cup"), ent, user);
        RefreshAppearance(ent.Owner, ent.Comp);
        return true;
    }

    private bool TryRestockSugar(Entity<CoffeemakerComponent> ent, EntityUid user, EntityUid used)
    {
        if (!HasPrototype(used, ent.Comp.SugarPacketPrototype) && !HasPrototype(used, ent.Comp.FallbackSugarPacketPrototype))
            return false;

        if (!TryRestockPacket(ent, user, used, ref ent.Comp.SugarPacks, ent.Comp.MaxSugarPacks, "coffeemaker-popup-sugar-max"))
            return true;

        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-added-sugar"), ent, user);
        return true;
    }

    private bool TryRestockSweetener(Entity<CoffeemakerComponent> ent, EntityUid user, EntityUid used)
    {
        if (!HasPrototype(used, ent.Comp.SweetenerPacketPrototype) && !HasPrototype(used, ent.Comp.FallbackSweetenerPacketPrototype))
            return false;

        if (!TryRestockPacket(ent, user, used, ref ent.Comp.SweetenerPacks, ent.Comp.MaxSweetenerPacks, "coffeemaker-popup-sweetener-max"))
            return true;

        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-added-sweetener"), ent, user);
        return true;
    }

    private bool TryRestockCreamer(Entity<CoffeemakerComponent> ent, EntityUid user, EntityUid used)
    {
        if (!HasPrototype(used, ent.Comp.CreamerPacketPrototype))
            return false;

        if (!TryRestockPacket(ent, user, used, ref ent.Comp.CreamerPacks, ent.Comp.MaxCreamerPacks, "coffeemaker-popup-creamer-max"))
            return true;

        _popup.PopupEntity(Loc.GetString("coffeemaker-popup-added-creamer"), ent, user);
        return true;
    }

    private bool TryRestockPacket(
        Entity<CoffeemakerComponent> ent,
        EntityUid user,
        EntityUid used,
        ref int current,
        int max,
        string maxPopup)
    {
        if (!_solutions.TryGetDrainableSolution(used, out _, out var packetSolution))
            return false;

        if (packetSolution.Volume < packetSolution.MaxVolume)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-packet-not-full"), ent, user);
            return false;
        }

        if (current >= max)
        {
            _popup.PopupEntity(Loc.GetString(maxPopup), ent, user);
            return false;
        }

        QueueDel(used);
        current++;
        RefreshAppearance(ent.Owner, ent.Comp);
        return true;
    }

    private void TryDispenseCup(Entity<CoffeemakerComponent> ent, EntityUid user)
    {
        if (ent.Comp.Cups <= 0)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-cups"), ent, user);
            return;
        }

        DispenseItem(ent.Owner, user, ent.Comp.CupPrototype);
        ent.Comp.Cups--;
        RefreshAppearance(ent.Owner, ent.Comp);
    }

    private void TryDispenseSugar(Entity<CoffeemakerComponent> ent, EntityUid user)
    {
        if (ent.Comp.SugarPacks <= 0)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-sugar"), ent, user);
            return;
        }

        DispenseItem(ent.Owner, user, ent.Comp.SugarPacketPrototype);
        ent.Comp.SugarPacks--;
        RefreshAppearance(ent.Owner, ent.Comp);
    }

    private void TryDispenseSweetener(Entity<CoffeemakerComponent> ent, EntityUid user)
    {
        if (ent.Comp.SweetenerPacks <= 0)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-sweetener"), ent, user);
            return;
        }

        DispenseItem(ent.Owner, user, ent.Comp.SweetenerPacketPrototype);
        ent.Comp.SweetenerPacks--;
        RefreshAppearance(ent.Owner, ent.Comp);
    }

    private void TryDispenseCreamer(Entity<CoffeemakerComponent> ent, EntityUid user)
    {
        if (ent.Comp.CreamerPacks <= 0)
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-creamer"), ent, user);
            return;
        }

        DispenseItem(ent.Owner, user, ent.Comp.CreamerPacketPrototype);
        ent.Comp.CreamerPacks--;
        RefreshAppearance(ent.Owner, ent.Comp);
    }

    private void TryDispenseBean(Entity<CoffeemakerComponent> ent, EntityUid user)
    {
        if (!ent.Comp.UsesBeans || !TryPeekOneBean(ent.Owner, ent.Comp, out var bean, out var beanContainer))
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-beans"), ent, user);
            return;
        }

        if (!_containers.Remove(bean.Value, beanContainer))
        {
            _popup.PopupEntity(Loc.GetString("coffeemaker-popup-no-beans"), ent, user);
            return;
        }

        _hands.PickupOrDrop(user, bean.Value);
        RefreshAppearance(ent.Owner, ent.Comp);
    }

    private void DispenseItem(EntityUid machine, EntityUid user, EntProtoId prototype)
    {
        var item = Spawn(prototype, Transform(machine).Coordinates);
        _hands.PickupOrDrop(user, item);
    }

    private void RefreshAppearance(EntityUid uid, CoffeemakerComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        var potState = CoffeemakerPotVisualState.None;
        var pot = _itemSlots.GetItemOrNull(uid, component.PotSlotId);
        if (pot is { Valid: true })
        {
            if (_solutions.TryGetRefillableSolution(pot.Value, out _, out var solution) && solution.Volume > FixedPoint2.Zero)
                potState = CoffeemakerPotVisualState.Full;
            else
                potState = CoffeemakerPotVisualState.Empty;
        }

        _appearance.SetData(uid, CoffeemakerVisuals.PotState, potState, appearance);
        _appearance.SetData(uid, CoffeemakerVisuals.HasCartridge, HasCartridge(uid, component), appearance);
        _appearance.SetData(uid, CoffeemakerVisuals.CupsState, GetCupsState(component), appearance);
        _appearance.SetData(uid, CoffeemakerVisuals.HasSugar, component.SugarPacks > 0, appearance);
        _appearance.SetData(uid, CoffeemakerVisuals.HasCreamer, component.CreamerPacks > 0, appearance);
        _appearance.SetData(uid, CoffeemakerVisuals.HasSweetener, component.SweetenerPacks > 0, appearance);
        _appearance.SetData(uid, CoffeemakerVisuals.GrinderState, GetGrinderState(uid, component), appearance);
    }

    private CoffeemakerCupsVisualState GetCupsState(CoffeemakerComponent component)
    {
        if (component.Cups <= 0)
            return CoffeemakerCupsVisualState.None;

        if (component.MaxCups <= 0)
            return CoffeemakerCupsVisualState.None;

        if (component.Cups * 3 > component.MaxCups * 2)
            return CoffeemakerCupsVisualState.High;

        if (component.Cups * 3 > component.MaxCups)
            return CoffeemakerCupsVisualState.Medium;

        return CoffeemakerCupsVisualState.Low;
    }

    private CoffeemakerGrinderVisualState GetGrinderState(EntityUid uid, CoffeemakerComponent component)
    {
        if (!component.UsesBeans)
            return CoffeemakerGrinderVisualState.None;

        var count = GetBeanCount(uid, component);
        if (count <= 0)
            return CoffeemakerGrinderVisualState.None;

        if (count * 10 >= component.BeanCapacity * 7)
            return CoffeemakerGrinderVisualState.Full;

        return CoffeemakerGrinderVisualState.Half;
    }

    private bool HasCartridge(EntityUid uid, CoffeemakerComponent component)
    {
        if (component.CartridgeSlotId is not { } cartridgeSlotId)
            return false;

        return _itemSlots.GetItemOrNull(uid, cartridgeSlotId) is { Valid: true };
    }

    private bool IsPowered(EntityUid uid)
    {
        return !TryComp<ApcPowerReceiverComponent>(uid, out var receiver) || receiver.Powered;
    }

    private bool TryGetCartridge(EntityUid uid, CoffeemakerComponent component, [NotNullWhen(true)] out CoffeeCartridgeComponent? cartridgeComponent)
    {
        cartridgeComponent = null;
        if (component.CartridgeSlotId is not { } cartridgeSlotId)
            return false;

        var cartridge = _itemSlots.GetItemOrNull(uid, cartridgeSlotId);
        return cartridge is { Valid: true } && TryComp(cartridge.Value, out cartridgeComponent);
    }

    private bool TryGetRefillablePot(EntityUid uid, CoffeemakerComponent component, [NotNullWhen(true)] out Entity<SolutionComponent>? solutionEntity, [NotNullWhen(true)] out Solution? solution)
    {
        solutionEntity = null;
        solution = null;
        var pot = _itemSlots.GetItemOrNull(uid, component.PotSlotId);
        return pot is { Valid: true } && _solutions.TryGetRefillableSolution(pot.Value, out solutionEntity, out solution);
    }

    private int GetBeanCount(EntityUid uid, CoffeemakerComponent component)
    {
        if (!TryGetBeanContainer(uid, component, out var beanContainer))
            return 0;

        return beanContainer.ContainedEntities.Count;
    }

    private bool TryPeekOneBean(EntityUid uid, CoffeemakerComponent component, [NotNullWhen(true)] out EntityUid? bean)
    {
        return TryPeekOneBean(uid, component, out bean, out _);
    }

    private bool TryPeekOneBean(EntityUid uid,
        CoffeemakerComponent component,
        [NotNullWhen(true)] out EntityUid? bean,
        [NotNullWhen(true)] out Container? beanContainer)
    {
        bean = null;
        beanContainer = null;
        if (!TryGetBeanContainer(uid, component, out beanContainer))
            return false;

        if (beanContainer.ContainedEntities.Count <= 0)
        {
            beanContainer = null;
            return false;
        }

        bean = beanContainer.ContainedEntities[0];
        return true;
    }

    private bool TryGetBeanContainer(EntityUid uid, CoffeemakerComponent component, [NotNullWhen(true)] out Container? container)
    {
        container = null;
        if (!_containers.TryGetContainer(uid, component.BeanContainerId, out var baseContainer))
            return false;

        if (baseContainer is not Container realContainer)
        {
            Log.Warning($"Coffeemaker {uid} bean container '{component.BeanContainerId}' was expected to be {nameof(Container)} but got {baseContainer.GetType().Name}.");
            return false;
        }

        container = realContainer;
        return true;
    }

    private bool HasPrototype(EntityUid uid, EntProtoId prototype)
    {
        var id = CompOrNull<MetaDataComponent>(uid)?.EntityPrototype?.ID;
        return id != null && prototype.Equals(id);
    }
}
