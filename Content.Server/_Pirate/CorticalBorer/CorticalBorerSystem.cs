// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 Coenx-flex
// SPDX-FileCopyrightText: 2025 Cojoke
// SPDX-FileCopyrightText: 2025 Ilya246
// SPDX-FileCopyrightText: 2025 ScyronX
// SPDX-FileCopyrightText: 2025 ark1368
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Shared._Pirate.CorticalBorer;
using Content.Shared._Starlight.CollectiveMind;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Goobstation.Shared.Overlays;
using Content.Shared.Inventory;
using Content.Shared.MedicalScanner;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Species.Components;
using Content.Shared.Temperature;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.CorticalBorer;

public sealed partial class CorticalBorerSystem : SharedCorticalBorerSystem
{
    private static readonly EntProtoId MindHolderPrototype = "FoodMeatFish";
    private static readonly EntProtoId EndControlAction = "ActionEndControlHost";
    private static readonly EntProtoId LayEggAction = "ActionLayEggHost";
    private static readonly HashSet<int> ValidInjectAmounts = [1, 5, 10, 15, 20, 25, 30, 50, 100];

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly HealthAnalyzerSystem _analyzer = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly ISharedAdminLogManager _admin = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly GhostRoleSystem _ghost = default!;
    [Dependency] private readonly CollectiveMindUpdateSystem _collective = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAbilities();
        SubscribeVision();
        SubscribeLocalEvent<CorticalBorerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalBorerDispenserInjectMessage>(OnInjectReagentMessage);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalBorerDispenserSetInjectAmountMessage>(OnSetInjectAmountMessage);
        SubscribeLocalEvent<InventoryComponent, InfestHostAttempt>(OnInfestHostAttempt);
        SubscribeLocalEvent<CorticalBorerComponent, CheckTargetedSpeechEvent>(OnTargetedSpeech);
        SubscribeLocalEvent<CorticalBorerComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<CorticalBorerComponent, ModifyChangedTemperatureEvent>(OnTemperatureChange);
        SubscribeLocalEvent<CorticalBorerComponent, TryIgniteEvent>(OnIgniteAttempt);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalBorerEjectingEvent>(OnEjecting);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalBorerEjectedEvent>(OnEjected);
    }

    private void OnStartup(Entity<CorticalBorerComponent> ent, ref ComponentStartup args)
    {
        foreach (var actionId in ent.Comp.InitialCorticalBorerActions)
            Actions.AddAction(ent, actionId);

        EnsureThermalVisionAction(ent);

        _alerts.ShowAlert(ent.Owner, ent.Comp.ChemicalAlert);
        UpdateUiState(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var component in EntityManager.EntityQuery<CorticalBorerComponent>())
        {
            if (_timing.CurTime < component.UpdateTimer)
                continue;

            component.UpdateTimer = _timing.CurTime + TimeSpan.FromSeconds(component.UpdateCooldown);
            if (component.Host is not null)
                UpdateChems((component.Owner, component), component.ChemicalGenerationRate);
        }

        foreach (var component in EntityManager.EntityQuery<CorticalBorerInfestedComponent>())
        {
            if (_timing.CurTime >= component.ControlTimeEnd)
                EndControl(component.Borer);
        }
    }

    private void OnTargetedSpeech(Entity<CorticalBorerComponent> ent, ref CheckTargetedSpeechEvent args)
    {
        args.ChatTypeIgnore.Add(InGameICChatType.CollectiveMind);

        if (ent.Comp.Host is not { } host)
            return;

        args.Targets.Add(ent);
        args.Targets.Add(host);
    }

    public void UpdateChems(Entity<CorticalBorerComponent> ent, int change)
    {
        ent.Comp.ChemicalPoints = Math.Clamp(ent.Comp.ChemicalPoints + change, 0, ent.Comp.ChemicalPointCap);

        if (ent.Comp.ChemicalPoints % ent.Comp.UiUpdateInterval == 0)
            UpdateUiState(ent);

        _alerts.ShowAlert(ent.Owner, ent.Comp.ChemicalAlert);
        Dirty(ent);
    }

    private void OnInfestHostAttempt(Entity<InventoryComponent> ent, ref InfestHostAttempt args)
    {
        if (!_inventory.TryGetSlotEntity(ent, "head", out var head) ||
            !TryComp<IngestionBlockerComponent>(head, out var blocker) ||
            !blocker.Enabled)
        {
            return;
        }

        args.Blocker = head;
        args.Cancel();
    }

    public bool TryInjectHost(Entity<CorticalBorerComponent> ent,
        CorticalBorerChemicalPrototype chemical,
        int amount)
    {
        var (uid, comp) = ent;

        if (comp.Host is not { } host)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-no-host"), uid, uid, PopupType.Medium);
            return false;
        }

        if (amount <= 0 || !CanUseAbility(ent, host))
            return false;

        var totalCost = amount * chemical.Cost;
        if (totalCost > comp.ChemicalPointCap)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-not-enough-chem-storage"), uid, uid, PopupType.Medium);
            return false;
        }

        if (totalCost > comp.ChemicalPoints)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-not-enough-chem"), uid, uid, PopupType.Medium);
            return false;
        }

        if (!TryComp<BloodstreamComponent>(host, out var bloodstream))
            return false;

        var solution = new Solution();
        solution.AddReagent(chemical.Reagent, amount);
        if (!_blood.TryAddToBloodstream((host, bloodstream), solution))
            return false;

        UpdateChems(ent, -totalCost);
        return true;
    }

    private void OnInjectReagentMessage(Entity<CorticalBorerComponent> ent,
        ref CorticalBorerDispenserInjectMessage message)
    {
        foreach (var chemical in _prototypes.EnumeratePrototypes<CorticalBorerChemicalPrototype>())
        {
            if (chemical.Reagent != message.ChemProtoId)
                continue;

            TryInjectHost(ent, chemical, ent.Comp.InjectAmount);
            break;
        }

        UpdateUiState(ent);
    }

    private void OnSetInjectAmountMessage(Entity<CorticalBorerComponent> ent,
        ref CorticalBorerDispenserSetInjectAmountMessage message)
    {
        if (!ValidInjectAmounts.Contains(message.CorticalBorerDispenserDispenseAmount))
            return;

        ent.Comp.InjectAmount = message.CorticalBorerDispenserDispenseAmount;
        UpdateUiState(ent);
    }

    private List<CorticalBorerDispenserItem> GetAllBorerChemicals(Entity<CorticalBorerComponent> ent)
    {
        var chemicals = new List<CorticalBorerDispenserItem>();
        foreach (var chemical in _prototypes.EnumeratePrototypes<CorticalBorerChemicalPrototype>())
        {
            if (!_prototypes.TryIndex(chemical.Reagent, out ReagentPrototype? reagent))
                continue;

            chemicals.Add(new CorticalBorerDispenserItem(
                reagent.LocalizedName,
                reagent.ID,
                chemical.Cost,
                ent.Comp.InjectAmount,
                ent.Comp.ChemicalPoints,
                reagent.SubstanceColor));
        }

        return chemicals;
    }

    private void UpdateUiState(Entity<CorticalBorerComponent> ent)
    {
        var state = new CorticalBorerDispenserBoundUserInterfaceState(GetAllBorerChemicals(ent), ent.Comp.InjectAmount);
        _userInterface.SetUiState(ent.Owner, CorticalBorerDispenserUiKey.Key, state);
    }

    public bool TryToggleCheckBlood(Entity<CorticalBorerComponent> ent)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var userInterface) ||
            !HasComp<HealthAnalyzerComponent>(ent))
        {
            return false;
        }

        if (Ui.IsUiOpen((ent, userInterface), HealthAnalyzerUiKey.Key))
            CloseCheckBlood(ent, userInterface);
        else if (ent.Comp.Host.HasValue)
            OpenCheckBlood(ent, userInterface);

        return true;
    }

    public void OpenCheckBlood(Entity<CorticalBorerComponent> ent, UserInterfaceComponent userInterface)
    {
        if (ent.Comp.Host is not { } host || !TryComp<HealthAnalyzerComponent>(ent, out var healthAnalyzer))
            return;

        if (!Ui.IsUiOpen((ent, userInterface), HealthAnalyzerUiKey.Key))
            Ui.OpenUi((ent, userInterface), HealthAnalyzerUiKey.Key, ent);

        _analyzer.BeginAnalyzingEntity((ent, healthAnalyzer), host);
    }

    public void CloseCheckBlood(Entity<CorticalBorerComponent> ent, UserInterfaceComponent userInterface)
    {
        if (!TryComp<HealthAnalyzerComponent>(ent, out var healthAnalyzer) ||
            healthAnalyzer.ScannedEntity is not { } scanned)
        {
            return;
        }

        Ui.CloseUi((ent, userInterface), HealthAnalyzerUiKey.Key, ent);
        _analyzer.StopAnalyzingEntity((ent, healthAnalyzer), scanned);
    }

    public void TakeControlHost(Entity<CorticalBorerComponent> ent, CorticalBorerInfestedComponent infested)
    {
        var (worm, comp) = ent;
        if (comp.Host is not { } host ||
            TryComp<MobStateComponent>(host, out var mobState) && mobState.CurrentState == MobState.Dead ||
            !_mind.TryGetMind(worm, out var wormMind, out _))
        {
            return;
        }

        if (TryComp<MindContainerComponent>(host, out var mindContainer) && mindContainer.HasMind ||
            HasComp<GhostRoleComponent>(host))
        {
            infested.ControlTimeEnd = _timing.CurTime + comp.ControlDuration;
        }

        infested.BorerMindId = wormMind;

        if (_mind.TryGetMind(host, out var controlledMind, out _))
        {
            infested.OriginalMindId = controlledMind;
            var dummy = Spawn(MindHolderPrototype, MapCoordinates.Nullspace);
            EnsureComp<CorticalBorerControlMindHolderComponent>(dummy);
            Container.Insert(dummy, infested.ControlContainer);
            _mind.TransferTo(controlledMind, dummy);
        }
        else
        {
            infested.OriginalMindId = null;
        }

        // Pirate: mark control only after moving the host's original mind. The move raises MindRemovedMessage,
        // which must not be treated as an unexpected loss of the host's mind during takeover.
        comp.ControlingHost = true;
        AddControlThermalVision(worm, host, infested);
        _mind.TransferTo(wormMind, host);

        if (TryComp<GhostRoleComponent>(worm, out var ghostRole))
            _ghost.UnregisterGhostRole((worm, ghostRole));

        if (Actions.AddAction(host, EndControlAction) is { } endControl)
            infested.RemoveAbilities.Add(endControl);

        if (comp.CanReproduce && !comp.HasLaidEgg &&
            Actions.AddAction(host, LayEggAction) is { } layEgg)
        {
            infested.LayEggAction = layEgg;
            infested.RemoveAbilities.Add(layEgg);
        }

        if (TryComp<ReformComponent>(host, out var reform) && reform.ActionEntity is { } reformAction)
        {
            infested.RemovedReformAction = reformAction;
            Actions.RemoveAction(host, reformAction);
        }

        var collective = EnsureComp<CollectiveMindComponent>(host);
        var channel = comp.HivemindChannel;
        infested.HadHivemind = collective.Channels.Contains(channel);
        infested.OldDefault = collective.DefaultChannel;
        collective.Channels.Add(channel);
        collective.DefaultChannel = channel;
        _collective.UpdateCollectiveMind(host, collective);
        Dirty(host, collective);

        var logMessage = $"{ToPrettyString(worm)} has taken control over {ToPrettyString(host)}";
        Log.Info(logMessage);
        _admin.Add(LogType.Mind, LogImpact.High, $"{logMessage}");
        _chat.SendAdminAlert(logMessage);
    }

    public void EndControl(Entity<CorticalBorerComponent> worm, EntityUid? originalMindTarget = null)
    {
        var (uid, comp) = worm;
        if (comp.Host is not { } host ||
            !TryComp<CorticalBorerInfestedComponent>(host, out var infested) ||
            !comp.ControlingHost)
        {
            return;
        }

        comp.ControlingHost = false;

        RemoveControlThermalVision(host, infested);

        foreach (var ability in infested.RemoveAbilities)
            Actions.RemoveAction(host, ability);
        infested.RemoveAbilities.Clear();
        infested.LayEggAction = null;

        if (infested.RemovedReformAction is not null && TryComp<ReformComponent>(host, out var reform))
        {
            reform.ActionEntity = Actions.AddAction(host, reform.ActionPrototype);
            infested.RemovedReformAction = null;
        }

        if (TryComp<GhostRoleComponent>(uid, out var ghostRole))
            _ghost.RegisterGhostRole((uid, ghostRole));

        if (!TerminatingOrDeleted(infested.BorerMindId))
            _mind.TransferTo(infested.BorerMindId, infested.Borer);

        var mindTarget = originalMindTarget ?? host;
        if (infested.OriginalMindId is { } originalMind &&
            !TerminatingOrDeleted(originalMind) &&
            !TerminatingOrDeleted(mindTarget))
        {
            _mind.TransferTo(originalMind, mindTarget);
        }

        if (TryComp<CollectiveMindComponent>(host, out var collective))
        {
            if (!infested.HadHivemind)
                collective.Channels.Remove(comp.HivemindChannel);

            collective.DefaultChannel = infested.OldDefault;
            _collective.UpdateCollectiveMind(host, collective);
            Dirty(host, collective);
        }

        infested.ControlTimeEnd = null;
        Container.CleanContainer(infested.ControlContainer);
    }

    private void EnsureThermalVisionAction(Entity<CorticalBorerComponent> ent)
    {
        if (!TryComp<ThermalVisionComponent>(ent, out var thermal) ||
            thermal.ToggleAction is not { } toggleAction ||
            thermal.ToggleActionEntity is not null)
        {
            return;
        }

        Actions.AddAction(ent, ref thermal.ToggleActionEntity, toggleAction);
    }

    private void AddControlThermalVision(EntityUid worm,
        EntityUid host,
        CorticalBorerInfestedComponent infested)
    {
        ThermalVisionComponent thermal;
        if (TryComp<ThermalVisionComponent>(host, out var existingThermal))
        {
            if (!infested.AddedBorerThermalVision)
                return;

            thermal = existingThermal;
        }
        else
        {
            thermal = EnsureComp<ThermalVisionComponent>(host);
        }

        infested.AddedControlThermalVision = true;
        if (TryComp<ThermalVisionComponent>(worm, out var borerThermal))
        {
            thermal.Color = borerThermal.Color;
            thermal.LightRadius = borerThermal.LightRadius;
            thermal.ThermalShader = borerThermal.ThermalShader;
            thermal.DrawOverlay = borerThermal.DrawOverlay;
            thermal.OverlayOpacity = borerThermal.OverlayOpacity;
            thermal.ToggleAction = borerThermal.ToggleAction;
        }

        thermal.IsEquipment = false;
        thermal.IsActive = true;
        thermal.ActivateSound = null;
        thermal.DeactivateSound = null;
        if (thermal.ToggleAction is { } toggleAction)
        {
            Actions.AddAction(host, ref thermal.ToggleActionEntity, toggleAction);
            Actions.SetToggled(thermal.ToggleActionEntity, true);
        }
        Dirty(host, thermal);
    }

    private void RemoveControlThermalVision(EntityUid host, CorticalBorerInfestedComponent infested)
    {
        if (!infested.AddedControlThermalVision)
            return;

        infested.AddedControlThermalVision = false;

        if (infested.AddedBorerThermalVision &&
            TryComp<ThermalVisionComponent>(infested.Borer, out var borerThermal) &&
            TryComp<ThermalVisionComponent>(host, out var hostThermal))
        {
            ConfigureGrantedVision(host, hostThermal, borerThermal);
            SetVisionActive(host, hostThermal, true);
            return;
        }

        RemCompDeferred<ThermalVisionComponent>(host);
    }

    private void OnMindRemoved(Entity<CorticalBorerComponent> ent, ref MindRemovedMessage args)
    {
        if (!ent.Comp.ControlingHost)
            TryEjectBorer(ent);
    }

    private void OnTemperatureChange(Entity<CorticalBorerComponent> ent, ref ModifyChangedTemperatureEvent args)
    {
        if (ent.Comp.Host.HasValue)
            args.TemperatureDelta = 0;
    }

    private void OnIgniteAttempt(Entity<CorticalBorerComponent> ent, ref TryIgniteEvent args)
    {
        // A borer inside a host cannot meaningfully burn before it is ejected.
        args.Cancelled = ent.Comp.Host.HasValue;
    }

    private void OnEjecting(Entity<CorticalBorerComponent> ent, ref CorticalBorerEjectingEvent args)
    {
        if (TryComp<UserInterfaceComponent>(ent, out var userInterface))
            CloseCheckBlood(ent, userInterface);

        if (ent.Comp.ControlingHost)
            EndControl(ent);
    }

    private void OnEjected(Entity<CorticalBorerComponent> ent, ref CorticalBorerEjectedEvent args)
    {
        if (TryComp<CorticalBorerInfestedComponent>(args.Host, out var infested) &&
            infested.Borer.Owner == ent.Owner)
        {
            ClearHostVision(args.Host, infested);
        }
    }
}
