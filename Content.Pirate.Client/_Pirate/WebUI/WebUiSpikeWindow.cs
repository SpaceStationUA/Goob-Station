// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.WebView;
using Robust.Shared.Timing;
using Robust.Shared.Maths;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Spike window hosting a <see cref="WebViewControl"/> pointed at the test page.
///     Window chrome (titlebar, dragging, resizing) stays native; only the content
///     region is a browser.
/// </summary>
public sealed class WebUiSpikeWindow : DefaultWindow
{
    private const string PageUrl = WebArcadeWindow.ResPrefix + "_Pirate/WebUI/spike.html";
    private const string titleBase = "Pirate WebUI — ";

    public readonly WebViewControl Web;

    /// <summary>
    ///     Invoked when the page posts an action back over the bridge.
    ///     Second argument is the opaque URL-escaped data string (may be null).
    /// </summary>
    public event Action<string, string?>? ActionReceived;

    private readonly WebUiTuiIpc _ipc;

    public WebUiSpikeWindow(bool allowExternal = false)
    {
        Title = "Pirate WebUI Spike";
        SetSize = new Vector2i(500, 400);

        _ipc = new WebUiTuiIpc((action, data) => ActionReceived?.Invoke(action, data), allowExternal);

        Web = new WebViewControl();
        Web.AddBeforeBrowseHandler(_ipc.HandleBeforeBrowse);
        Web.VerticalExpand = true;
        Web.HorizontalExpand = true;

        Contents.AddChild(Web);
    }

    public void OpenCenteredSpike()
    {
        OpenCentered();
        Web.Url = PageUrl;
        _ipc.Attach(Web);
    }

    public void OpenUrl(string url)
    {
        Title = titleBase + url;
        OpenCentered();
        Web.Url = url;
        // Attach only when the page is actually ours; external pages have no bridge.
        if (url.StartsWith("res://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("usr://", StringComparison.OrdinalIgnoreCase))
            _ipc.Attach(Web);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Dispatch bridge actions queued from the CEF UI thread.
        _ipc.Pump();
    }
}
