// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Client._Pirate.WebUI;
using Content.Pirate.Shared.Radio;
using Robust.Client.Player;
using Robust.Client.WebView;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Pirate.Client.Radio;

/// <summary>
///     PID radio client half. Owns the persistent playback webview: the PDA
///     program fragment only hosts the control, so closing the program (or
///     the whole PDA UI) does not dispose the CEF browser and the audio
///     keeps playing. Playback is stopped when the PDA carrying the radio
///     program is no longer on the local player.
/// </summary>
public sealed class PirateRadioClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private WebViewControl? _view;
    private WebUiTuiIpc? _ipc;
    private WebRadioDriver? _driver;
    private NetEntity _marker = NetEntity.Invalid;
    private bool _requestedCatalog;
    private bool _uncarriedStopSent;

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

    /// <summary>
    ///     Returns the persistent playback control, creating it on first use
    ///     (or rebuilding it if the radio program entity changed). The
    ///     fragment adds this to its own tree; a later fragment disposal must
    ///     detach (not dispose) it.
    /// </summary>
    public WebViewControl? EnsurePlayback(NetEntity marker)
    {
        if (_view != null && _view.Disposed)
            Teardown();
        if (_view != null && _marker != marker)
            Teardown(); // a different program instance took over
        if (_view != null)
            return _view;

        try
        {
            _view = new WebViewControl
            {
                AlwaysActive = true,
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            _ipc = new WebUiTuiIpc(OnAction)
            {
                // Streams load via media elements, not navigation; no hosts
                // need allow-listing.
                AllowHttpHosts = new List<string>(),
            };
            _view.AddBeforeBrowseHandler(_ipc.HandleBeforeBrowse);
            _driver = new WebRadioDriver(_ipc);
            _driver.Attach(_view);
            _view.Url = "res://webres/_Pirate/WebUI/Radio/index.html";
            _marker = marker;
            _requestedCatalog = false;
        }
        catch
        {
            Teardown(); // headless/dev
        }

        return _view;
    }

    public WebViewControl? View => _view;

    private void OnAction(string action, string? data)
    {
        switch (action)
        {
            case "play":
                PirateRadioClientState.Send("play", _marker, ExtractStationId(data));
                break;
            case "stop":
                PirateRadioClientState.Send("stop", _marker);
                break;
            case "volume":
                // Volume is page-local; kept for future persistence.
                break;
        }
    }

    private static string ExtractStationId(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return "";

        // Deliberately no System.Text.Json: the sandbox allowlist only
        // permits the Serialization attributes. Payload is {"id":"..."}.
        const string key = "\"id\"";
        var at = data.IndexOf(key, StringComparison.Ordinal);
        if (at < 0)
            return "";
        var colon = data.IndexOf(':', at + key.Length);
        if (colon < 0)
            return "";
        var i = colon + 1;
        while (i < data.Length && (data[i] == ' ' || data[i] == '\t'))
            i++;
        if (i >= data.Length || data[i] != '"')
            return "";
        i++;
        var sb = new System.Text.StringBuilder();
        while (i < data.Length && data[i] != '"')
        {
            if (data[i] == '\\' && i + 1 < data.Length)
            {
                i++;
                sb.Append(data[i] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    _ => data[i],
                });
            }
            else
            {
                sb.Append(data[i]);
            }
            i++;
        }
        return sb.ToString();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_view == null || _driver == null || _ipc == null || _marker == NetEntity.Invalid)
            return;

        if (_view.Disposed)
        {
            Teardown();
            return;
        }

        // Program uninstalled / PDA destroyed: kill the playback.
        if (!Exists(GetEntity(_marker)))
        {
            Teardown();
            return;
        }

        // Radio plays only while the PDA carrying the program is on us.
        if (!Carried())
        {
            var st = PirateRadioClientState.Get(_marker);
            if (st.Playing && !_uncarriedStopSent)
            {
                _uncarriedStopSent = true;
                PirateRadioClientState.Send("stop", _marker);
            }
        }
        else
        {
            _uncarriedStopSent = false;
        }

        _ipc.Pump();

        if (!_requestedCatalog)
        {
            _requestedCatalog = true;
            PirateRadioClientState.RequestCatalog(_marker);
        }

        var catalog = PirateRadioClientState.Catalog(_marker);
        if (catalog != null)
            _driver.SetCatalog(ToTuples(catalog));

        var state = PirateRadioClientState.Get(_marker);
        _driver.SetState(state.StationId, state.Playing);
    }

    /// <summary>Is the program's loader chain anchored at the local player?</summary>
    private bool Carried()
    {
        var player = _players.LocalEntity;
        if (player == null)
            return true; // main menu / detached: don't fight the state

        var cur = GetEntity(_marker);
        for (var depth = 0; depth < 8 && cur.IsValid(); depth++)
        {
            if (cur == player)
                return true;
            if (!_containers.TryGetContainingContainer(cur, out var container))
                return false;
            cur = container.Owner;
        }
        return false;
    }

    private void Teardown()
    {
        try { _view?.Dispose(); } catch { /* already gone */ }
        _view = null;
        _ipc = null;
        _driver = null;
        _marker = NetEntity.Invalid;
        _requestedCatalog = false;
        _uncarriedStopSent = false;
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
