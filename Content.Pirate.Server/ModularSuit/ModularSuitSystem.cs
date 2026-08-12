using System.Linq;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Lock;
using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitSystem : SharedModularSuitSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private const float ModuleExtractTime = 1.5f;
    private const float CoreExtractTime = 4.0f;
    private const float PartExtractTime = 5.0f;
    private static readonly TimeSpan NonWearerVerbTime = TimeSpan.FromSeconds(2);

    private readonly List<(EntityUid Suit, EntityUid Actor, ModularSuitVerbType Verb)> _pendingVerbs = new();

    public override void Initialize()
    {
        base.Initialize();

        InitializePower();
        InitializeUi();

        SubscribeLocalEvent<ModularSuitComponent, ComponentInit>(OnSuitInit);
        SubscribeLocalEvent<ModularSuitComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<ModularSuitComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ModularSuitComponent, GetVerbsEvent<EquipmentVerb>>(OnGetEquipmentVerbs);
        SubscribeLocalEvent<ModularSuitComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnRelayedEquipmentVerbs);
        SubscribeLocalEvent<ModularSuitComponent, ModularSuitVerbDoAfterEvent>(OnVerbDoAfter);
        SubscribeLocalEvent<ModularSuitComponent, ModularSuitExtractDoAfterEvent>(OnDoAfterComplete);
        SubscribeLocalEvent<ModularSuitComponent, InteractUsingEvent>(OnSuitInteractUsing);

        SubscribeLocalEvent<ModularSuitPreassembledComponent, MapInitEvent>(OnPreassembledMapInit);

        SubscribeLocalEvent<ModularSuitPartComponent, GetVerbsEvent<Verb>>(OnGetPartVerbs);
        SubscribeLocalEvent<ModularSuitPartComponent, ModularSuitPartSealDoAfterEvent>(OnPartDoAfterComplete);
    }

    private void OnSuitInit(Entity<ModularSuitComponent> ent, ref ComponentInit args)
    {
        Container.EnsureContainer<ContainerSlot>(ent, CoreContainer);
        Container.EnsureContainer<Container>(ent, PartContainer);
        Container.EnsureContainer<Container>(ent, ModuleContainer);
        Container.EnsureContainer<Container>(ent, HiddenClothingContainer);
    }

    private void OnGetVerbs(Entity<ModularSuitComponent> suit, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (!TryComp<WiresPanelComponent>(suit, out var panel) || !panel.Open)
            return;

        if (_lock.IsLocked(suit.Owner))
            return;

        var tool = _hands.GetActiveItem(args.User);
        if (tool == null || !_tool.HasQuality(tool.Value, suit.Comp.Tool))
            return;

        var user = args.User;
        var containers = new Dictionary<string, BaseContainer>
        {
            [ModuleContainer] = Container.GetContainer(suit, ModuleContainer),
            [CoreContainer] = Container.GetContainer(suit, CoreContainer),
            [PartContainer] = Container.GetContainer(suit, PartContainer)
        };

        foreach (var module in containers[ModuleContainer].ContainedEntities)
        {
            if (!TryComp<ModularSuitModuleComponent>(module, out var moduleComp))
                continue;

            if (moduleComp.IsPermanent)
                continue;

            var extractVerb = new Verb
            {
                Priority = 2,
                Text = Name(module),
                Icon = moduleComp.VerbIcon,
                Category = VerbCategory.Eject,
                Impact = LogImpact.Low,
                DoContactInteraction = true,
                Act = () => StartExtractDoAfter(suit, module, ModuleExtractTime, user, ModularSuitPart.Module)
            };
            args.Verbs.Add(extractVerb);
        }

        foreach (var core in containers[CoreContainer].ContainedEntities)
        {
            if (!TryComp<ModularSuitCoreComponent>(core, out var coreComp))
                continue;

            var extractVerb = new Verb
            {
                Priority = 1,
                Text = Name(core),
                Icon = coreComp.VerbIcon,
                Category = VerbCategory.Eject,
                Impact = LogImpact.Medium,
                DoContactInteraction = true,
                Act = () => StartExtractDoAfter(suit, core, CoreExtractTime, user, ModularSuitPart.Core)
            };
            args.Verbs.Add(extractVerb);
        }

        foreach (var part in containers[PartContainer].ContainedEntities)
        {
            if (suit.Comp.Equipped || !TryComp<ModularSuitPartComponent>(part, out var partComp))
                continue;

            var extractVerb = new Verb
            {
                Priority = 0,
                Text = Name(part),
                Icon = partComp.VerbIcon,
                Category = VerbCategory.Eject,
                Impact = LogImpact.Low,
                DoContactInteraction = true,
                Act = () => StartExtractDoAfter(suit, part, PartExtractTime, user, ModularSuitPart.Part)
            };
            args.Verbs.Add(extractVerb);
        }
    }

    private void OnGetVerbs(Entity<ModularSuitComponent> suit, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (suit.Comp.Active)
            return;

        var user = args.User;
        var verb = new AlternativeVerb
        {
            Priority = 4,
            Text = suit.Comp.AutoActivateOnAssemble
                ? Loc.GetString("modsuit-verb-auto-activate-off")
                : Loc.GetString("modsuit-verb-auto-activate-on"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Act = () =>
            {
                suit.Comp.AutoActivateOnAssemble = !suit.Comp.AutoActivateOnAssemble;
                Dirty(suit.Owner, suit.Comp);

                Popup.PopupEntity(Loc.GetString("modsuit-verb-auto-activate-toggled"), suit.Owner, user);
            },
        };

        args.Verbs.Add(verb);
    }

    private void OnRelayedEquipmentVerbs(Entity<ModularSuitComponent> suit, ref InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        OnGetEquipmentVerbs(suit, ref args.Args);
    }

    /// <summary>
    ///     The deploy and seal verbs the legacy modsuits show when right-clicking their wearer.
    /// </summary>
    private void OnGetEquipmentVerbs(Entity<ModularSuitComponent> suit, ref GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanComplexInteract || suit.Comp.Wearer is not { } wearer)
            return;

        // The control unit lives inside the wearer's inventory, so CanAccess is false for everyone
        // but them - check reach against the suit instead.
        if (!_interaction.InRangeUnobstructed(args.User, suit.Owner))
            return;

        var user = args.User;

        args.Verbs.Add(new EquipmentVerb
        {
            Priority = 6,
            Text = Loc.GetString(suit.Comp.Deployed ? "modsuit-verb-retract" : "modsuit-verb-deploy"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Act = () => StartVerb(suit, user, wearer, ModularSuitVerbType.ToggleDeploy),
        });

        args.Verbs.Add(new EquipmentVerb
        {
            Priority = 5,
            Text = Loc.GetString("modsuit-verb-toggle-seal"),
            Icon = new SpriteSpecifier.Texture(new(suit.Comp.Assembled
                ? "/Textures/Interface/VerbIcons/unlock.svg.192dpi.png"
                : "/Textures/Interface/VerbIcons/lock.svg.192dpi.png")),
            Act = () => StartVerb(suit, user, wearer, ModularSuitVerbType.ToggleSeal),
        });
    }

    private void StartVerb(Entity<ModularSuitComponent> suit, EntityUid user, EntityUid wearer, ModularSuitVerbType verb)
    {
        if (user == wearer)
        {
            RunVerb(suit, user, verb);
            return;
        }

        // RunVerb validates again because suit state can change during the delay.
        if (!CanRunVerb(suit, wearer, user, verb))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, NonWearerVerbTime,
            new ModularSuitVerbDoAfterEvent(verb), suit.Owner, wearer, suit.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            Popup.PopupEntity(Loc.GetString("modsuit-verb-other-start"), wearer, user);
    }

    private bool CanRunVerb(
        Entity<ModularSuitComponent> suit,
        EntityUid wearer,
        EntityUid actor,
        ModularSuitVerbType verb)
    {
        var springlocked = TryComp<AffectedModuleSpringlockComponent>(wearer, out var springlock)
                           && springlock.Locked;

        switch (verb)
        {
            case ModularSuitVerbType.ToggleDeploy when suit.Comp.Deployed && suit.Comp.Active:
                Refuse(suit, actor, "modsuit-retract-blocked-active");
                return false;
            case ModularSuitVerbType.ToggleDeploy when suit.Comp.Deployed && springlocked:
                Refuse(suit, actor, "modsuit-undeploy-blocked");
                return false;
            case ModularSuitVerbType.ToggleSeal when !suit.Comp.Deployed:
                Refuse(suit, actor, "modsuit-seal-blocked-undeployed");
                return false;
            case ModularSuitVerbType.ToggleSeal when !suit.Comp.Assembled && !AllRequiredPartsDeployed(suit):
                Refuse(suit, actor, "modsuit-seal-failed");
                return false;
        }

        return true;
    }

    private void OnVerbDoAfter(Entity<ModularSuitComponent> suit, ref ModularSuitVerbDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        // Run after do-after iteration to avoid mutating its component collection.
        _pendingVerbs.Add((suit.Owner, args.User, args.Verb));
    }

    private void ProcessPendingVerbs()
    {
        if (_pendingVerbs.Count == 0)
            return;

        var pending = _pendingVerbs.ToArray();
        _pendingVerbs.Clear();

        foreach (var (suitUid, actor, verb) in pending)
        {
            if (TryComp<ModularSuitComponent>(suitUid, out var suit))
                RunVerb((suitUid, suit), actor, verb);
        }
    }

    private void RunVerb(Entity<ModularSuitComponent> suit, EntityUid actor, ModularSuitVerbType verb)
    {
        if (suit.Comp.Wearer is not { } wearer)
            return;

        switch (verb)
        {
            case ModularSuitVerbType.ToggleDeploy:
                ToggleDeploy(suit, wearer, actor);
                break;
            case ModularSuitVerbType.ToggleSeal:
                ToggleSeal(suit, wearer, actor);
                break;
        }
    }

    private void StartExtractDoAfter(EntityUid suit, EntityUid target, float delay, EntityUid user, ModularSuitPart type)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay,
            new ModularSuitExtractDoAfterEvent(type), suit, suit, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 0.01f
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        Popup.PopupEntity(Loc.GetString("modsuit-extract-start", ("item", target)), user, user, PopupType.Medium);
    }

    private void OnDoAfterComplete(Entity<ModularSuitComponent> suit, ref ModularSuitExtractDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used == null)
            return;

        var container = Container.GetContainer(suit, GetContainerForType(args.Type));
        if (!container.ContainedEntities.Contains(args.Used.Value))
            return;

        switch (args.Type)
        {
            case ModularSuitPart.Module:
                if (TryComp<ModularSuitModuleComponent>(args.Used.Value, out var module))
                {
                    if (module.CanBeDisabled) module.IsActive = false;
                    var ev = new ModularSuitRemovedEvent(suit, args.User);
                    RaiseLocalEvent(args.Used.Value, ref ev);
                }
                break;
            case ModularSuitPart.Part:
                if (!RemoveDependentModules(suit, args.Used.Value, args.User))
                    return;

                if (HasComp<PointLightComponent>(args.Used.Value))
                    _light.SetEnabled(args.Used.Value, false);
                break;
        }

        if (Container.Remove(args.Used.Value, container))
        {
            Popup.PopupEntity(Loc.GetString("modsuit-extract-success", ("item", Name(args.Used.Value))), suit, args.User);
            _hands.TryPickupAnyHand(args.User, args.Used.Value);

            Dirty(suit.Owner, suit.Comp);
            CheckSuitAssembly(suit);
            args.Handled = true;

            _audio.PlayPredicted(suit.Comp.EjectSound, suit.Owner, null);

            var tool = _hands.GetActiveItem(args.User);
            if (TryComp<ToolComponent>(tool, out var toolComp))
                _tool.PlayToolSound(tool.Value, toolComp, null);

            UpdateUiState(suit);
        }
    }

    private string GetContainerForType(ModularSuitPart type)
    {
        return type switch
        {
            ModularSuitPart.Module => ModuleContainer,
            ModularSuitPart.Core => CoreContainer,
            ModularSuitPart.Part => PartContainer,
            _ => throw new ArgumentException($"Unknown type: {type}")
        };
    }

    private void OnSuitInteractUsing(Entity<ModularSuitComponent> suit, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;

        // Core
        if (TryComp<ModularSuitCoreComponent>(used, out var core))
        {
            if (_lock.IsLocked(suit.Owner) || !TryComp<WiresPanelComponent>(suit, out var panel) || !panel.Open)
            {
                Popup.PopupEntity(Loc.GetString("modsuit-panel-closed"), suit.Owner, args.User);
                return;
            }

            var container = Container.GetContainer(suit, CoreContainer);
            if (container.ContainedEntities.Count > 0)
            {
                Popup.PopupEntity(Loc.GetString("modsuit-core-slot-occupied"), suit.Owner, args.User);
                return;
            }

            if (Container.Insert(used, container))
            {
                Popup.PopupEntity(Loc.GetString("modsuit-core-installed"), suit.Owner, args.User);
                CheckSuitAssembly(suit.Owner);
                _audio.PlayPredicted(suit.Comp.InsertSound, suit.Owner, null);
                UpdateUiState(suit);
                args.Handled = true;
            }
            return;
        }

        // Part
        if (TryComp<ModularSuitPartComponent>(used, out var part))
        {
            if (_lock.IsLocked(suit.Owner) || !TryComp<WiresPanelComponent>(suit, out var panel) || !panel.Open)
            {
                Popup.PopupEntity(Loc.GetString("modsuit-panel-closed"), suit.Owner, args.User);
                return;
            }

            if (suit.Comp.Equipped)
            {
                Popup.PopupEntity(Loc.GetString("modsuit-cant-install-part-worn"), suit.Owner, args.User);
                return;
            }

            if (TryComp<ModularSuitEquippedComponent>(suit, out var equipped))
            {
                foreach (var (_, partUid) in equipped.EquippedParts)
                {
                    if (TryComp<ModularSuitPartComponent>(partUid, out var existingPart) &&
                        existingPart.PartType == part.PartType)
                    {
                        Popup.PopupEntity(Loc.GetString("modsuit-part-already-installed"), suit.Owner, args.User);
                        return;
                    }
                }
            }

            var container = Container.GetContainer(suit, PartContainer);
            foreach (var existing in container.ContainedEntities)
            {
                if (TryComp<ModularSuitPartComponent>(existing, out var existingPart) &&
                    existingPart.PartType == part.PartType)
                {
                    Popup.PopupEntity(Loc.GetString("modsuit-part-already-installed"), suit.Owner, args.User);
                    return;
                }
            }

            if (Container.Insert(used, container))
            {
                Popup.PopupEntity(Loc.GetString("modsuit-part-installed", ("part", part.PartType.ToString())),
                    suit.Owner, args.User);
                CheckSuitAssembly(suit.Owner);
                _audio.PlayPredicted(suit.Comp.InsertSound, suit.Owner, null);
                Toggle.TryDeactivate(used, args.User, false);
                UpdateUiState(suit);
                args.Handled = true;
            }
            return;
        }

        // Module
        if (TryComp<ModularSuitModuleComponent>(used, out var module))
        {
            if (_lock.IsLocked(suit.Owner) || !TryComp<WiresPanelComponent>(suit, out var panel) || !panel.Open)
            {
                Popup.PopupEntity(Loc.GetString("modsuit-panel-closed"), suit.Owner, args.User);
                return;
            }

            if (module.ModulePart != null && !HasPartInstalled(suit.Owner, module.ModulePart.Value))
            {
                var partName = Loc.GetString($"modsuit-part-{module.ModulePart.Value.ToString().ToLower()}");
                Popup.PopupEntity(Loc.GetString("modsuit-module-requires-part", ("part", partName)), suit.Owner, args.User);
                return;
            }

            args.Handled = TryInstallModule(suit, (used, module), args.User);
        }
    }

    private void OnPreassembledMapInit(Entity<ModularSuitPreassembledComponent> suit, ref MapInitEvent args)
    {
        if (!TryComp<ModularSuitComponent>(suit, out var modularSuit))
            return;

        var moduleContainer = Container.GetContainer(suit, ModuleContainer);
        foreach (var moduleProto in suit.Comp.Modules)
        {
            var moduleUid = Spawn(moduleProto, Transform(suit).Coordinates);
            if (!TryComp<ModularSuitModuleComponent>(moduleUid, out var moduleComp))
            {
                Del(moduleUid);
                continue;
            }

            if (moduleComp.Tags.Count > 0)
            {
                var hasConflict = false;
                var existingModules = GetCurrentModules((suit.Owner, modularSuit));

                foreach (var existing in existingModules)
                {
                    if (TryComp<ModularSuitModuleComponent>(existing, out var existingComp)
                        && existingComp.Tags.Intersect(moduleComp.Tags).Any())
                    {
                        hasConflict = true;
                        break;
                    }
                }

                if (hasConflict)
                {
                    Del(moduleUid);
                    continue;
                }
            }

            if (Container.Insert(moduleUid, moduleContainer))
            {
                var installEvent = new ModularSuitInstalledEvent(suit.Owner, null);
                RaiseLocalEvent(moduleUid, ref installEvent);

                Dirty(moduleUid, moduleComp);
            }
            else
            {
                Del(moduleUid);
            }
        }
    }

    private void OnGetPartVerbs(Entity<ModularSuitPartComponent> part, ref GetVerbsEvent<Verb> args)
    {
        if (!TryComp<AttachedModularSuitPartComponent>(part, out var attached))
            return;

        if (!TryComp<ModularSuitComponent>(attached.Suit, out var suit) || suit.Active)
            return;

        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (!TryComp<ItemToggleComponent>(part, out var itemToggle))
            return;

        var user = args.User;
        var activated = Toggle.IsActivated((part, itemToggle));
        var toggleVerb = new Verb
        {
            Priority = 2,
            Text = activated ? Loc.GetString(itemToggle.VerbToggleOff) : Loc.GetString(itemToggle.VerbToggleOn),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Impact = LogImpact.Low,
            DoContactInteraction = true,
            Act = () => StartPartToggleDoAfter(user, part, !activated)
        };

        args.Verbs.Add(toggleVerb);
    }

    private void StartPartToggleDoAfter(
        EntityUid user,
        Entity<ModularSuitPartComponent> part,
        bool activate,
        bool activateSuit = false,
        bool deactivateSuit = false)
    {
        var delay = part.Comp.ToggleDelay;
        if (TryComp<AffectedModuleSpringlockComponent>(user, out var springlock))
            delay /= springlock.SpeedMultiplier;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay,
            new ModularSuitPartSealDoAfterEvent(activate, activateSuit, deactivateSuit), part, part)
        {
            BreakOnDamage = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private bool TryStartSuitSealing(Entity<ModularSuitComponent> suit, EntityUid user, bool activateSuit = true)
    {
        if (!suit.Comp.Deployed || suit.Comp.Wearer != user ||
            !TryComp<ModularSuitEquippedComponent>(suit, out var equipped))
        {
            return false;
        }

        if (!AllRequiredPartsDeployed(suit))
            return false;

        foreach (var partUid in equipped.EquippedParts.Values)
        {
            if (!TryComp<ModularSuitPartComponent>(partUid, out var part) ||
                !TryComp<ItemToggleComponent>(partUid, out var toggle) ||
                Toggle.IsActivated((partUid, toggle)))
            {
                continue;
            }

            StartPartToggleDoAfter(user, (partUid, part), true, activateSuit: activateSuit);
            return true;
        }

        return false;
    }

    private bool AllRequiredPartsDeployed(EntityUid suit)
    {
        if (!TryComp<ModularSuitEquippedComponent>(suit, out var equipped))
            return false;

        var requiredParts = new HashSet<SuitPartType>
        {
            SuitPartType.Helmet,
            SuitPartType.Torso,
            SuitPartType.Gloves,
            SuitPartType.Boots
        };

        foreach (var partUid in equipped.EquippedParts.Values)
        {
            if (TryComp<ModularSuitPartComponent>(partUid, out var part))
                requiredParts.Remove(part.PartType);
        }

        return requiredParts.Count == 0;
    }

    private bool TryStartSuitUnsealing(Entity<ModularSuitComponent> suit, EntityUid user)
    {
        if (!suit.Comp.Deployed || suit.Comp.Wearer != user ||
            !TryComp<ModularSuitEquippedComponent>(suit, out var equipped))
            return false;

        foreach (var partUid in equipped.EquippedParts.Values)
        {
            if (!TryComp<ModularSuitPartComponent>(partUid, out var part) ||
                !TryComp<ItemToggleComponent>(partUid, out var toggle) ||
                !Toggle.IsActivated((partUid, toggle)))
            {
                continue;
            }

            StartPartToggleDoAfter(user, (partUid, part), false, deactivateSuit: true);
            return true;
        }

        return false;
    }

    private void OnPartDoAfterComplete(Entity<ModularSuitPartComponent> part, ref ModularSuitPartSealDoAfterEvent args)
    {
        if (!TryComp<AttachedModularSuitPartComponent>(part, out var attached)
            || args.Handled || args.Cancelled || attached.Suit == null)
            return;

        if (args.Activate)
        {
            args.Handled = Toggle.TryActivate(part.Owner, args.User, false);
        }
        else
        {
            args.Handled = Toggle.TryDeactivate(part.Owner, args.User, false);
        }

        CheckSuitAssembly(attached.Suit.Value);
        if (!args.Handled)
            return;

        var chains = args.Activate || args.DeactivateSuit;
        if (!chains)
            return;

        if (!TryComp<ModularSuitEquippedComponent>(attached.Suit.Value, out var equipped))
            return;

        foreach (var (_, partUid) in equipped.EquippedParts)
        {
            if (partUid == part.Owner)
                continue;

            if (!TryComp<ModularSuitPartComponent>(partUid, out var nextPart) ||
                !TryComp<ItemToggleComponent>(partUid, out var toggle))
                continue;

            if (Toggle.IsActivated((partUid, toggle)) == args.Activate)
                continue;

            StartPartToggleDoAfter(
                args.User,
                (partUid, nextPart),
                args.Activate,
                args.ActivateSuit,
                args.DeactivateSuit);
            Popup.PopupEntity(
                Loc.GetString(args.Activate ? "modsuit-continue-sealing" : "modsuit-continue-unsealing"),
                args.User,
                args.User);
            return;
        }

        if (!TryComp<ModularSuitComponent>(attached.Suit.Value, out var suitComp))
            return;

        if (args.Activate)
        {
            if ((suitComp.AutoActivateOnAssemble || args.ActivateSuit)
                && !suitComp.Active
                && suitComp.Assembled)
            {
                SetActive((attached.Suit.Value, suitComp), true);
            }
        }
        else if (suitComp.Active)
        {
            SetActive((attached.Suit.Value, suitComp), false);
        }
    }

    private bool TryInstallModule(Entity<ModularSuitComponent> suit, Entity<ModularSuitModuleComponent> module, EntityUid user)
    {
        var container = Container.GetContainer(suit, ModuleContainer);
        if (module.Comp.IsPermanent && container.Contains(module.Owner))
        {
            Popup.PopupEntity(Loc.GetString("modsuit-module-permanent"), suit, user);
            return false;
        }

        if (suit.Comp.BlacklistModules != null && _whitelist.IsWhitelistPass(suit.Comp.BlacklistModules, module.Owner))
        {
            Popup.PopupEntity(Loc.GetString("modsuit-module-blacklist-conflict"), suit, user);
            return false;
        }

        var moduleProtoId = Prototype(module)?.ID;
        var existingModules = GetCurrentModules(suit);
        foreach (var existing in existingModules)
        {
            if (!TryComp<ModularSuitModuleComponent>(existing, out var existingComp))
                continue;

            if (module.Comp.Tags.Count > 0 && existingComp.Tags.Intersect(module.Comp.Tags).Any())
            {
                Popup.PopupEntity(Loc.GetString("modsuit-module-tag-conflict"), suit, user);
                return false;
            }

            if (moduleProtoId != null && moduleProtoId == Prototype(existing)?.ID)
            {
                Popup.PopupEntity(Loc.GetString("modsuit-module-proto-conflict"), suit, user);
                return false;
            }
        }

        if (!Container.Insert(module.Owner, container))
        {
            Popup.PopupEntity(Loc.GetString("modsuit-install-failed"), suit, user);
            return false;
        }

        Popup.PopupEntity(Loc.GetString("modsuit-module-installed"), suit, user);
        _audio.PlayPredicted(suit.Comp.InsertSound, suit.Owner, null);

        var ev = new ModularSuitInstalledEvent(suit, user);
        RaiseLocalEvent(module.Owner, ref ev);

        Dirty(suit.Owner, suit.Comp);
        UpdateUiState(suit);

        return true;
    }

    private List<EntityUid> GetCurrentModules(Entity<ModularSuitComponent> suit)
    {
        if (!Container.TryGetContainer(suit, ModuleContainer, out var container))
            return new List<EntityUid>();

        return container.ContainedEntities.ToList();
    }

    private bool HasPartInstalled(EntityUid suit, SuitPartType partType)
    {
        if (TryComp<ModularSuitEquippedComponent>(suit, out var equipped))
        {
            foreach (var (_, partUid) in equipped.EquippedParts)
            {
                if (TryComp<ModularSuitPartComponent>(partUid, out var partComp) && partComp.PartType == partType)
                    return true;
            }
        }

        var partContainer = Container.GetContainer(suit, PartContainer);
        foreach (var part in partContainer.ContainedEntities)
        {
            if (TryComp<ModularSuitPartComponent>(part, out var partComp) && partComp.PartType == partType)
                return true;
        }

        return false;
    }

    private bool RemoveDependentModules(Entity<ModularSuitComponent> suit, EntityUid removedPart, EntityUid user)
    {
        if (!TryComp<ModularSuitPartComponent>(removedPart, out var partComp))
            return true;

        var moduleContainer = Container.GetContainer(suit, ModuleContainer);
        var modulesToRemove = new List<EntityUid>();

        foreach (var module in moduleContainer.ContainedEntities)
        {
            if (!TryComp<ModularSuitModuleComponent>(module, out var moduleComp) ||
                moduleComp.ModulePart != partComp.PartType)
                continue;

            if (moduleComp.IsPermanent)
            {
                Popup.PopupEntity(Loc.GetString("modsuit-module-permanent"), suit, user);
                return false;
            }

            modulesToRemove.Add(module);
        }

        foreach (var module in modulesToRemove)
        {
            if (Container.Remove(module, moduleContainer))
            {
                var ev = new ModularSuitRemovedEvent(suit, suit.Comp.Wearer ?? suit.Owner);
                RaiseLocalEvent(module, ref ev);
            }
        }

        return true;
    }

    private void CheckSuitAssembly(EntityUid uid)
    {
        if (!TryComp<ModularSuitComponent>(uid, out var suit))
            return;

        var coreContainer = Container.GetContainer(uid, CoreContainer);
        if (coreContainer.ContainedEntities.Count == 0)
        {
            suit.Assembled = false;
            UpdateUiState((uid, suit));
            return;
        }

        if (!TryComp<ModularSuitEquippedComponent>(uid, out var equipped) || equipped.EquippedParts.Count == 0)
        {
            suit.Assembled = false;
            UpdateUiState((uid, suit));
            return;
        }

        var requiredParts = new HashSet<SuitPartType>
        {
            SuitPartType.Helmet,
            SuitPartType.Torso,
            SuitPartType.Gloves,
            SuitPartType.Boots
        };

        var partContainer = Container.GetContainer(uid, PartContainer);
        foreach (var part in partContainer.ContainedEntities)
        {
            if (TryComp<ModularSuitPartComponent>(part, out var partComp))
                requiredParts.Remove(partComp.PartType);
        }

        foreach (var (_, partUid) in equipped.EquippedParts)
        {
            if (TryComp<ModularSuitPartComponent>(partUid, out var partComp))
                requiredParts.Remove(partComp.PartType);
        }

        if (requiredParts.Count > 0)
        {
            suit.Assembled = false;
            UpdateUiState((uid, suit));
            return;
        }

        var allPartsSealed = true;
        foreach (var (_, partUid) in equipped.EquippedParts)
        {
            if (!TryComp<ItemToggleComponent>(partUid, out var toggle) || !Toggle.IsActivated((partUid, toggle)))
            {
                allPartsSealed = false;
                break;
            }
        }

        suit.Assembled = allPartsSealed;

        Dirty(uid, suit);
        UpdateUiState((uid, suit));
    }
}
