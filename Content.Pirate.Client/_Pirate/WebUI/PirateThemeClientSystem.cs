// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Pirate.Shared.WebUi;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Client half of the device WebUI theme picker. Opens and pumps the
///     window, carries its bridge actions over the shared network events,
///     and feeds state pushes back into open windows.
/// </summary>
public sealed class PirateThemeClientSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    private readonly Dictionary<NetEntity, WebThemeWindow> _windows = new();
    private readonly List<NetEntity> _dead = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateThemeStateEvent>(OnState);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        foreach (var (pda, window) in _windows)
        {
            if (window.Disposed)
                _dead.Add(pda);
            else
                window.Pump();
        }
        foreach (var pda in _dead)
            _windows.Remove(pda);
        _dead.Clear();
    }

    /// <summary>PdaMenu theme button: open (or refocus) the picker for this PDA.</summary>
    public void ToggleWindow(EntityUid pda) => OpenFor(pda);

    private void OpenFor(EntityUid pda)
    {
        var net = _entMan.GetNetEntity(pda);
        if (_windows.TryGetValue(net, out var existing) && !existing.Disposed)
            return;

        var window = new WebThemeWindow(net, (action, data) => HandleAction(net, action, data));
        _windows[net] = window;
        window.OpenCentered();
        window.Load(); // page loads after the window is shown (arcade recipe)
        RequestState(net);
    }

    private void RequestState(NetEntity pda)
    {
        IoCManager.Resolve<IEntityNetworkManager>()
            .SendSystemNetworkMessage(new PirateThemeListRequestEvent { Pda = pda });
    }

    private void OnState(PirateThemeStateEvent msg)
    {
        Robust.Shared.Log.Logger.DebugS("webui.theme", $"client state push for {msg.Pda} cur={msg.Current} allowed={string.Join(',', msg.Allowed)}");
        if (!_windows.TryGetValue(msg.Pda, out var window) || window.Disposed)
        {
            // The server pushed state without a window: the user just
            // opened the picker from the PDA settings tab.
            var pda = _entMan.GetEntity(msg.Pda);
            if (pda.IsValid())
                OpenFor(pda);
            return;
        }
        window.ApplyState(msg.Current, msg.Allowed);
    }

    private void HandleAction(NetEntity pda, string action, string? data)
    {
        switch (action)
        {
            case "ready":
                // The page's listeners are up; the server reply supplies the
                // full snapshot (also re-pushes after any stale race).
                RequestState(pda);
                break;
            case "set":
                if (data is { Length: > 0 })
                    IoCManager.Resolve<IEntityNetworkManager>().SendSystemNetworkMessage(
                        new PirateThemeSetEvent { Pda = pda, ThemeId = data });
                break;
            case "closed":
                _windows.Remove(pda);
                break;
        }
    }

}
