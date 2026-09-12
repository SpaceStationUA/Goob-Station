// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.MalfAI;

namespace Content.Client._Pirate.MalfAI;

/// <summary>
/// Receives server requests to open the Malf AI viewport window.
/// </summary>
public sealed class MalfAiViewportSystem : EntitySystem
{

    private MalfAiViewportWindow? _window;
    private NetEntity? _anchor;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<MalfAiViewportOpenEvent>(OnOpenViewport);
        SubscribeNetworkEvent<MalfAiViewportCloseEvent>(OnCloseViewport);
    }

    private void OnOpenViewport(MalfAiViewportOpenEvent ev)
    {
        CloseWindow();
        _anchor = ev.AnchorEntity;
        _window = new MalfAiViewportWindow(ev.MapId, ev.WorldPosition, ev.SizePixels, ev.Title, ev.Rotation, ev.ZoomLevel, ev.AnchorEntity);
        _window.OnClose += OnWindowClosed;
    }

    private void OnCloseViewport(MalfAiViewportCloseEvent ev)
    {
        CloseWindow();
    }

    private void OnWindowClosed()
    {
        RaiseNetworkEvent(new MalfAiViewportClosedEvent(_anchor));
        _window = null;
        _anchor = null;
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        // Server-directed replacement/closure must not report a manual close of the new window.
        _window.OnClose -= OnWindowClosed;
        _window.Close();
        _window = null;
        _anchor = null;
    }
}
