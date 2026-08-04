// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.ServerCurrency;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Sandbox;
using Content.Shared._Pirate.CCVars;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;

namespace Content.Pirate.Server.ServerCurrency;

/// <summary>Handles round-start bonuses and early-cryo penalties.</summary>
public sealed class PirateGoobcoinRewardSystem : EntitySystem
{
    [Dependency] private readonly ICommonCurrencyManager _currency = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SandboxSystem _sandbox = default!;

    private int _roundStartBonus;
    private int _earlyCryoPenalty;
    private float _earlyCryoWindowMinutes;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        // CryostorageSystem owns the direct insertion and mind-removal events.
        // This component starts on pod entry and is available when the mind is removed.
        SubscribeLocalEvent<CryostorageContainedComponent, ComponentStartup>(OnCryoContainedStartup);
        SubscribeLocalEvent<PirateCryoEntryTimeComponent, MindRemovedMessage>(OnCryoMindRemoved);

        Subs.CVar(_cfg, PirateGoobcoinCVars.RoundStartBonus, value => _roundStartBonus = value, true);
        Subs.CVar(_cfg, PirateGoobcoinCVars.EarlyCryoPenalty, value => _earlyCryoPenalty = value, true);
        Subs.CVar(_cfg, PirateGoobcoinCVars.EarlyCryoWindowMinutes, value => _earlyCryoWindowMinutes = value, true);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.LateJoin || _roundStartBonus <= 0 || _sandbox.IsSandboxEnabled)
            return;

        _currency.AddCurrency(ev.Player.UserId, _roundStartBonus);
        _chat.DispatchServerMessage(ev.Player,
            Loc.GetString("pirate-goobcoin-round-start-bonus",
                ("amount", _currency.Stringify(_roundStartBonus))));
    }

    private void OnCryoContainedStartup(Entity<CryostorageContainedComponent> ent, ref ComponentStartup args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var entry = EnsureComp<PirateCryoEntryTimeComponent>(ent.Owner);
        entry.RoundTimeOnEntry = _gameTicker.RoundDuration();
        entry.Charged = false;
    }

    private void OnCryoMindRemoved(Entity<PirateCryoEntryTimeComponent> ent, ref MindRemovedMessage args)
    {
        if (_earlyCryoPenalty <= 0 || _sandbox.IsSandboxEnabled)
            return;

        if (args.Mind.Comp.UserId is not { } userId)
            return;

        // The tracking component outlives a cryo stay.
        if (!HasComp<CryostorageContainedComponent>(ent.Owner))
            return;

        var entry = ent.Comp;
        if (entry.Charged || entry.RoundTimeOnEntry.TotalMinutes > _earlyCryoWindowMinutes)
            return;

        entry.Charged = true;

        var amount = Math.Min(_earlyCryoPenalty, _currency.GetBalance(userId));
        if (amount <= 0)
            return;

        _currency.RemoveCurrency(userId, amount);

        if (!_players.TryGetSessionById(userId, out var session))
            return;

        _chat.DispatchServerMessage(session,
            Loc.GetString("pirate-goobcoin-early-cryo-penalty",
                ("amount", _currency.Stringify(amount)),
                ("minutes", _earlyCryoWindowMinutes)));
    }
}
