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
using Robust.Shared.Network;

namespace Content.Server._Pirate.ServerCurrency;

/// <summary>Handles sign-on bonuses and early-cryo penalties.</summary>
public sealed class PirateGoobcoinRewardSystem : EntitySystem
{
    [Dependency] private readonly ICommonCurrencyManager _currency = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SandboxSystem _sandbox = default!;

    private readonly HashSet<NetUserId> _signedOn = new();

    private int _roundStartBonus;
    private int _earlyCryoPenalty;
    private float _earlyCryoWindowMinutes;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        // CryostorageSystem owns the direct insertion and mind-removal events.
        // This component starts on pod entry and is available when the mind is removed.
        SubscribeLocalEvent<CryostorageContainedComponent, ComponentStartup>(OnCryoContainedStartup);
        SubscribeLocalEvent<PirateCryoEntryTimeComponent, MindRemovedMessage>(OnCryoMindRemoved);

        Subs.CVar(_cfg, PirateGoobcoinCVars.RoundStartBonus, value => _roundStartBonus = value, true);
        Subs.CVar(_cfg, PirateGoobcoinCVars.EarlyCryoPenalty, value => _earlyCryoPenalty = value, true);
        Subs.CVar(_cfg, PirateGoobcoinCVars.EarlyCryoWindowMinutes, value => _earlyCryoWindowMinutes = value, true);
    }

    public int GetRoundStartBonus(NetUserId userId) => _signedOn.Contains(userId) ? _roundStartBonus : 0;

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.LateJoin || _roundStartBonus <= 0 || _sandbox.IsSandboxEnabled)
            return;

        _signedOn.Add(ev.Player.UserId);
        _chat.DispatchServerMessage(ev.Player,
            Loc.GetString("pirate-goobcoin-signed-on",
                ("amount", _currency.Stringify(_roundStartBonus))));
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _signedOn.Clear();
    }

    private void OnCryoContainedStartup(Entity<CryostorageContainedComponent> ent, ref ComponentStartup args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var entry = EnsureComp<PirateCryoEntryTimeComponent>(ent.Owner);
        entry.RoundTimeOnEntry = _gameTicker.RoundDuration();
        entry.Processed = false;
    }

    private void OnCryoMindRemoved(Entity<PirateCryoEntryTimeComponent> ent, ref MindRemovedMessage args)
    {
        if (_sandbox.IsSandboxEnabled)
            return;

        if (args.Mind.Comp.UserId is not { } userId)
            return;

        // The tracking component outlives a cryo stay.
        if (!HasComp<CryostorageContainedComponent>(ent.Owner))
            return;

        var entry = ent.Comp;
        if (entry.Processed)
            return;

        entry.Processed = true;

        // Payouts use the original owner; messages use the active session.
        var account = args.Mind.Comp.OriginalOwnerUserId ?? userId;
        _players.TryGetSessionById(userId, out var session);

        if (_signedOn.Remove(account) && session != null)
        {
            _chat.DispatchServerMessage(session,
                Loc.GetString("pirate-goobcoin-signed-on-forfeited",
                    ("amount", _currency.Stringify(_roundStartBonus))));
        }

        if (_earlyCryoPenalty <= 0 || _earlyCryoWindowMinutes <= 0f)
            return;

        // Tapering avoids a hard timing threshold.
        var elapsedMinutes = entry.RoundTimeOnEntry.TotalMinutes;
        if (elapsedMinutes >= _earlyCryoWindowMinutes)
            return;

        var scaled = (int) Math.Round(_earlyCryoPenalty * (1d - elapsedMinutes / _earlyCryoWindowMinutes));

        var amount = Math.Min(scaled, _currency.GetBalance(account));
        if (amount <= 0)
            return;

        _currency.RemoveCurrency(account, amount);

        if (session == null)
            return;

        _chat.DispatchServerMessage(session,
            Loc.GetString("pirate-goobcoin-early-cryo-penalty",
                ("amount", _currency.Stringify(amount))));
    }
}
