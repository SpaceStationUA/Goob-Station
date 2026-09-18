// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Shared.Arcade;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Pirate.Server.Arcade;

/// <summary>
///     Pirate WebArcade server half: who holds a cabinet's seat and who is
///     watching. The game itself lives in a player's own CEF browser — the
///     server only relays frames captured by the seated client to that
///     cabinet's spectators (log n fans), never more: capture and decode
///     stay on the participating clients. Keeps tickrate untouched.
/// </summary>
public sealed class PirateArcadeSystem : EntitySystem
{
    /// <summary>Spectators per cabinet.</summary>
    private const int MaxSpectators = 5;

    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private sealed class Session
    {
        public EntityUid Cab;
        public NetUserId? Seated;
        public string PlayerName = "";
        public readonly HashSet<INetChannel> Spectators = new();
    }

    private readonly Dictionary<EntityUid, Session> _sessions = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateArcadeSeatEvent>(OnSeat);
        SubscribeNetworkEvent<PirateArcadeWatchEvent>(OnWatch);
        SubscribeNetworkEvent<PirateArcadeFrameEvent>(OnFrame);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    // ===== client → server =====

    private void OnSeat(PirateArcadeSeatEvent msg, EntitySessionEventArgs args)
    {
        var ust = _players.GetSessionByChannel(args.SenderSession.Channel);
        var cab = EntityManager.GetEntity(msg.Cab);
        if (msg.Seat)
            TrySeat(cab, ust);
        else
            TryUnseat(cab, ust);
    }

    private void OnWatch(PirateArcadeWatchEvent msg, EntitySessionEventArgs args)
    {
        var ust = _players.GetSessionByChannel(args.SenderSession.Channel);
        var cab = EntityManager.GetEntity(msg.Cab);
        if (!_sessions.TryGetValue(cab, out var ses))
        {
            ses = _sessions[cab] = new Session { Cab = cab };
        }

        bool changed = false;
        if (msg.Watch)
        {
            if (!ses.Spectators.Add(ust.Channel))
                return;
            if (ses.Spectators.Count > MaxSpectators)
            {
                ses.Spectators.Remove(ust.Channel);
                _popup.PopupEntity("Спостерігачі зайняті на цьому автоматі (максимум 5)", cab,
                    Filter.SinglePlayer(ust), false, PopupType.Small);
            }
            else
            {
                changed = true;
                _popup.PopupEntity("лишилися спостерігати автомат", cab,
                    Filter.SinglePlayer(ust), false, PopupType.Small);
            }
        }
        else if (ses.Spectators.Remove(ust.Channel))
        {
            changed = true;
        }

        if (changed)
            Broadcast(ses);
    }

    private void OnFrame(PirateArcadeFrameEvent msg, EntitySessionEventArgs args)
    {
        // Only the seated session of that same cabinet may push frames; the
        // decode (render) happens on each spectator's browser, the capture
        // on the player's — the server stays a dumb relay here.
        var ust = _players.GetSessionByChannel(args.SenderSession.Channel);
        var cab = EntityManager.GetEntity(msg.Cab);
        if (!_sessions.TryGetValue(cab, out var ses) || ses.Seated != ust.UserId)
            return;
        if (ses.Spectators.Count == 0)
            return;

        foreach (var ch in ses.Spectators)
            RaiseNetworkEvent(new PirateArcadeFrameEvent { Cab = msg.Cab, Frame = msg.Frame }, ch);
    }

    // ===== core =====

    private void TrySeat(EntityUid cab, ICommonSession user)
    {
        if (!_sessions.TryGetValue(cab, out var ses))
            ses = _sessions[cab] = new Session { Cab = cab };

        if (ses.Seated != null && ses.Seated != user.UserId)
        {
            // Already seated by someone else: the opener flips to spectator.
            Broadcast(ses);
            return;
        }

        if (ses.Seated == user.UserId)
        {
            Broadcast(ses); // refresh
            return;
        }

        ses.Seated = user.UserId;
        ses.PlayerName = user.Name;
        Broadcast(ses);

        var ent = AttachedEntity(user);
        if (ent != null)
            _popup.PopupEntity("сів за автомат", ent.Value, Filter.Pvs(ent.Value), false, PopupType.Medium);
    }

    private void TryUnseat(EntityUid cab, ICommonSession user)
    {
        if (!_sessions.TryGetValue(cab, out var ses) || ses.Seated != user.UserId)
            return;
        ses.Seated = null;
        ses.PlayerName = "";
        Broadcast(ses);

        var ent = AttachedEntity(user);
        if (ent != null)
            _popup.PopupEntity("встав з автомата", ent.Value, Filter.Pvs(ent.Value), false, PopupType.Medium);
    }

    private void Broadcast(Session ses)
    {
        RaiseNetworkEvent(new PirateArcadeStateEvent
        {
            Cab = EntityManager.GetNetEntity(ses.Cab),
            Taken = ses.Seated != null,
            PlayerName = ses.PlayerName,
        });
    }

    private EntityUid? AttachedEntity(ICommonSession user)
    {
        return user.AttachedEntity;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        // Dropping a session frees any seat it held.
        if (e.NewStatus != SessionStatus.Disconnected)
            return;
        var toRemove = new List<EntityUid>();
        foreach (var (cab, ses) in _sessions)
        {
            var touched = false;
            if (ses.Seated == e.Session.UserId)
            {
                ses.Seated = null;
                ses.PlayerName = "";
                touched = true;
            }
            if (ses.Spectators.Remove(e.Session.Channel))
                touched = true;
            if (touched)
            {
                toRemove.Add(cab);
                Broadcast(ses);
            }
        }
        // Clean out fully idle sessions to avoid unbounded dict growth.
        foreach (var cab in toRemove)
            if (_sessions[cab].Seated == null && _sessions[cab].Spectators.Count == 0)
                _sessions.Remove(cab);
    }
}
