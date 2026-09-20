// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Client.UserInterface.Fragments;
using Content.Pirate.Client._Pirate.WebUI;
using Content.Pirate.Client.Radio;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.WebView;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Pirate.Client.CartridgeLoader.Cartridges;

/// <summary>
///     PID internet-radio program. Hosts a CEF page that plays curated
///     Ogg/Opus streams; the page owns playback, the server owns the
///     selected station.
/// </summary>
public sealed partial class RadioUi : UIFragment
{
    private PanelContainer? _root;
    private WebRadioDriver? _driver;
    private PirateRadioClientSystem? _system;
    private NetEntity _marker = NetEntity.Invalid;

    public override Control GetUIFragmentRoot() => _root!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _root = new PanelContainer { HorizontalExpand = true, VerticalExpand = true };

        try
        {
            _marker = fragmentOwner is { } owner && owner.IsValid()
                ? IoCManager.Resolve<IEntityManager>().GetNetEntity(owner)
                : NetEntity.Invalid;

            _system = EntitySystem.Get<PirateRadioClientSystem>();

            var web = new WebViewControl
            {
                AlwaysActive = true,
                HorizontalExpand = true,
                VerticalExpand = true,
            };

            var ipc = new WebUiTuiIpc(OnIpcAction)
            {
                // Streams are loaded by media elements (not navigation), so
                // no http hosts need allow-listing here.
                AllowHttpHosts = new System.Collections.Generic.List<string>(),
            };
            web.AddBeforeBrowseHandler(ipc.HandleBeforeBrowse);

            _driver = new WebRadioDriver(ipc);
            _driver.Attach(web);
            _driver.Action += OnAction;

            _root.AddChild(web);
            _system.Register(_driver, _marker, () => _root is { Disposed: false });

            web.Url = "res://webres/_Pirate/WebUI/Radio/index.html";
        }
        catch
        {
            // Headless/dev: show a placeholder instead of crashing.
            _root.AddChild(new Label { Text = "Radio unavailable (headless)." });
        }
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        // State arrives via raw network events, not the BUI; nothing to do.
    }

    private void OnIpcAction(string action, string? data)
    {
        if (_driver == null)
            return;
        _driver.HandleAction(action, data);
    }

    private void OnAction(string action, string? data)
    {
        if (_driver == null || _marker == NetEntity.Invalid)
            return;

        switch (action)
        {
            case "ready":
                if (_system != null)
                    return; // system requests the catalog each frame
                PirateRadioClientState.RequestCatalog(_marker);
                break;
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
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("id", out var id))
                return id.GetString() ?? "";
        }
        catch { }
        return "";
    }

    /// <summary>Called by the BUI when the program is closed.</summary>
    public void Detach()
    {
        if (_driver != null)
        {
            _driver.Action -= OnAction;
            _system?.Unregister(_driver);
            _driver = null;
        }
    }
}
