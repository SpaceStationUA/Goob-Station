// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Shared state hub for the Pirate WebArcade cabinets — the client
///     mirror of the server's seat state, keyed by cabinet NetEntity (the
///     server broadcasts PirateArcadeStateEvent globally on change).
/// </summary>
public static class WebArcadeBackend
{
    public sealed class CabState
    {
        public bool Taken;
        public string PlayerName = "";
    }

    private sealed class Receiver
    {
        public readonly Action<CabState> Hook;
        public Receiver(Action<CabState> hook) => Hook = hook;
    }

    private static readonly Dictionary<int, List<Receiver>> _subs = new();
    private static readonly Dictionary<int, CabState> _states = new();
    private static readonly Dictionary<int, List<Action<string>>> _frames = new();

    private static int Key(NetEntity cab) => cab.GetHashCode();

    /// <summary>Subscribes a per-cabinet frame hook (spectator viewers);
    /// returns an unsubscriber.</summary>
    public static Action SubscribeFrames(NetEntity cab, Action<string> receiver)
    {
        var key = Key(cab);
        lock (_states)
        {
            if (!_frames.TryGetValue(key, out var hooks))
                _frames[key] = hooks = new List<Action<string>>();
            hooks.Add(receiver);
            return () => { lock (_states) hooks.Remove(receiver); };
        }
    }

    /// <summary>Relayed frame from the seated player's capture hook.</summary>
    public static void DispatchFrame(NetEntity cab, string frame)
    {
        lock (_states)
            if (_frames.TryGetValue(Key(cab), out var hooks))
                foreach (var h in hooks.ToArray())
                    h(frame);
    }

    /// <summary>Subscribes a per-cabinet receiver; returns an unsubscriber.</summary>
    public static Action Subscribe(NetEntity cab, Action<CabState> receiver)
    {
        var key = Key(cab);
        var r = new Receiver(receiver);
        if (!_subs.TryGetValue(key, out var list))
            _subs[key] = list = new List<Receiver>();
        list.Add(r);
        return () => { lock (_states) list.Remove(r); };
    }

    /// <summary>Mutates the state for a cabinet and fans the updates out.</summary>
    public static void Apply(NetEntity cab, Action<CabState> mutate)
    {
        lock (_states)
        {
            if (!_states.TryGetValue(Key(cab), out var st))
                _states[Key(cab)] = st = new CabState();
            mutate(st);
            if (_subs.TryGetValue(Key(cab), out var list))
                foreach (var r in list.ToArray())
                    r.Hook(st);
        }
    }

    /// <summary>Instant view (initial render / verb logic).</summary>
    public static CabState Snapshot(NetEntity cab)
    {
        lock (_states)
        {
            if (_states.TryGetValue(Key(cab), out var s))
                return s;
            var fresh = new CabState();
            _states[Key(cab)] = fresh;
            return fresh;
        }
    }
}
