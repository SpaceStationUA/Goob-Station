// SPDX-FileCopyrightText: 2025 TyraFox <
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Alert;
using Robust.Server.Player;
using Content.Shared.Roles;
using Content.Server.Mind;
using Content.Server.Actions;
using Content.Server.Silicons.Laws;
using Content.Shared.Mind;
using Content.Server.Objectives;
using Robust.Shared.Random;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Configures selected Malf AIs and checks eligibility for the mid-round event.
/// AntagSelectionSystem owns assignment, including preferences and mid-round timing.
/// </summary>
public sealed class MalfAiRuleSystem : GameRuleSystem<MalfAiRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly Store.Systems.StoreSystem _store = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SiliconLawSystem _siliconLaws = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedRoleSystem _sharedRoleSystem = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    private readonly ISawmill _sawmill = Logger.GetSawmill("malfai");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MalfAiRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
        SubscribeLocalEvent<MalfAiRuleComponent, ObjectivesTextGetInfoEvent>(OnObjectivesTextGetInfo);
        SubscribeLocalEvent<MalfAiCandidateCheckEvent>(OnCandidateCheck);
    }

    private void SeedDefaultLaws(EntityUid rule)
    {
        // Ensure master lawset storage & law container exist on the rule entity.
        var holder = EnsureComp<Content.Shared._Pirate.MalfAI.MalfMasterLawsetComponent>(rule);
        EnsureComp<Content.Shared.Silicons.Laws.Components.SiliconLawBoundComponent>(rule);
        EnsureComp<Content.Shared.Silicons.Laws.Components.SiliconLawProviderComponent>(rule);

        // If empty, seed with defaults so the editor shows contents on first open.
        if (holder.Laws.Count == 0)
        {
            holder.Laws.Add(Loc.GetString("silicon-law-malfai-master-1"));
            holder.Laws.Add(Loc.GetString("silicon-law-malfai-master-2"));
            holder.Laws.Add(Loc.GetString("silicon-law-malfai-master-3"));
        }

        // Initialize the SiliconLawBoundComponent with the laws from MalfMasterLawsetComponent
        // so the law editor can properly read and display them
        var defaultLaws = holder.Laws.Select((law, index) => new Content.Shared.Silicons.Laws.SiliconLaw
        {
            LawString = law,
            Order = index + 1
        }).ToList();

        _siliconLaws.SetLaws(defaultLaws, rule);
    }

    private void OnAfterAntagEntitySelected(Entity<MalfAiRuleComponent> rule, ref AfterAntagEntitySelectedEvent args)
    {
        // Apply malf AI setup to the selected antagonist entity.
        ApplyMalfSetup(args.EntityUid);
    }

    private void ApplyMalfSetup(EntityUid aiEnt)
    {
        // Mark as Malf AI for special interactions
        EnsureComp<Content.Shared._Pirate.MalfAI.MalfAiMarkerComponent>(aiEnt);

        var store = EnsureComp<Content.Shared.Store.Components.StoreComponent>(aiEnt);
        // Configure store minimal settings
        store.Name = "store-preset-name-malfai";

        var requiredCategories = new[] { "All", "MalfAI", "Deception", "Factory", "Disruption" };
        foreach (var category in requiredCategories)
        {
            if (!store.Categories.Contains(category))
                store.Categories.Add(category);
        }

        if (!store.CurrencyWhitelist.Contains("CPU"))
            store.CurrencyWhitelist.Add("CPU");

        // Ensure UI updates and set starting balance to 0 CPU
        _store.TryAddCurrency(new() { { "CPU", Content.Goobstation.Maths.FixedPoint.FixedPoint2.New(0) } }, aiEnt, store);

        // Grant the Open Shop action to the AI entity
        _actions.AddAction(aiEnt, "ActionMalfAiOpenStore");
        // Grant the Borgs management UI action
        _actions.AddAction(aiEnt, "ActionMalfAiOpenBorgsUi");

        EnsureComp<Content.Shared._Pirate.MalfAI.MalfAiCameraUpgradeComponent>(aiEnt);

        // Ensure AlertsComponent exists before showing alert and show CPU alert HUD on the client
        EnsureComp<AlertsComponent>(aiEnt);
        _alerts.ShowAlert(aiEnt, "MalfCpu");
    }





    private bool TryPickUniqueAssassinationTarget(EntityUid aiEnt, HashSet<EntityUid> reserved, out EntityUid picked)
    {
        picked = default;

        // Eligible: crew with a mind, not the AI, and not another Malf AI.
        var candidates = EntityQuery<MindComponent>()
            .Where(m => m.Owner != aiEnt &&
                       !HasComp<Content.Shared._Pirate.MalfAI.MalfAiMarkerComponent>(m.Owner) &&
                       !reserved.Contains(m.Owner))
            .Select(m => m.Owner)
            .ToList();

        if (candidates.Count == 0)
            return false;

        // Use direct random selection since candidates are already filtered
        picked = _random.Pick(candidates);
        return true;
    }

    protected override void Started(EntityUid uid, MalfAiRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Seed the default master lawset on the rule entity so it is available in editors/logic.
        SeedDefaultLaws(uid);
    }

    private void OnCandidateCheck(MalfAiCandidateCheckEvent args)
    {
        var existing = AllEntityQuery<MalfAiMarkerComponent>();
        if (existing.MoveNext(out _, out _))
            return;

        if (!_prototypes.Index<EntityPrototype>("MalfAi").TryGetComponent<AntagSelectionComponent>(
                out var selection, EntityManager.ComponentFactory))
            return;

        var candidates = _players.Sessions.Where(session =>
            session.Status == SessionStatus.InGame &&
            session.AttachedEntity is { } ai &&
            _mobState.IsAlive(ai) && _stationAi.TryGetCore(ai, out _)).ToList();

        // Use the normal role preferences, bans and multi-antag checks without assigning anyone.
        args.Available = selection.Definitions.Any(def =>
            _antag.GetPlayerPool((EntityUid.Invalid, selection), candidates, def).Count > 0);
    }

    private void OnObjectivesTextGetInfo(EntityUid uid, MalfAiRuleComponent component, ref ObjectivesTextGetInfoEvent args)
    {
        args.AgentName = Loc.GetString("malfai-round-end-result");

        var antags = _antag.GetAntagIdentifiers(uid);
        foreach (var (mindId, _, name) in antags)
        {
            args.Minds.Add((mindId, name));
        }
    }

}
