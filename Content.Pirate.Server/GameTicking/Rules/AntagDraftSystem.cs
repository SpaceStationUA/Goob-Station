// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Shared.StationEvents;
using Content.Pirate.Server.GameTicking.Rules.Components;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents;
using Content.Shared.GameTicking.Components;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.GameTicking.Rules;

public sealed partial class AntagDraftSystem : GameRuleSystem<AntagDraftComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private EventManagerSystem _event = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    protected override void Added(EntityUid uid, AntagDraftComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        var players = _antag.GetTotalPlayerCount(_players.Sessions) + _event.PlayerCountBias;
        var plan = AntagDraftPlanner.Plan(component, players, MinPlayers, Incompatible, RobustRandom.NextDouble);

        foreach (var pick in plan.Picks)
            AddDrafted(pick);

        var picks = plan.Picks.Count == 0
            ? "none"
            : string.Join(", ", plan.Picks.Select(p => p.Major ? $"{p.Rule} (major)" : $"{p.Rule} x{p.Antags}"));
        var message = $"Antag draft: {plan.Players} players, 1 antag per {plan.PlayersPerAntag:0.0}, target {plan.Target}: {picks}";
        Log.Info(message);
        ChatManager.SendAdminAnnouncement(message);
    }

    private void AddDrafted(AntagDraftPick pick)
    {
        var ruleUid = GameTicker.AddGameRule(pick.Rule);

        // Eligibility was checked against connected players before adding the rule.
        if (TryComp<GameRuleComponent>(ruleUid, out var rule))
            rule.CancelPresetOnTooFewPlayers = false;

        if (pick.Major || !TryComp<AntagSelectionComponent>(ruleUid, out var selection) || selection.Definitions.Count == 0)
            return;

        // Replace the rule's random count with the drafted count.
        var def = selection.Definitions[0];
        def.Min = def.Max = pick.Antags;
        selection.Definitions[0] = def;
    }

    private int MinPlayers(string rule)
    {
        return _proto.TryIndex<EntityPrototype>(rule, out var proto)
               && proto.TryComp<GameRuleComponent>(out var gameRule, EntityManager.ComponentFactory)
            ? gameRule.MinPlayers
            : int.MaxValue;
    }

    private IEnumerable<string>? Incompatible(string rule)
    {
        return _proto.TryIndex<IncompatibleGameModesPrototype>(rule, out var modes) ? modes.Modes : null;
    }
}
