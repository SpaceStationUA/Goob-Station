// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Shared.Radio;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Pirate.Client.Radio;

/// <summary>
///     PID radio client half: routes server catalog/state pushes into
///     <see cref="PirateRadioClientState"/>, and each frame pumps the TUI
///     bridge for open radio UIs plus pushes them the current catalog and
///     selected state. UIFragments have no FrameUpdate, so the fragments
///     register their drivers here and this system drives them.
/// </summary>
public sealed class PirateRadioClientSystem : EntitySystem
{
    private sealed class OpenRadio
    {
        public required WebRadioDriver Driver;
        public required NetEntity Marker;
        public required Func<bool> Alive;
    }

    private readonly List<OpenRadio> _open = new();

    [Dependency] private readonly INetManager _net = default!;

    private bool _requestedCatalog;
    private NetEntity _requestMarker = NetEntity.Invalid;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateRadioCatalogEvent>(OnCatalog);
        SubscribeNetworkEvent<PirateRadioStateEvent>(OnState);
    }

    private void OnCatalog(PirateRadioCatalogEvent msg, EntitySessionEventArgs _)
        => PirateRadioClientState.OnCatalog(msg);

    private void OnState(PirateRadioStateEvent msg, EntitySessionEventArgs _)
        => PirateRadioClientState.OnState(msg);

    /// <summary>Register an open radio UI's driver for per-frame driving.</summary>
    public void Register(WebRadioDriver driver, NetEntity marker, Func<bool> alive)
        => _open.Add(new OpenRadio { Driver = driver, Marker = marker, Alive = alive });

    public void Unregister(WebRadioDriver driver)
    {
        _open.RemoveAll(o => o.Driver == driver);
        if (_open.Count == 0)
        {
            _requestMarker = NetEntity.Invalid;
            _requestedCatalog = false;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_open.Count == 0)
            return;

        // Drop drivers whose program entity is gone.
        for (var i = _open.Count - 1; i >= 0; i--)
        {
            if (!_open[i].Alive())
                _open.RemoveAt(i);
        }
        if (_open.Count == 0)
        {
            _requestMarker = NetEntity.Invalid;
            _requestedCatalog = false;
            return;
        }

        // Ask for the catalog once per open program.
        if (_open[0].Marker != _requestMarker)
        {
            _requestMarker = _open[0].Marker;
            _requestedCatalog = false;
        }
        if (!_requestedCatalog && _requestMarker.IsValid())
        {
            _requestedCatalog = true;
            PirateRadioClientState.RequestCatalog(_requestMarker);
        }

        foreach (var open in _open)
        {
            open.Driver.Ipc.Pump();

            var catalog = PirateRadioClientState.Catalog(open.Marker);
            if (catalog != null)
                open.Driver.SetCatalog(ToTuples(catalog));

            var state = PirateRadioClientState.Get(open.Marker);
            open.Driver.SetState(state.StationId, state.Playing);
        }
    }

    private static List<(string, string, string, string, bool)> ToTuples(
        IReadOnlyList<PirateRadioStationEntry> catalog)
    {
        var list = new List<(string, string, string, string, bool)>(catalog.Count);
        foreach (var s in catalog)
            list.Add((s.Id, s.Label, s.Genre, s.Url, s.Featured));
        return list;
    }
}
